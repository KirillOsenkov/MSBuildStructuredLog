using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
