# binlogtool

Command-line utilities for MSBuild binary logs (`*.binlog`), from the
[MSBuild Structured Log Viewer](https://msbuildlog.com) project.

## Installation

```
dotnet tool install -g binlogtool
```

Update to the latest version:

```
dotnet tool update -g binlogtool
```

Alternatively, the [binlogtoolexe](https://www.nuget.org/packages/binlogtoolexe)
package contains the same tool as a standalone self-contained `BinlogTool.exe`
for Windows x64 that doesn't require the .NET SDK.

## Commands

| Command | Description |
|---|---|
| [`stats`](#stats) | Break down what takes up space in a binlog (text and/or HTML report) |
| [`search`](#search) | Search one or more binlogs using the viewer's search syntax |
| [`redact`](#redact) | Redact secrets from binlogs |
| [`savefiles`](#savefiles) | Save all files embedded in the binlog to disk |
| [`reconstruct`](#reconstruct) | Reconstruct the source tree from the binlog |
| [`savestrings`](#savestrings) | Save the deduplicated string table from the binlog |
| [`listtools`](#listtools) | List the tools and compilers invoked during the build |
| [`listnuget`](#listnuget) | List the NuGet packages referenced in the binlog |
| [`listproperties`](#listproperties) | List all property values seen during the build |
| [`compilerinvocations`](#compilerinvocations) | List the compiler invocations in the binlog |
| [`doublewrites`](#doublewrites) | Report files written more than once during the build |
| [`dumprecords`](#dumprecords) | Low-level dump of the records contained in binlogs |

Run `binlogtool <command> --help` for the full help of any command.

### stats

```
binlogtool stats input.binlog [--html [report.html]] [--text [report.txt]] [--strings]
```

Analyzes which record types, strings, name-value lists and blobs take up space in
the binlog — think WinDirStat for binlog size. Buckets are sorted by total size
and drill down into subcategories, with sample record texts drawn from the top
of each bucket and from each size percentile (p90, p80, …) so you can see what
the bytes actually are.

* With no options, prints a plain text report to stdout. The format is designed
  to be friendly to both humans and LLMs — point an AI agent at it and ask what
  makes the binlog large.
* `--html [path]` writes a standalone self-contained HTML report with charts and
  interactive drill-down. The path is optional and defaults to
  `<input>.stats.html`.
* `--text [path]` writes the text report to a file instead of stdout. The path
  is optional and defaults to `<input>.stats.txt`.
* `--strings` additionally collects and includes the largest strings from the
  string table (uses more memory on large binlogs).

```
binlogtool stats msbuild.binlog
binlogtool stats msbuild.binlog --html --text
binlogtool stats msbuild.binlog --html report.html --strings
```

### search

```
binlogtool search *.binlog <search string>
```

Searches one or more binlogs and prints matching nodes. The first argument can
be a file, a directory, or a wildcard pattern (subdirectories are searched
recursively); the rest of the command line is the search query. Supports the
same search syntax as the [Structured Log Viewer](https://msbuildlog.com),
e.g. `$error`, `$task csc`, `under()`, `project()` etc.

```
binlogtool search msbuild.binlog $error
binlogtool search C:\logs CS8600
```

### redact

```
binlogtool redact --input:path -p:secret1 -p:secret2 [--recurse] [--in-place]
```

Redacts secrets from binlogs: explicitly provided strings (`-p:`) as well as
autodetected common credential patterns and usernames. Files embedded in the
binlog are processed too. Unless `--in-place` is specified, writes the result
to `<input>.redacted.binlog`.

* `--input` — binlog file or directory to redact. Defaults to the current
  working directory.
* `-p` — a secret string to redact. Can be specified multiple times.
* `--recurse` — recurse into subdirectories when the input is a directory.
* `--in-place` — overwrite the input logs instead of writing new ones with a
  suffix.

### savefiles

```
binlogtool savefiles input.binlog output_path
```

Saves all files embedded in the binlog (project files, imported targets,
response files, source-generated files, etc.) into the output directory.

### reconstruct

```
binlogtool reconstruct input.binlog output_path
```

Like `savefiles`, but reconstructs the original source tree layout from the
files embedded in the binlog.

### savestrings

```
binlogtool savestrings input.binlog output.txt
```

Saves the deduplicated string table of the binlog into a text file, sorted.
Useful for diffing binlogs or inspecting what text data a binlog contains
(see also `stats --strings` for just the largest strings).

### listtools

```
binlogtool listtools input.binlog
```

Prints the MSBuild version, the source commit (when available) and the set of
tools, compilers and task assemblies invoked during the build.

### listnuget

```
binlogtool listnuget input.binlog [output_path]
```

Lists the NuGet packages referenced in the binlog. Prints to stdout or writes to
the optional output file.

### listproperties

```
binlogtool listproperties input.binlog
```

Prints all `Name=Value` property combinations observed in project evaluations
and project builds, sorted by property name.

### compilerinvocations

```
binlogtool compilerinvocations input.binlog [output_path]
```

Lists the compiler invocations (command lines) in the binlog. Prints to stdout
or writes to the optional output file.

### doublewrites

```
binlogtool doublewrites input.binlog [output_path]
```

Reports files that were written more than once during the build (a common
source of race conditions and non-deterministic builds), together with the
sources they were copied from.

### dumprecords

```
binlogtool dumprecords [path] [--include-total] [--include-rollup] [--exclude-details]
```

Low-level dump of the raw records contained in binlogs, for debugging the
binlog format itself. `path` is a binlog file or directory and defaults to the
current working directory.

* `--include-total` — include the total record count.
* `--include-rollup` — include a per-record-type rollup (count and average size).
* `--exclude-details` — exclude the per-record listing.

For a higher-level, size-oriented view see the `stats` command.

## Links

* [MSBuild Structured Log Viewer](https://msbuildlog.com)
* [GitHub repository](https://github.com/KirillOsenkov/MSBuildStructuredLog)
