using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using BinlogTool;

var root = new RootCommand("binlogtool - utilities for MSBuild binary logs");

root.SetAction(parseResult =>
{
    // No command specified: show help.
    root.Parse("--help").Invoke();
    return 1;
});
root.Subcommands.Add(CreateListTools());
root.Subcommands.Add(CreateSaveFiles());
root.Subcommands.Add(CreateReconstruct());
root.Subcommands.Add(CreateListNuget());
root.Subcommands.Add(CreateListProperties());
root.Subcommands.Add(CreateCompilerInvocations());
root.Subcommands.Add(CreateDoubleWrites());
root.Subcommands.Add(CreateSaveStrings());
root.Subcommands.Add(CreateSearch());
root.Subcommands.Add(CreateRedact());
root.Subcommands.Add(CreateDumpRecords());
root.Subcommands.Add(CreateStats());

return root.Parse(args).Invoke();

// binlogtool listtools input.binlog
Command CreateListTools()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var command = new Command("listtools", "List the tools invoked in the binlog.") { binlog };
    command.SetAction(parseResult =>
    {
        new ListTools().Run(parseResult.GetValue(binlog));
        return 0;
    });
    return command;
}

// binlogtool savefiles input.binlog output_path
Command CreateSaveFiles()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var outputRoot = new Argument<string>("output_path") { Description = "Directory to write the files to." };
    var command = new Command("savefiles", "Save all files embedded in the binlog to disk.") { binlog, outputRoot };
    command.SetAction(parseResult =>
    {
        new SaveFiles(args).Run(parseResult.GetValue(binlog), parseResult.GetValue(outputRoot));
        return 0;
    });
    return command;
}

// binlogtool reconstruct input.binlog output_path
Command CreateReconstruct()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var outputRoot = new Argument<string>("output_path") { Description = "Directory to reconstruct the sources into." };
    var command = new Command("reconstruct", "Reconstruct the source tree from the binlog.") { binlog, outputRoot };
    command.SetAction(parseResult =>
    {
        new SaveFiles(args).Run(parseResult.GetValue(binlog), parseResult.GetValue(outputRoot), reconstruct: true);
        return 0;
    });
    return command;
}

// binlogtool listnuget input.binlog [output_path]
Command CreateListNuget()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var outputFile = new Argument<string>("output_path")
    {
        Description = "Optional file to write the results to.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var command = new Command("listnuget", "List the NuGet packages referenced in the binlog.") { binlog, outputFile };
    command.SetAction(parseResult =>
    {
        var input = parseResult.GetValue(binlog);
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Binlog file {input} not found");
            return -6;
        }

        return new ListNuget(args).Run(input, parseResult.GetValue(outputFile));
    });
    return command;
}

// binlogtool listproperties input.binlog
Command CreateListProperties()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var command = new Command("listproperties", "List the properties set during the build.") { binlog };
    command.SetAction(parseResult =>
    {
        new ListProperties().Run(parseResult.GetValue(binlog));
        return 0;
    });
    return command;
}

// binlogtool compilerinvocations input.binlog [output_path]
Command CreateCompilerInvocations()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var outputFile = new Argument<string>("output_path")
    {
        Description = "Optional file to write the results to.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var command = new Command("compilerinvocations", "List the compiler invocations in the binlog.") { binlog, outputFile };
    command.SetAction(parseResult =>
    {
        var input = parseResult.GetValue(binlog);
        if (File.Exists(input))
        {
            new CompilerInvocations().Run(input, parseResult.GetValue(outputFile));
        }

        return 0;
    });
    return command;
}

// binlogtool doublewrites input.binlog [output_path]
Command CreateDoubleWrites()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var outputFile = new Argument<string>("output_path")
    {
        Description = "Optional file to write the results to.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var command = new Command("doublewrites", "Report files written more than once during the build.") { binlog, outputFile };
    command.SetAction(parseResult =>
    {
        var input = parseResult.GetValue(binlog);
        if (File.Exists(input))
        {
            new DoubleWrites().Run(input, parseResult.GetValue(outputFile));
        }

        return 0;
    });
    return command;
}

// binlogtool savestrings input.binlog output.txt
Command CreateSaveStrings()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to read." };
    var outputFile = new Argument<string>("output.txt") { Description = "File to write the strings to." };
    var command = new Command("savestrings", "Save the string table from the binlog.") { binlog, outputFile };
    command.SetAction(parseResult =>
    {
        new SaveStrings().Run(parseResult.GetValue(binlog), parseResult.GetValue(outputFile));
        return 0;
    });
    return command;
}

// binlogtool search *.binlog search string
Command CreateSearch()
{
    var binlogs = new Argument<string>("*.binlog") { Description = "Binlog file or glob pattern to search." };
    var searchTerms = new Argument<string[]>("search string")
    {
        Description = "Search query (remaining arguments are joined with spaces).",
        Arity = ArgumentArity.OneOrMore
    };
    var command = new Command("search", "Search one or more binlogs.") { binlogs, searchTerms };
    command.SetAction(parseResult =>
    {
        var search = string.Join(" ", parseResult.GetValue(searchTerms) ?? Array.Empty<string>());
        new Searcher().Search2(parseResult.GetValue(binlogs), search);
        return 0;
    });
    return command;
}

// binlogtool redact --input:path --recurse --in-place -p:secret
Command CreateRedact()
{
    var input = new Option<List<string>>("--input")
    {
        Description = "Binlog file or directory to redact. Defaults to the current working directory.",
        AllowMultipleArgumentsPerToken = false
    };
    var tokens = new Option<List<string>>("-p")
    {
        Description = "Secret to redact. Can be specified multiple times.",
        AllowMultipleArgumentsPerToken = false
    };
    var recurse = new Option<bool>("--recurse") { Description = "Recurse into subdirectories." };
    var inPlace = new Option<bool>("--in-place") { Description = "Overwrite the input logs instead of writing new ones with a suffix." };

    var command = new Command("redact", "Redact secrets from binlogs.") { input, tokens, recurse, inPlace };
    command.SetAction(parseResult =>
    {
        Redact.Run(
            parseResult.GetValue(input) ?? new List<string>(),
            parseResult.GetValue(tokens) ?? new List<string>(),
            parseResult.GetValue(inPlace),
            parseResult.GetValue(recurse));
        return 0;
    });
    return command;
}

// binlogtool stats input.binlog [--html [report.html]] [--text [report.txt]] [--strings]
Command CreateStats()
{
    var binlog = new Argument<string>("input.binlog") { Description = "Path to the binlog to analyze." };
    var html = new Option<string>("--html")
    {
        Description = "Write a standalone HTML report with charts and drill-down. Optional path, defaults to <input>.stats.html.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var text = new Option<string>("--text")
    {
        Description = "Write an LLM-friendly plain text report. Optional path, defaults to <input>.stats.txt.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var strings = new Option<bool>("--strings") { Description = "Include the largest strings from the string table in the report (uses more memory)." };

    var command = new Command("stats", "Break down what takes up space in a binlog. Prints text to stdout unless --html/--text are specified.")
    {
        binlog, html, text, strings
    };
    command.SetAction(parseResult =>
    {
        var input = parseResult.GetValue(binlog);
        return new Stats().Run(
            input,
            GetOutputPath(parseResult, html, input, ".stats.html"),
            GetOutputPath(parseResult, text, input, ".stats.txt"),
            parseResult.GetValue(strings));
    });
    return command;

    static string GetOutputPath(ParseResult parseResult, Option<string> option, string input, string suffix)
    {
        if (parseResult.GetResult(option) is null)
        {
            return null;
        }

        var value = parseResult.GetValue(option);
        return !string.IsNullOrEmpty(value) ? value : input + suffix;
    }
}

// binlogtool dumprecords [[--input:]path] [--include-total] [--include-rollup] [--exclude-details]
Command CreateDumpRecords()
{
    var input = new Option<List<string>>("--input")
    {
        Description = "Binlog file or directory. Defaults to the current working directory.",
        AllowMultipleArgumentsPerToken = false
    };
    var positionalInput = new Argument<List<string>>("path")
    {
        Description = "Binlog path(s), can also be given without the --input switch.",
        Arity = ArgumentArity.ZeroOrMore
    };
    var includeTotal = new Option<bool>("--include-total") { Description = "Include the total record count." };
    var includeRollup = new Option<bool>("--include-rollup") { Description = "Include the per-type rollup." };
    var excludeDetails = new Option<bool>("--exclude-details") { Description = "Exclude the detailed overview." };

    var command = new Command("dumprecords", "Dump the records contained in binlogs.")
    {
        input, positionalInput, includeTotal, includeRollup, excludeDetails
    };
    command.SetAction(parseResult =>
    {
        var inputPaths = new List<string>(parseResult.GetValue(input) ?? new List<string>());
        inputPaths.AddRange(parseResult.GetValue(positionalInput) ?? new List<string>());

        DumpRecords.Run(
            inputPaths,
            parseResult.GetValue(includeTotal),
            parseResult.GetValue(includeRollup),
            !parseResult.GetValue(excludeDetails));
        return 0;
    });
    return command;
}
