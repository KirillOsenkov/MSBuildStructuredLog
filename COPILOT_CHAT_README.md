# Copilot Chat Feature - Setup Guide

## Overview

The MSBuild Structured Log Viewer now includes a Copilot Chat feature that allows you to query and analyze binlog files using AI-powered assistance. The chat uses Azure AI Foundry (or compatible endpoints) to provide intelligent answers about your build logs.

## Features

- **Interactive Chat Interface**: Ask questions about your build in natural language
- **Context-Aware**: Automatically includes information about the currently selected node in the tree
- **Tool Integration**: The AI can execute functions to query the binlog data:
  - `GetBuildSummary()` - Get build status, duration, and error/warning counts
  - `SearchNodes(query)` - Search for specific nodes in the build tree
  - `GetErrorsAndWarnings(type)` - List all errors and/or warnings
  - `GetProjects()` - Get all projects with their build status
  - `GetProjectTargets(projectName)` - Get targets for a specific project

## Setup

### 1. Configure Environment Variables

Before using Copilot Chat, you must set up the following environment variables:

#### Required Variables:

```bash
# Windows (PowerShell)
$env:AZURE_FOUNDRY_ENDPOINT = "https://your-endpoint.azure.com"
$env:AZURE_FOUNDRY_API_KEY = "your-api-key-here"

# Windows (Command Prompt)
set AZURE_FOUNDRY_ENDPOINT=https://your-endpoint.azure.com
set AZURE_FOUNDRY_API_KEY=your-api-key-here

# Linux/macOS
export AZURE_FOUNDRY_ENDPOINT="https://your-endpoint.azure.com"
export AZURE_FOUNDRY_API_KEY="your-api-key-here"
```

#### Optional Variables:

```bash
# Specify the model name (defaults to "gpt-4")
$env:AZURE_FOUNDRY_MODEL_NAME = "gpt-4"
```

### 2. Restart the Application

After setting the environment variables, restart the MSBuild Structured Log Viewer for the changes to take effect.

## Usage

### Opening the Chat Panel

1. Load a binlog file in the viewer
2. Click the **✨ Copilot** button in the top-right corner of the window
3. The chat panel will appear on the right side of the interface

### Asking Questions

Type your question in the input box at the bottom of the chat panel and press Enter or click Send.

#### Example Questions:

- "What errors occurred in this build?"
- "Show me a summary of the build"
- "How long did the build take?"
- "What projects were built?"
- "Search for 'compilation failed'"
- "What warnings are in the CoreLib project?"
- "Show me the targets in MyProject.csproj"

### Using Context

When you select a node in the build tree (left side), that node's context is automatically included in your chat. This helps the AI provide more relevant answers.

For example:
1. Select an error node in the tree
2. Ask "Tell me more about this error"
3. The AI will have context about which error you selected

### Managing the Conversation

- **Send Message**: Press Enter or click the Send button
- **New Line**: Press Shift+Enter to add a line break without sending
- **Clear Chat**: Click the "Clear" button to start a fresh conversation

## Obtaining Azure AI Credentials

### Option 1: Azure AI Foundry

1. Go to [Azure AI Foundry](https://ai.azure.com/)
2. Create a new project or use an existing one
3. Deploy a model (e.g., GPT-4)
4. Get your endpoint URL and API key from the deployment settings

### Option 2: Azure OpenAI Service

1. Create an Azure OpenAI resource in the [Azure Portal](https://portal.azure.com/)
2. Deploy a model
3. Get the endpoint and key from "Keys and Endpoint" section

### Option 3: Compatible Endpoints

The feature uses the Azure AI Inference SDK, which is compatible with:
- Azure AI Foundry
- Azure OpenAI Service
- GitHub Models (future support planned)
- Other OpenAI-compatible endpoints

## Troubleshooting

### "Copilot is not configured" Error

**Cause**: Environment variables are not set or application hasn't been restarted.

**Solution**:
1. Verify environment variables are set correctly
2. Restart the application
3. Check that the endpoint URL is valid and accessible

### Connection Errors

**Cause**: Network issues, invalid credentials, or endpoint unavailable.

**Solution**:
1. Verify your API key is correct
2. Check that the endpoint URL is accessible from your network
3. Ensure you have an active subscription/credits

### Copilot Button Disabled

**Cause**: No build is currently loaded.

**Solution**: Load a binlog file first, then click the Copilot button.

## Privacy and Security

⚠️ **Important**: When using Copilot Chat, build data is sent to the configured AI service endpoint. Be aware that:

1. Build logs may contain sensitive information (paths, environment variables, etc.)
2. Data is sent to your configured Azure endpoint
3. Consider using the "Redact Secrets" feature before analyzing sensitive builds
4. Review your organization's policies regarding AI service usage

## Technical Details

### Architecture

- **Frontend**: WPF control (CopilotChatControl)
- **Backend**: CopilotChatService orchestrates the chat
- **AI Integration**: Microsoft.Extensions.AI with Azure AI Inference client
- **Tool Execution**: BinlogToolExecutor provides functions for the AI to call

### Dependencies

The feature requires the following NuGet packages:
- `Microsoft.Extensions.AI` (v9.0.1-preview.1)
- `Microsoft.Extensions.AI.AzureAIInference` (v9.0.1-preview.1)
- `Azure.AI.Inference` (v1.0.0-beta.2)

## Future Enhancements

Planned improvements include:
- GitHub Copilot service integration
- Chat history persistence across sessions
- Export chat conversations
- More sophisticated binlog analysis tools
- Custom tool definitions
- Voice input support

## Feedback

If you encounter issues or have suggestions, please file an issue on the [GitHub repository](https://github.com/KirillOsenkov/MSBuildStructuredLog/issues).
