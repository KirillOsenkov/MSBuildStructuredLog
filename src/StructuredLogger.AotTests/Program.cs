using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using BuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: StructuredLogger.AotTests <binlog-path>");
    return 1;
}

var binlogPath = Path.GetFullPath(args[0]);
if (!File.Exists(binlogPath))
{
    Console.Error.WriteLine($"Binlog not found: {binlogPath}");
    return 2;
}

if (Serialization.CreateNode(nameof(Build)) is not Build)
{
    return 3;
}

var message = new BuildMessageEventArgs("message", "help", "sender", MessageImportance.Normal);
Reflector.SetFile(message, "file.cs");
Reflector.SetLineNumber(message, 42);

if (Reflector.GetMessage(message) != "message")
{
    return 4;
}

var sourceText = new SourceText("<!-- comment --><Project />");
if (!SourceTextXml.TryGetXml(sourceText, out var root) || root.Name != "Project")
{
    return 5;
}

var build = BinaryLog.ReadBuild(binlogPath);
if (!build.Succeeded)
{
    Console.Error.WriteLine($"The build recorded in {binlogPath} did not succeed.");
    return 6;
}

var recordCount = BinaryLog.ReadRecords(binlogPath).Count();
Console.WriteLine($"Read {recordCount} records from {Path.GetFileName(binlogPath)}.");

const int minimumRecordCount = 100;
if (recordCount < minimumRecordCount)
{
    Console.Error.WriteLine($"Expected at least {minimumRecordCount} records but read {recordCount}.");
    return 7;
}

var hasRepresentativeNodes =
    ReportNodeCount<ProjectEvaluation>(build) &
    ReportNodeCount<Project>(build) &
    ReportNodeCount<Target>(build) &
    ReportNodeCount<BuildTask>(build) &
    ReportNodeCount<Property>(build) &
    ReportNodeCount<Item>(build) &
    ReportNodeCount<Message>(build);

if (!hasRepresentativeNodes)
{
    return 8;
}

return 0;

static bool ReportNodeCount<T>(Build build)
    where T : BaseNode
{
    var count = build.FindChildrenRecursive<T>().Count;
    Console.WriteLine($"{typeof(T).Name}: {count}");

    if (count > 0)
    {
        return true;
    }

    Console.Error.WriteLine($"Expected at least one {typeof(T).Name} node.");
    return false;
}
