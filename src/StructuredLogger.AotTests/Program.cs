using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;

if (Serialization.CreateNode(nameof(Build)) is not Build)
{
    return 1;
}

var message = new BuildMessageEventArgs("message", "help", "sender", MessageImportance.Normal);
Reflector.SetFile(message, "file.cs");
Reflector.SetLineNumber(message, 42);

if (Reflector.GetMessage(message) != "message")
{
    return 2;
}

var sourceText = new SourceText("<!-- comment --><Project />");
if (!SourceTextXml.TryGetXml(sourceText, out var root) || root.Name != "Project")
{
    return 3;
}

var usesBundledFixture = args.Length == 0;
var binlogPath = usesBundledFixture
    ? Path.Combine(AppContext.BaseDirectory, "TestData", "Sample.binlog")
    : Path.GetFullPath(args[0]);

var build = BinaryLog.ReadBuild(binlogPath);
if (!build.Succeeded)
{
    return 4;
}

var recordCount = BinaryLog.ReadRecords(binlogPath).Count();
Console.WriteLine($"Read {recordCount} records from {Path.GetFileName(binlogPath)}.");

const int expectedFixtureRecordCount = 7;
if (usesBundledFixture && recordCount != expectedFixtureRecordCount)
{
    Console.Error.WriteLine($"Expected {expectedFixtureRecordCount} records but read {recordCount}.");
    return 5;
}

if (recordCount == 0)
{
    return 6;
}

return 0;
