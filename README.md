# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values.

[![Build status](https://ci.appveyor.com/api/projects/status/v7vwgphs239i14ya?svg=true)](https://ci.appveyor.com/project/KirillOsenkov/msbuildstructuredlog)

## Install:
https://github.com/KirillOsenkov/MSBuildStructuredLog/releases/download/v1.0.2/MSBuildStructuredLogSetup.exe

The app updates automatically via [Squirrel](https://github.com/Squirrel/Squirrel.Windows) (after launch it checks for updates in background), next launch starts the newly downloaded latest version.

![Screenshot1](/docs/Screenshot1.png)

## Requirements:
 * .NET Framework 4.6
 * MSBuild 14.0
 * Visual Studio 2015

## Usage:

You can either build your solution yourself and pass the logger:

```
msbuild solution.sln /t:Rebuild /v:diag /noconlog /logger:StructuredLogger,C:\MSBuildStructuredLog\bin\Debug\StructuredLogger.dll;buildlog1.xml
```

or you can build the solution or open an existing .xml log file through the viewer app:

![Screenshot2](/docs/Screenshot2.png)

## Features:

 * Displays double-writes (when files from different sources are written to the same destination during a build, thus causing non-determinism)
 * Displays target dependencies for each target
 * Text search through the entire log
 * Ctrl+C to copy an item and the entire subtree to Clipboard as text
 * Delete to hide nodes from the tree (to get uninteresting stuff out of the way)
 * Open and save .xml log files (ask a friend to record and send you the .xml log which you can then investigate on your machine)

## MSBuild Resources
 * [https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/MSBuild-Resources](https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/MSBuild-Resources)
 * [https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/MSBuild-Tips-&-Tricks](https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/MSBuild-Tips-&-Tricks)
