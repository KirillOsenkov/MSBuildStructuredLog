using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace BinlogMcp;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // MCP servers communicate over stdio; route logs to stderr so they
        // don't corrupt the JSON-RPC protocol stream on stdout.
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInstructions = ServerInstructions;
                options.ServerInfo = new()
                {
                    Name = "binlogmcp",
                    Version = Assembly.GetExecutingAssembly()
                        .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0.0",
                    WebsiteUrl = "https://github.com/KirillOsenkov/MSBuildStructuredLog"
                };

                // Strict tool-argument validation: reject any argument key that
                // isn't a declared parameter of the target tool. Without this,
                // typos get silently dropped and the LLM never learns it called
                // the tool wrong. See https://github.com/modelcontextprotocol/csharp-sdk/issues/1508.
                options.Filters.Request.CallToolFilters.Add(next => async (context, ct) =>
                {
                    if (context.MatchedPrimitive is McpServerTool tool &&
                        context.Params?.Arguments is { Count: > 0 } arguments &&
                        tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var props) &&
                        props.ValueKind == JsonValueKind.Object)
                    {
                        var valid = props.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                        var unknown = arguments.Keys.Where(k => !valid.Contains(k)).ToList();
                        if (unknown.Count > 0)
                        {
                            throw new McpException(
                                $"Unknown argument(s) for tool '{tool.ProtocolTool.Name}': " +
                                $"{string.Join(", ", unknown)}. Valid arguments: {string.Join(", ", valid.OrderBy(s => s))}.");
                        }
                    }

                    return await next(context, ct);
                });
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private const string ServerInstructions = """
        MSBuild .binlog navigator. Nodes have stable integer ids printed as [123] or [42/3.7].
        Standard flow: load_binlog → search → get_node / get_ancestors / get_children on returned ids.
        Unsure how to proceed? Call `get_llm_guide` (workflow + recipes + pitfalls) or `get_search_syntax_help` (DSL reference).
        """;
}
