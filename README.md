# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values. It can greatly simplify build investigations and provides a portable log interchange format (*.binlog) and a rich interactive log viewer app.

[![Build status](https://ci.appveyor.com/api/projects/status/v7vwgphs239i14ya?svg=true)](https://ci.appveyor.com/project/KirillOsenkov/msbuildstructuredlog)
[![NuGet package](https://img.shields.io/nuget/v/MSBuild.StructuredLogger.svg)](https://nuget.org/packages/MSBuild.StructuredLogger)
[![Chocolatey](https://img.shields.io/chocolatey/v/msbuild-structured-log-viewer.svg)](https://chocolatey.org/packages/msbuild-structured-log-viewer)

Homepage: https://msbuildlog.com

View binlogs in the Browser: https://live.msbuildlog.com

Important: the NuGet package is now being published to https://nuget.org/packages/MSBuild.StructuredLogger (old location: https://nuget.org/packages/Microsoft.Build.Logging.StructuredLogger). Please update to use the new Package Id.

Thanks to [SignPath.io](https://signpath.io?utm_source=foundation&utm_medium=github&utm_campaign=MSBuildStructuredLog) for providing a free code signing service and to the [SignPath Foundation](https://signpath.org?utm_source=foundation&utm_medium=github&utm_campaign=MSBuildStructuredLog) for a free code signing certificate to sign the installer.

## Install:
Install from https://msbuildlog.com.

The app updates automatically via [Squirrel](https://github.com/Squirrel/Squirrel.Windows) (after launch it checks for updates in background), next launch starts the newly downloaded latest version.

![Screenshot1](https://msbuildlog.com/Screenshot1.png)

## Installing the Avalonia version on Mac:

There are a couple of extra steps to get the .app running on macOS since it's currently distributed as an unsigned application:

1. In _System Preferences -> Security & Privacy_ ensure _Allow apps downloaded from:_ is set to `App Store and identified developers`.
2. Download the _StructuredLogViewer-x64.zip_ (if on Intel Mac) or _Structured.Log.Viewer-arm64.zip_ (if on M1/ARM) from the latest [Release](https://github.com/KirillOsenkov/MSBuildStructuredLog/releases).
3. If necessary, unzip the file (Safari does this for you after downloading automatically).
4. Move the `StructuredLogViewer.app` into your `Applications` folder.
5. In terminal run: `chmod +x StructuredLogViewer.app/Contents/MacOS/StructuredLogViewer.Avalonia` to give the app execution permissions
6. In terminal run: `codesign -s- --deep StructuredLogViewer.app` to work around notarization issues
7. On the first run, right click the app on Finder and select _Open_.  You will be prompted that the app is not signed by a known developer. Click _Open_.



## Building & Running the Avalonia version on Mac:

```
git clone https://github.com/KirillOsenkov/MSBuildStructuredLog
cd MSBuildStructuredLog
./run.sh
```

Alternatively, a longer version:

1. `dotnet build MSBuildStructuredLog.Avalonia.sln`
2. `dotnet publish MSBuildStructuredLog.Avalonia.sln --self-contained -o <some_dir>` (I used $HOME/tools/artifacts/StructuredLogViewer.Avalonia)
3. make a script `$HOME/bin/structured-log-viewer` (or whatever's on your PATH):

```
#! /bin/sh
exec dotnet ${HOME}/tools/artifacts/StructuredLogViewer.Avalonia/publish/StructuredLogViewer.Avalonia.dll "$@"
```

## Requirements:

Windows:
 * .NET Framework 4.7.2
 * MSBuild 16.0
 * Visual Studio 2019

Mac:
 * .NET 6 SDK

## Usage:

Starting with MSBuild 15.3 you can pass the new `/bl` switch to `msbuild.exe` to record a binary build log to `msbuild.binlog`, in the same folder as the project/solution being built:

![Screenshot](https://msbuildlog.com/BinLogFromCommandLine.png)

or you can build the solution or open an existing log file through the viewer app:

![Screenshot2](/docs/Screenshot2.png)

Alternatively (useful for older versions of MSBuild) you can attach the logger to any MSBuild-based build using the logger library: `StructuredLogger.dll`. It is available in a NuGet package:
https://www.nuget.org/packages/MSBuild.StructuredLogger

```
msbuild solution.sln /t:Rebuild /v:diag /noconlog /logger:BinaryLogger,%localappdata%\MSBuildStructuredLogViewer\app-2.1.596\StructuredLogger.dll;1.binlog
```

To use a portable version of the logger (e.g. with the `dotnet msbuild` command) you need a .NET Standard version of `StructuredLogger.dll`, not the .NET Framework (Desktop) version.

Download this NuGet package: https://www.nuget.org/packages/MSBuild.StructuredLogger/2.1.545
and inside it there's the `lib\netstandard2.0\StructuredLogger.dll`. Try passing that to `dotnet build` like this:
```
dotnet msbuild Some.sln /v:diag /nologo /logger:BinaryLogger,"packages\MSBuild.StructuredLogger.2.1.545\lib\netstandard2.0\StructuredLogger.dll";"C:\Users\SomeUser\Desktop\binarylog.binlog"
```

Read more about the log formats here:
https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/Log-Format

## Features:

 * Preprocess project files (with all imports inlined), right-click on a project -> Preprocess
 * If a log has embedded files, you can view the list of files, full-text search in all files, and use the Space key (or double-click) on most nodes to view the source code.
 * Displays [double-writes](https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/Double%20write%20detection) (when files from different sources are written to the same destination during a build, thus causing non-determinism)
 * Displays target dependencies for each target
 * Narrow down the search results using the under() or project() clauses to only display results under a certain parent or project.
 * Each node in the tree has a context menu. Ctrl+C to copy an item and the entire subtree to Clipboard as text.
 * Delete to hide nodes from the tree (to get uninteresting stuff out of the way).
 * Open and save log files (option to save log files to .xml)
 * Logs can include the source code project files and all imported files used during the build.

## Investigating problems with MSBuildStructuredLog

Open an issue if you're running into something weird and I can take a look into it. If MSBuildStructuredLog crashes during the build, it will attempt to write the exception call stack to:

```
%localappdata%\Microsoft\MSBuildStructuredLog\LoggerExceptions.txt
```

## MSBuild Resources
 * [https://msbuildlog.com](https://msbuildlog.com)
 * [https://github.com/dotnet/msbuild/blob/main/documentation/wiki/MSBuild-Resources.md](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/MSBuild-Resources.md)
 * [https://github.com/dotnet/msbuild/blob/main/documentation/wiki/MSBuild-Tips-&-Tricks.md](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/MSBuild-Tips-&-Tricks.md)
