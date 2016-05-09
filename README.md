# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values.

## Usage:

First build your solution and pass the logger:

```
msbuild solution.sln /noconlog /logger:StructuredLogger,C:\MSBuildStructuredLog\bin\Debug\StructuredLogger.dll;buildlog1.xml
```
