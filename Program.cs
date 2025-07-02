// <copyright file="Program.cs" company="Sedat Kapanoglu">
// Copyright © 2016-2020 Sedat Kapanoglu
// SPDX-License-Identifier: MIT
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unbom
{
    internal class Statistics
    {
        public int TotalFilesEvaluated { get; set; }
        public int Utf8WithBomFiles { get; set; }
        public int BomRemovedFiles { get; set; }
        public int ErrorsEncountered { get; set; }

        public void PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("=== Processing Summary ===");
            Console.WriteLine("Total files evaluated: {0}", TotalFilesEvaluated);
            Console.WriteLine("Files with UTF-8 BOM: {0}", Utf8WithBomFiles);
            Console.WriteLine("BOM successfully removed: {0}", BomRemovedFiles);
            Console.WriteLine("Files skipped (no BOM): {0}", TotalFilesEvaluated - Utf8WithBomFiles);
            Console.WriteLine("Errors encountered: {0}", ErrorsEncountered);

            if (Utf8WithBomFiles > 0)
            {
                var successRate = (double)BomRemovedFiles / Utf8WithBomFiles * 100;
                Console.WriteLine("Success rate: {0:F1}%", successRate);
            }
        }
    }

    internal static class Program
    {
        private static readonly byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
        internal static readonly string[] defaultPatterns = new string[] { "*" };

        internal static Statistics statistics = new();

        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("Removes BOM markers from UTF-8 files");

            rootCommand.Arguments.Add(new Argument<string[]>("pattern")
            {
                Description = "Files to process. e.g., *.txt, *.cs, etc. Multiple patterns can be provided.",
                Arity = ArgumentArity.ZeroOrMore,
                DefaultValueFactory = _ => defaultPatterns
            });

            rootCommand.Options.Add(
            new Option<DirectoryInfo>("--path")
            {
                Description = "Path to scan. e.g., ./",
                Arity = ArgumentArity.ExactlyOne,
                DefaultValueFactory = _ => new DirectoryInfo("./"),
            });

            rootCommand.Options.Add(
                new Option<bool>("--recurse", "-r")
                {
                    Description = "Recurse subdirectories.",
                    Arity = ArgumentArity.Zero,
                    DefaultValueFactory = _ => false
                });

            rootCommand.Options.Add(
                new Option<bool>("--noBackup", "-n")
                {
                    Description = "Do not save a backup file.",
                    Arity = ArgumentArity.Zero,
                    DefaultValueFactory = _ => false,
                });

            rootCommand.SetAction(async parsedResult =>
            {
                var patterns = parsedResult.GetValue<string[]>("pattern");
                var directory = parsedResult.GetValue<DirectoryInfo>("--path");
                var recurse = parsedResult.GetValue<bool>("--recurse");
                var noBackup = parsedResult.GetValue<bool>("--noBackup");

                foreach (var pattern in patterns!)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        Console.Error.WriteLine("Invalid pattern: '{0}'", pattern);
                        return; // Exit with error code
                    }
                }

                await UnBom(directory!, patterns!, recurse, !noBackup);
            });

            var parseResult = rootCommand.Parse(args);

            var result = parseResult.Invoke();

            statistics.PrintSummary();

            return result;
        }

        private static async Task UnBom(DirectoryInfo directory, string[] patterns, bool recurse, bool backup)
        {
            Debug.WriteLine($"directory={directory.FullName} argument={patterns} recurse={recurse} backup={backup}");

            try
            {
                var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                var tasks = new List<Task>();

                foreach (var pattern in patterns!)
                {
                    try
                    {
                        var files = directory.EnumerateFiles(pattern, searchOption);

                        foreach (var file in files)
                        {
                            tasks.Add(RemoveBomMarkers(file, backup, statistics));
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.Error.WriteLine("Access denied to files in directory {0}: {1}", directory.FullName, ex.Message);
                    }
                }

                await Task.WhenAll(tasks);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine("Access denied to directory {0}: {1}", directory.FullName, ex.Message);
            }
        }

        private static async Task RemoveBomMarkers(FileInfo file, bool backup, Statistics statistics)
        {
            statistics.TotalFilesEvaluated++;

            if (!file.Exists || file.Length < bom.Length)
            {
                Debug.WriteLine("{0}: File does not exist or is too short to contain BOM.", file.FullName);
                return;
            }

            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                // Read BOM bytes
                var bomBuffer = new byte[bom.Length];
                var bytesRead = await stream.ReadAsync(bomBuffer, 0, bomBuffer.Length).ConfigureAwait(false);

                if (bytesRead != bom.Length || !bomBuffer.SequenceEqual(bom))
                {
                    // No BOM detected, nothing to do
                    return;
                }

                statistics.Utf8WithBomFiles++;

                Console.Write("{0}: BOM found - removing...", file.FullName);

                FileInfo? backupFileInfo = null;

                if (backup)
                {
                    var backupFileName = file.FullName + ".bak";
                    Debug.WriteLine("Backup enabled, creating {0}...", backupFileName);

                    backupFileInfo = file.CopyTo(backupFileName, true);

                    Debug.Write("done!", backupFileName);
                }

                // GetTempFileName also creates the file
                var tempName = Path.GetTempFileName();

                try
                {
                    using var tempStream = new FileStream(tempName, FileMode.Create, FileAccess.Write);

                    // Stream the rest of the file (excluding BOM)
                    var bufferSize = 8192; // 8 KB buffer
                    var readBuffer = new byte[bufferSize];

                    while (stream.Position < stream.Length)
                    {
                        bytesRead = await stream.ReadAsync(readBuffer, 0, bufferSize).ConfigureAwait(false);
                        await tempStream.WriteAsync(readBuffer, 0, bytesRead).ConfigureAwait(false);
                    }

                    await tempStream.FlushAsync().ConfigureAwait(false);

                    // Replace original with new file
                    File.Move(tempName, file.FullName, true);

                    statistics.BomRemovedFiles++;
                    Console.Write("done!");
                }
                catch (Exception ex)
                {
                    statistics.ErrorsEncountered++;
                    Console.Error.WriteLine("Error processing file {0}: {1}", file.FullName, ex.Message);

                    if (backup && backupFileInfo is FileInfo backupFile && backupFile.Exists)
                    {
                        try
                        {
                            backupFile.MoveTo(file.FullName, true);
                        }
                        catch (Exception exRestoreBackup)
                        {
                            Console.Error.WriteLine("Could not restore file {0}: {1}", file.FullName, exRestoreBackup.Message);
                            // Note: Not incrementing error count for backup restoration failures as they're secondary errors
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tempName))
                    {
                        Debug.WriteLine("Temp file {0} still exists. Cleaning up...", tempName);

                        File.Delete(tempName);

                        Debug.Write("done!");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("{0}: Access denied. {1}", file.FullName, ex.Message);
                statistics.ErrorsEncountered++;
            }
            catch (IOException ex)
            {
                Console.WriteLine("{0}: Cannot access file. {1}", file.FullName, ex.Message);
                statistics.ErrorsEncountered++;
            }
        }
    }
}