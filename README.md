# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values.

## Requirements:
 * .NET Framework 4.6
 * MSBuild 14.0
 * Visual Studio 2015

## Usage:

First build your solution and pass the logger:

```
msbuild solution.sln /t:Rebuild /noconlog /logger:StructuredLogger,C:\MSBuildStructuredLog\bin\Debug\StructuredLogger.dll;buildlog1.xml
```
