# BinlogTool Prompt - AI-Powered Build Log Analysis

Use natural language to analyze your MSBuild binlog files powered by AI.

## Quick Start

```bash
# Set environment variables (one-time setup)
export LLM_ENDPOINT="https://your-resource.azure.com/..."
export LLM_MODEL="claude-sonnet-4-5-2"  # or "gpt-4", etc.
export LLM_API_KEY="your-api-key"

# Analyze your build
binlogtool prompt why is this build slow

# Quick answer mode
binlogtool prompt -mode:singleshot count the projects

# Interactive conversation
binlogtool prompt -interactive
```

## Command Syntax

```
binlogtool prompt [options] <prompt-text>
binlogtool prompt -interactive [options]
```

## Options

| Option | Description |
|--------|-------------|
| `-binlog:<path>` | Path to binlog file(s), comma-separated for multiple |
| `--recurse` | Search subdirectories for binlog files |
| `-llm-endpoint:<url>` | Override LLM endpoint (overrides env var) |
| `-llm-model:<model>` | Override model name (overrides env var) |
| `-llm-api-key:<key>` | Override API key (overrides env var) |
| `-mode:<agent\|singleshot>` | Execution mode (default: agent) |
| `-interactive` | Enter interactive REPL mode |
| `-verbose` | Show detailed progress and tool results |
| `-quiet` | Show only final output and errors |
| `-help` | Show help message |

## Modes

### Agent Mode (Default)
Multi-step reasoning with automatic planning, research, and summarization.

**Best for**: Complex questions requiring thorough analysis

**Example**:
```bash
binlogtool prompt why is this build taking so long
```

The agent will:
1. Create a research plan (3-5 tasks)
2. Execute each task using available tools
3. Synthesize findings into comprehensive answer

### Single-Shot Mode
Direct question answering without planning overhead.

**Best for**: Simple, straightforward questions

**Example**:
```bash
binlogtool prompt -mode:singleshot how many projects
binlogtool prompt -mode:singleshot what errors occurred
```

### Interactive Mode
REPL-style conversation with history.

**Best for**: Exploratory analysis, multiple questions

**Example**:
```bash
binlogtool prompt -interactive
> what errors occurred
> why did project X take so long
> /mode singleshot
> count the warnings
> clear
> exit
```

**Interactive Commands**:
- `exit` or `quit` - Leave interactive mode
- `clear` - Clear conversation history
- `/mode agent` - Switch to agent mode
- `/mode singleshot` - Switch to single-shot mode

## Configuration

### Environment Variables

The same variables used by the GUI StructuredLogViewer:

```bash
# Required
export LLM_ENDPOINT="https://your-resource.services.ai.azure.com/..."
export LLM_MODEL="claude-sonnet-4-5-2"
export LLM_API_KEY="your-api-key-here"
```

### Provider Detection

The tool automatically detects the LLM provider based on endpoint and model:

- **Azure OpenAI**: Endpoints containing `cognitiveservices.azure.com` or `openai.azure.com`
- **Anthropic**: Endpoints containing `/anthropic/` or models starting with `claude`
- **Azure AI Inference**: Other Azure endpoints

### Command-Line Overrides

CLI arguments take precedence over environment variables:

```bash
binlogtool prompt -llm-api-key:temp-key what failed
```

## Binlog Discovery

### Auto-Discovery

If no `-binlog` specified, searches for `*.binlog` in current directory:

```bash
cd path/to/build/output
binlogtool prompt what failed
```

Use `--recurse` to search subdirectories:

```bash
binlogtool prompt --recurse find all errors
```

### Explicit Paths

Specify one or more binlog files:

```bash
# Single file
binlogtool prompt -binlog:mybuild.binlog what failed

# Multiple files (CSV)
binlogtool prompt -binlog:build1.binlog,build2.binlog compare these

# Path with spaces (use quotes)
binlogtool prompt -binlog:"C:\My Build\output.binlog" analyze this
```

## Output Verbosity

### Normal (Default)
Shows progress, tool calls, and results:

```
[SYSTEM] Loading binlog: msbuild.binlog
[SYSTEM] LLM configured: claude-sonnet-4-5-2 (Anthropic)
[TOOL] 🔧 Executing: GetBuildSummary
[TOOL] ✓ GetBuildSummary (0.1s)
The build completed in 11.011 seconds...
```

### Verbose (-verbose)
Includes tool arguments and result previews:

```
[VERBOSE] Found 1 binlog file(s):
[VERBOSE]   - C:\path\to\msbuild.binlog
[TOOL] 🔧 Executing: GetProjects
[VERBOSE]    Arguments: maxResults: 50
[TOOL] ✓ GetProjects (0.1s)
[VERBOSE]    Result: Project1.csproj (8.3s)...
```

### Quiet (-quiet)
Only final output and errors (perfect for scripts):

```
The build completed in 11.011 seconds with 0 errors and 0 warnings.
```

## Visual Output Format

Color-coded console output helps distinguish message types:

| Prefix | Color | Purpose |
|--------|-------|---------|
| `[SYSTEM]` | Gray | System/status messages |
| `[TOOL]` | Cyan | Tool execution with timing |
| `[AGENT]` | Yellow | Agent progress with emojis |
| `[RETRY]` | Magenta | Retry/throttling information |
| `[ERROR]` | Red | Error messages |
| `[VERBOSE]` | Dark Gray | Verbose debug info |
| (none) | White/Green | LLM responses |

## Example Prompts

### Build Analysis
```bash
binlogtool prompt why is this build slow
binlogtool prompt what's taking the most time
binlogtool prompt which project built the longest
binlogtool prompt how can I make this faster
```

### Error Investigation
```bash
binlogtool prompt what errors occurred
binlogtool prompt show me compilation errors
binlogtool prompt why did project X fail
binlogtool prompt list all warnings
```

### Build Information
```bash
binlogtool prompt count the projects
binlogtool prompt list all projects
binlogtool prompt show build summary
binlogtool prompt how long did it take
```

### Target Analysis
```bash
binlogtool prompt which targets ran
binlogtool prompt what did the Build target do
binlogtool prompt show me the CoreCompile execution
```

### Advanced Analysis
```bash
binlogtool prompt compare this build to best practices
binlogtool prompt analyze build parallelization
binlogtool prompt find performance bottlenecks
binlogtool prompt what's causing the rebuild
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| -1 | No binlog file found |
| -2 | LLM not configured (missing env vars) |
| -3 | Invalid command-line arguments |
| -4 | LLM execution failed |
| -5 | Cancelled by user (Ctrl+C) |

Perfect for scripting and CI/CD integration!

## Error Handling

### No Binlog Found
```
[ERROR] No binlog files found.
[SYSTEM] Searched in: C:\current\directory
```

**Solution**: Ensure you're in the correct directory or use `-binlog:<path>`

### LLM Not Configured
```
[ERROR] LLM is not configured.
[SYSTEM] Please set these environment variables:
[SYSTEM]   LLM_ENDPOINT - LLM service endpoint URL
[SYSTEM]   LLM_MODEL - Model name
[SYSTEM]   LLM_API_KEY - API key for authentication
```

**Solution**: Set the required environment variables or use CLI overrides

### Invalid Arguments
```
Error: Unknown option: -badarg
Use 'binlogtool prompt -help' for usage information.
```

**Solution**: Check the help for valid options

## Tips & Best Practices

### 1. Choose the Right Mode

- **Agent mode**: Complex questions, thorough analysis
- **Single-shot**: Quick facts, simple queries
- **Interactive**: Exploring, multiple related questions

### 2. Use Verbosity Appropriately

- **Normal**: Day-to-day use
- **Verbose**: Debugging, understanding tool behavior
- **Quiet**: Scripts, automation, piping output

### 3. Binlog Location

Place your terminal in the build output directory for auto-discovery:
```bash
cd path/to/bin/Debug
binlogtool prompt analyze this build
```

### 4. Interactive Exploration

Start interactive mode to ask follow-up questions:
```bash
binlogtool prompt -interactive
> what failed
> why did that target take so long
> show me the compilation warnings
```

### 5. Combine with Other Tools

Quiet mode makes it easy to integrate with other tools:
```bash
# Save analysis to file
binlogtool prompt -quiet what failed > analysis.txt

# Use in CI/CD
if binlogtool prompt -quiet "did the build succeed" | grep -q "succeeded"; then
  echo "Build OK"
fi
```

## Performance

- **Cold start**: 3-5 seconds (loading binlog)
- **Single-shot**: 2-10 seconds (simple query)
- **Agent mode**: 15-60 seconds (comprehensive analysis)
- **Interactive**: Instant response for follow-ups

## Troubleshooting

### Slow Responses

LLM responses depend on model, complexity, and API latency. Agent mode performs multiple tool calls for thorough analysis.

**Solution**: Use single-shot mode for faster answers

### Rate Limiting

If you see throttling messages:
```
[RETRY] ⚠️ Rate limited, retrying in 2s (attempt 1/3)
```

The tool automatically retries with exponential backoff.

### Cancellation

Press Ctrl+C to gracefully cancel:
```
Cancelling...
[SYSTEM] Operation cancelled by user.
```

All resources are properly cleaned up.

## Related Commands

- `binlogtool search` - Text search in binlog
- `binlogtool listtools` - List all tasks
- `binlogtool savefiles` - Extract embedded files
- `binlogtool dumprecords` - Binary structure analysis

See `binlogtool --help` for all commands.

## Support

For issues or questions:
- GitHub: https://github.com/KirillOsenkov/MSBuildStructuredLog
- Documentation: https://msbuildlog.com

---

**Happy analyzing! 🔍✨**
