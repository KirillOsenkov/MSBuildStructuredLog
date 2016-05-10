# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values.

![Screenshot1](/docs/Screenshot1.png)

## Requirements:
 * .NET Framework 4.6
 * MSBuild 14.0
 * Visual Studio 2015

## Usage:

First build your solution and pass the logger:

```
msbuild solution.sln /t:Rebuild /v:diag /noconlog /logger:StructuredLogger,C:\MSBuildStructuredLog\bin\Debug\StructuredLogger.dll;buildlog1.xml
```
