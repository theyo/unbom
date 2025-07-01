// <copyright file="Program.cs" company="Sedat Kapanoglu">
// Copyright © 2016-2020 Sedat Kapanoglu
// SPDX-License-Identifier: MIT
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Unbom
{
    internal static class Program
    {
        private static readonly byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };

        /// <summary>
        /// Removes BOM markers from UTF-8 files.
        /// </summary>
        /// <param name="argument">Path to scan.</param>
        /// <param name="recurse">Recurse subdirectories.</param>
        /// <param name="backup">Save a backup file.</param>
        public static void Main(string argument, bool recurse = false, bool backup = true)
        {


            UnBom(argument, recurse, backup);
        }

        private static void UnBom(string path, bool recurse = false, bool backup = false)
        {
            Debug.WriteLine($"path={path} recurse={recurse} backup={backup}");

            var pattern = path.Contains(Path.DirectorySeparatorChar) || path.Contains(Path.AltDirectorySeparatorChar) ? Path.GetFileName(path) : path;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                pattern = "*";
            }
            else
            {
                path = Path.GetDirectoryName(path) ?? ".";
            }

            Debug.WriteLine($"path={path} pattern={pattern}");

            try
            {
                var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                var files = Directory.EnumerateFiles(path, pattern, searchOption);

                var count = 0;

                foreach (var fileName in files)
                {
                    RemoveBomMarkers(fileName, backup);
                    count++;
                }

                Console.WriteLine($"{count} file(s) processed");
            }
            catch (DirectoryNotFoundException)
            {
                Console.Error.WriteLine($"Directory not found: {path}");
            }
        }

        private static void RemoveBomMarkers(string fileName, bool backup)
        {
            Span<byte> buffer = stackalloc byte[bom.Length];

            using var reader = new StreamReader(fileName, detectEncodingFromByteOrderMarks: true);

            if (reader.CurrentEncoding != Encoding.UTF8)
            {
                // Not a UTF-8 file, nothing to do
                return;
            }

            // read first few bytes to check for BOM and leave the stream after the BOM
            var readBytes = reader.BaseStream.Read(buffer);

            if (readBytes != buffer.Length || !buffer.SequenceEqual(bom))
            {
                // No BOM detected, nothing to do
                return;
            }

            Console.Write("{0}: BOM found - removing...", fileName);

            if (backup)
            {
                var backupName = fileName + ".bak";
                File.Copy(fileName, backupName, true);
            }

            // GetTempFileName also creates the file
            var tempName = Path.GetTempFileName();

            try
            {
                using var writer = new StreamWriter(tempName, false, Encoding.UTF8);

                // Write the rest of the file (position is after BOM from reading BOM bytes earlier)
                writer.Write(reader.ReadToEnd());

                writer.Flush();
                writer.Close();

                //replace original with new file
                File.Move(tempName, fileName, true);

                Console.WriteLine("done");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing file {fileName}: {ex.Message}");

                if (File.Exists(tempName))
                {
                    File.Delete(tempName);
                }
            }

            reader.Close();
        }
    }
}