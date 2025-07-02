unbom
=====
Tool to remove UTF-8 BOM markers from files.

## why
UTF-8 BOMs are problematic and useless in common cases. A lot of tools don't support BOM markers; command-line
diff tools for instance. Tools that support UTF-8, support it without markers anyway.
They serve no purpose other than making our diff outputs annoying.

Visual Studio defaults to UTF-8+BOM causing the problems.

There are many shell scripts available for removing BOM markers. Many of them either slow or buggy. They assume BOM
marker can appear inside a file. A careless user can easily corrupt their files. 
I just wanted a simple and safer tool so I spent an hour on this. 

## install
Install unbom by running the command:

```
dotnet tool install --global unbom
```

## usage

Description:
  Removes BOM markers from UTF-8 files

Usage:
  unbom [<pattern>...] [options]

Arguments:
  <pattern>  Files to process (e.g., *.txt, *.cs, etc.) Multiple patterns can be provided. [default: *]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information
  --path          Path to scan. e.g., ./ [default: ./]
  -r, --recurse   Recurse subdirectories. [default: False]
  -n, --noBackup  Do not save a backup file. [default: False]

## example
Remove UTF-8 BOM markers from all files with ".cs" extensions in the current directory and subdirectories without saving backup files.

    unbom *.cs -r -n

Remove UTF-8 BOM markers from all files with ".cs" or ".scss" extensions in a different directory and subdirectories without saving backup files.

    unbom *.cs *.scss --path ./some-path/ -r -n

## license

MIT License. See [LICENSE!](LICENSE) file for details.
