using System;
using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Server;

namespace BinlogMcp;

// Onboarding surface for LLM clients: a single tool the model pulls on demand.
// A short pointer to it is also injected into the system prompt via
// McpServerOptions.ServerInstructions in Program.cs.
[McpServerToolType]
public static class BinlogHelp
{
    private const string LlmGuideResourceName = "LlmGuide.md";

    private static string llmGuideText;
    private static string LlmGuideText
    {
        get
        {
            if (llmGuideText != null)
            {
                return llmGuideText;
            }

            var assembly = typeof(BinlogHelp).Assembly;
            using var stream = assembly.GetManifestResourceStream(LlmGuideResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{LlmGuideResourceName}' not found in {assembly.FullName}.");
            using var reader = new StreamReader(stream);
            llmGuideText = reader.ReadToEnd();
            return llmGuideText;
        }
    }

    [McpServerTool(Name = "get_llm_guide", ReadOnly = true, Idempotent = true)]
    [Description(@"Returns the BinlogMcp field manual for LLMs: workflow, navigation primitives, the search DSL cheat sheet, ready-made recipes (cold-start triage, why-was-this-copied, incremental-build investigation, slowest-tasks, etc.), and common pitfalls.

Call this once at the start of a session — or whenever you're unsure how to approach a binlog — before issuing other tool calls. Pair with get_search_syntax_help for the full search DSL reference.")]
    public static string GetLlmGuide() => LlmGuideText;
}
