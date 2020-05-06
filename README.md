# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values. It can greatly simplify build investigations and provides a portable log interchange format and a rich interactive log viewer app.

[![Build status](https://ci.appveyor.com/api/projects/status/v7vwgphs239i14ya?svg=true)](https://ci.appveyor.com/project/KirillOsenkov/msbuildstructuredlog)
[![NuGet package](https://img.shields.io/nuget/v/MSBuild.StructuredLogger.svg)](https://nuget.org/packages/MSBuild.StructuredLogger)
[![Chocolatey](https://img.shields.io/chocolatey/v/msbuild-structured-log-viewer.svg)](https://chocolatey.org/packages/msbuild-structured-log-viewer)

Homepage: http://msbuildlog.com

Important: the NuGet package is now being published to https://nuget.org/packages/MSBuild.StructuredLogger (old location: https://nuget.org/packages/Microsoft.Build.Logging.StructuredLogger). Please update to use the new Package Id.

Thanks to https://signpath.io for generously providing a certificate to sign the installer.
![signpath.io](https://about.signpath.io/wp-content/uploads/2018/11/logo_signpath_500.png)

## Install:
https://github.com/KirillOsenkov/MSBuildStructuredLog/releases/download/v2.1.133/MSBuildStructuredLogSetup.exe

The app updates automatically via [Squirrel](https://github.com/Squirrel/Squirrel.Windows) (after launch it checks for updates in background), next launch starts the newly downloaded latest version.

![Screenshot1](http://msbuildlog.com/Screenshot1.png)

## Running the Avalonia version on Mac:

1. `git clone https://github.com/KirillOsenkov/MSBuildStructuredLog`
2. `dotnet build`
3. `dotnet publish --self-contained -o <some_dir>` (I used $HOME/tools/artifacts/StructuredLogViewer.Avalonia)
4. make a script `$HOME/bin/structured-log-viewer` (or whatever's on your PATH):

```
#! /bin/sh
exec dotnet ${HOME}/tools/artifacts/StructuredLogViewer.Avalonia/publish/StructuredLogViewer.Avalonia.dll "$@"
```

## Requirements:
 * .NET Framework 4.7.2
 * MSBuild 16.0
 * Visual Studio 2019

## Usage:

Starting with MSBuild 15.3 you can just pass the new `/bl` switch to `msbuild.exe` to record a binary build log to `msbuild.binlog`, in the same folder as the project/solution being built:

![Screenshot](http://msbuildlog.com/BinLogFromCommandLine.png)

or you can build the solution or open an existing log file through the viewer app:

![Screenshot2](/docs/Screenshot2.png)

Alternatively (useful for older versions of MSBuild) you can attach the logger to any MSBuild-based build using the logger library: `StructuredLogger.dll`. It is available in a NuGet package:
https://www.nuget.org/packages/MSBuild.StructuredLogger

```
msbuild solution.sln /t:Rebuild /v:diag /noconlog /logger:BinaryLogger,%localappdata%\MSBuildStructuredLogViewer\app-2.1.133\StructuredLogger.dll;1.binlog
```

To use a portable version of the logger (e.g. with the `dotnet msbuild` command) you need a .NET Standard version of `StructuredLogger.dll`, not the .NET Framework (Desktop) version.

Download this NuGet package: https://www.nuget.org/packages/MSBuild.StructuredLogger/2.1.88
and inside it there's the `lib\netstandard2.0\StructuredLogger.dll`. Try passing that to `dotnet build` like this:
```
dotnet msbuild Some.sln /v:diag /nologo /logger:BinaryLogger,"packages\MSBuild.StructuredLogger.2.1.133\lib\netstandard2.0\StructuredLogger.dll";"C:\Users\SomeUser\Desktop\binarylog.binlog"
```

The logger supports three file formats:

 1. `*.binlog` (official MSBuild binary log format, same as `msbuild.exe /bl`)
 2. `*.buildlog` (when you Save As... in the Viewer)
 3. `*.xml` (for large human-readable XML logs)
 
The viewer can read all 3 formats and can save to either `.buildlog` or `.xml`.

Read more about the log formats here:
https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/Log-Format

## Features:

 * Displays [double-writes](https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/Double%20write%20detection) (when files from different sources are written to the same destination during a build, thus causing non-determinism)
 * Displays target dependencies for each target
 * Text search through the entire log
 * Narrow down the search results using the under() clause to only display results under a certain parent.
 * Each node in the tree has a context menu. Ctrl+C to copy an item and the entire subtree to Clipboard as text.
 * Delete to hide nodes from the tree (to get uninteresting stuff out of the way).
 * Open and save log files (ask a friend to record and send you the log which you can then investigate on your machine)
 * Logs can include the source code project files and all imported files used during the build.
 * If a log has embedded files, you can view the list of files, full-text search in all files, and use the Space key (or double-click) on most nodes to view the source code.
 * If MSBuild 15.3 or later was used during the build the log can also display a preprocessed view for each project, with all imported projects inlined.
 * The viewer associates with `*.binlog` and `*.buildlog` so you can double-click a log file in Explorer to open it.

## Differences between StructuredLogger and the new BinaryLogger that is shipped with MSBuild as of 15.3

Starting with Visual Studio 2017 Update 3 MSBuild supports a new `BinaryLogger` documented here:
https://github.com/Microsoft/MSBuild/wiki/Binary-Log

`BinaryLogger` is the low-level and exact representation of the events that happened during the build in the original order. You can fully reconstruct other build logs from a binary log by "playing back" the log file. `.binlog` is the file extension. Pass a `.binlog` file to MSBuild instead of a project or solution to "replay it" and specify the target loggers to replay to as if a real build was happening.

`StructuredLogger` is the format used by the viewer to save build trees to disk. It is as comprehensive as the binary log, but you can't easily reconstruct other logs from this format, it is only used by the viewer.

The Structured Log Viewer is able to open both formats, but can only save the `.buildlog` format. Here are some differences between the `BinaryLogger` and the `StructuredLogger`:

 * `BinaryLogger` (format used by `msbuild /bl`) will consume less memory and disk size, whereas the `StructuredLogger` `.buildlog` format may run out of memory on very large builds (by design).

 * `StructuredLogger` captures the target graph so it is able to show DependsOn list for each target and also order targets slightly better in some cases.

 * `StructuredLogger` is slightly more compact since it is not streaming and thus able to use string deduplication, reducing the log file up to 10x smaller compared to `.binlog`.

 * `BinaryLogger` is replayable and you're able to reconstruct text logs of any verbosity out of it. It is hard to reconstruct text logs of conventional format from a StructuredLogger log.

Other than that they're pretty much equivalent and both can be opened in the viewer.

## Investigating problems with MSBuildStructuredLog

Open an issue if you're running into something weird and I can take a look into it. If MSBuildStructuredLog crashes during the build, it will attempt to write the exception call stack to:

```
%localappdata%\Microsoft\MSBuildStructuredLog\LoggerExceptions.txt
```

## MSBuild Resources
 * [http://msbuildlog.com](http://msbuildlog.com)
 * [https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/MSBuild-Resources.md](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/MSBuild-Resources.md)
 * [https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/MSBuild-Tips-&-Tricks.md](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/MSBuild-Tips-&-Tricks.md)
