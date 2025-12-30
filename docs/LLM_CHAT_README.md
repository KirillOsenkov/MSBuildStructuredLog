# LLM Chat Feature

The MSBuild Structured Log Viewer now includes an AI-powered LLM Chat feature that allows you to query your build logs using natural language.

## Features

- **Natural Language Queries**: Ask questions about your build in plain English
- **Context-Aware**: Automatically includes the currently selected build node in the chat context
- **Build Traversal Tools**: The AI can search, filter, and analyze your build log using built-in tools:
  - `GetBuildSummary`: Get overall build statistics
  - `SearchNodes`: Search for specific nodes by query text
  - `GetErrorsAndWarnings`: Filter errors or warnings
  - `GetProjects`: List all projects in the build
  - `GetProjectTargets`: Get targets for a specific project

## Setup

### Option 1: Azure OpenAI (Recommended)

If you have an Azure OpenAI Service deployment:

1. Set the following environment variables:
   ```
   AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
   AZURE_OPENAI_API_KEY=your-api-key
   AZURE_OPENAI_DEPLOYMENT=your-deployment-name
   ```

2. Restart the application

### Option 2: Azure AI Foundry / GitHub Models

If you're using Azure AI Foundry or GitHub Models:

1. Set the following environment variables:
   ```
   AZURE_FOUNDRY_ENDPOINT=https://your-endpoint.inference.ai.azure.com
   AZURE_FOUNDRY_API_KEY=your-api-key
   AZURE_FOUNDRY_MODEL_NAME=gpt-4o
   ```

2. Restart the application

## Usage

1. **Open a binlog file** in the MSBuild Structured Log Viewer
2. **Click the LLM button** (✨) in the toolbar
3. **Ask questions** in the chat panel, such as:
   - "What caused the build to fail?"
   - "Show me all the errors in this build"
   - "Which project took the longest to build?"
   - "What targets were executed for the WebAPI project?"
4. **Select nodes** in the tree view to automatically include them as context in your chat

## Architecture

The feature is built using:

- **Microsoft.Extensions.AI**: Provides the `IChatClient` abstraction for AI services
- **Azure.AI.OpenAI**: SDK for Azure OpenAI Service
- **Azure.AI.Inference**: SDK for Azure AI Foundry and GitHub Models
- **AIFunction Tool Calling**: Enables the AI to invoke build analysis tools

The implementation uses a service layer pattern with minimal changes to existing code:
- `LLMConfiguration`: Loads configuration from environment variables
- `AzureFoundryLLMClient`: Creates the appropriate AI client
- `LLMChatService`: Orchestrates chat sessions with tool calling
- `BinlogToolExecutor`: Implements the build analysis tools
- `BinlogContextProvider`: Extracts context from selected build nodes

## Notes

- The Azure AI Inference SDK (`Azure.AI.Inference`) is designed for Azure AI Foundry and GitHub Models
- For Azure OpenAI Service, use the Azure OpenAI SDK (`Azure.AI.OpenAI`)
- The configuration automatically detects which provider you're using based on environment variables
- Azure OpenAI variables take priority if both sets are configured
