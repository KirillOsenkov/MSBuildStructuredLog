# MSBuildStructuredLog
A logger for MSBuild that records a structured representation of executed targets, tasks, property and item values

## Usage: 

```
msbuild solution.sln /v:diag /noconlog /logger:XmlFileLogger,C:\MSBuildStructuredLog\bin\Debug\StructuredLogger.dll;buildlog1.xml
```
