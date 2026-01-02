using System;
using System.Collections.Generic;
using System.Linq;
using StructuredLogger.LLM;

namespace BinlogTool
{
    /// <summary>
    /// Configuration for the prompt command, parsed from CLI arguments and environment.
    /// </summary>
    public class PromptConfiguration
    {
        public List<string> BinlogPaths { get; set; } = new List<string>();
        public bool Recurse { get; set; }
        public string Endpoint { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public bool AgentMode { get; set; } = true; // Default to agent mode
        public bool Interactive { get; set; }
        public CliLogger.Verbosity Verbosity { get; set; } = CliLogger.Verbosity.Normal;
        public string PromptText { get; set; }

        public static (PromptConfiguration config, string errorMessage) Parse(string[] args)
        {
            var config = new PromptConfiguration();
            var promptParts = new List<string>();
            bool parsingOptions = true;

            // Skip "prompt" command itself
            var argsToProcess = args.Skip(1).ToArray();

            foreach (var arg in argsToProcess)
            {
                if (parsingOptions && arg.StartsWith("-"))
                {
                    // Parse option
                    if (arg.StartsWith("-binlog:", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = arg.Substring("-binlog:".Length);
                        // Remove surrounding quotes if present
                        if (value.StartsWith("\"") && value.EndsWith("\""))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            return (null, "Invalid -binlog option: value is required");
                        }
                        config.BinlogPaths.Add(value);
                    }
                    else if (arg.Equals("--recurse", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Recurse = true;
                    }
                    else if (arg.StartsWith("-llm-endpoint:", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Endpoint = arg.Substring("-llm-endpoint:".Length);
                    }
                    else if (arg.StartsWith("-llm-model:", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Model = arg.Substring("-llm-model:".Length);
                    }
                    else if (arg.StartsWith("-llm-api-key:", StringComparison.OrdinalIgnoreCase))
                    {
                        config.ApiKey = arg.Substring("-llm-api-key:".Length);
                    }
                    else if (arg.StartsWith("-mode:", StringComparison.OrdinalIgnoreCase))
                    {
                        var mode = arg.Substring("-mode:".Length);
                        if (mode.Equals("agent", StringComparison.OrdinalIgnoreCase))
                        {
                            config.AgentMode = true;
                        }
                        else if (mode.Equals("singleshot", StringComparison.OrdinalIgnoreCase))
                        {
                            config.AgentMode = false;
                        }
                        else
                        {
                            return (null, $"Invalid mode: {mode}. Use 'agent' or 'singleshot'");
                        }
                    }
                    else if (arg.Equals("-interactive", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Interactive = true;
                    }
                    else if (arg.Equals("-verbose", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Verbosity = CliLogger.Verbosity.Verbose;
                    }
                    else if (arg.Equals("-quiet", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Verbosity = CliLogger.Verbosity.Quiet;
                    }
                    else if (arg.Equals("-help", StringComparison.OrdinalIgnoreCase) || 
                             arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                    {
                        return (null, null); // Signal to show help
                    }
                    else
                    {
                        return (null, $"Unknown option: {arg}");
                    }
                }
                else
                {
                    // First non-option argument starts the prompt
                    parsingOptions = false;
                    promptParts.Add(arg);
                }
            }

            config.PromptText = string.Join(" ", promptParts);

            // Validate: interactive mode doesn't require prompt, but non-interactive does (unless showing help)
            if (!config.Interactive && string.IsNullOrWhiteSpace(config.PromptText))
            {
                return (null, "Prompt text is required (or use -interactive mode)");
            }

            return (config, null);
        }

        public LLMConfiguration ToLLMConfiguration()
        {
            // Start with environment variables
            var llmConfig = LLMConfiguration.LoadFromEnvironment();

            // Override with CLI arguments if provided
            if (!string.IsNullOrWhiteSpace(Endpoint))
            {
                llmConfig.Endpoint = Endpoint;
            }
            if (!string.IsNullOrWhiteSpace(Model))
            {
                llmConfig.ModelName = Model;
            }
            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                llmConfig.ApiKey = ApiKey;
            }

            llmConfig.AgentMode = AgentMode;
            llmConfig.UpdateType();

            return llmConfig;
        }

        public static void ShowHelp()
        {
            Console.WriteLine(@"
BinlogTool Prompt - Analyze MSBuild binlogs using LLM

Usage:
  binlogtool prompt [options] <prompt-text>
  binlogtool prompt -interactive [options]

Options:
  -binlog:<path>              Path to binlog file(s), comma-separated
                              If omitted, searches for *.binlog in current directory
  --recurse                   Search subdirectories for binlog files
  -llm-endpoint:<url>         LLM endpoint URL (overrides LLM_ENDPOINT env var)
  -llm-model:<model>          LLM model name (overrides LLM_MODEL env var)
  -llm-api-key:<key>          LLM API key (overrides LLM_API_KEY env var)
  -mode:<agent|singleshot>    Execution mode (default: agent)
  -interactive                Enter interactive REPL mode
  -verbose                    Show detailed progress and tool results
  -quiet                      Show only final output and errors
  -help                       Show this help message

Environment Variables:
  LLM_ENDPOINT                LLM service endpoint URL
  LLM_MODEL                   Model name (e.g., claude-sonnet-4-5-2, gpt-4)
  LLM_API_KEY                 API key for authentication

Examples:
  binlogtool prompt why is this build slow
  binlogtool prompt -mode:singleshot count the projects
  binlogtool prompt -binlog:custom.binlog what errors occurred
  binlogtool prompt -interactive
  binlogtool prompt -verbose -llm-api-key:abc analyze the build

Notes:
  - All options must come before the prompt text
  - Prompt text can contain spaces and special characters
  - Agent mode breaks down complex queries into research tasks
  - SingleShot mode gives direct answers without planning
");
        }
    }
}
