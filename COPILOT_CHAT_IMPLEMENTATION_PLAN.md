# Copilot Chat Feature Implementation Plan

## Overview
Add a Copilot Chat feature to MSBuild Structured Log Viewer that allows users to query and interact with binlog data using LLM-based assistance.

## Requirements Analysis
- ✅ Study existing codebase structure
- ✅ Understand the viewer architecture (WPF application)
- ✅ Identify integration points for chat UI
- ✅ Review binlog data model (Build, TreeNode structure)

## Architecture Components

### 1. UI Components
- [ ] Create `CopilotChatControl.xaml` - Main chat panel UI
  - Chat message display area (scrollable)
  - Input text box for user queries
  - Send button
  - Clear/Reset button
  - Status indicator for LLM processing
- [ ] Create Copilot icon button in MainWindow toolbar
  - Position: Top toolbar area near menu
  - Icon: Use standard Copilot icon
  - Click handler to show/hide chat panel
- [ ] Integrate chat panel into BuildControl
  - Add as a right-side dockable panel or new tab
  - Maintain responsive layout

### 2. Backend Services
- [ ] Create `CopilotChatService.cs` - Core chat orchestration
  - Manages chat history
  - Coordinates between UI and LLM client
  - Handles context preparation
- [ ] Create `AzureFoundryLLMClient.cs` - Azure Foundry integration
  - Implement using Microsoft.Extensions.AI IChatClient
  - Read configuration from environment variables:
    - `AZURE_FOUNDRY_ENDPOINT`
    - `AZURE_FOUNDRY_API_KEY`
    - `AZURE_FOUNDRY_MODEL_NAME`
  - Handle API communication and error handling
- [ ] Create `BinlogContextProvider.cs` - Context extraction
  - Get currently selected node in tree
  - Extract relevant properties/items
  - Format context for LLM consumption

### 3. LLM Tools/Functions
- [ ] Create `BinlogToolDefinitions.cs` - Tool definitions for LLM
  - `GetBuildSummary()` - Returns build status, duration, errors count
  - `SearchNodes(query)` - Search nodes by text/pattern
  - `GetNodeDetails(nodeId)` - Get full details of a specific node
  - `GetProjectProperties(projectName)` - Get project properties and items
  - `GetErrorsAndWarnings()` - List all errors and warnings
  - `GetTargetDependencies(targetName)` - Show target dependency graph
- [ ] Create `BinlogToolExecutor.cs` - Execute tools against binlog
  - Implements actual tool logic
  - Returns formatted results to LLM

### 4. NuGet Package Dependencies
- [ ] Add `Microsoft.Extensions.AI` package
- [ ] Add `Microsoft.Extensions.AI.AzureAIInference` or appropriate Azure client package
- [ ] Add `System.Text.Json` (likely already present)

### 5. Configuration & Setup
- [ ] Create `CopilotConfiguration.cs` - Configuration model
  - Parse environment variables
  - Validate configuration
  - Provide defaults/fallbacks
- [ ] Add configuration validation on startup
  - Show warning if env vars not set
  - Disable Copilot button if not configured

### 6. Integration Points
- [ ] Integrate into BuildControl
  - Access to `Build` object
  - Access to selected TreeViewItem
  - Subscribe to selection changes
- [ ] Update MainWindow
  - Add Copilot button to toolbar
  - Handle button click to toggle chat panel
- [ ] Context menu integration (optional)
  - "Ask Copilot about this" on tree nodes

### 7. Testing & Validation
- [ ] Test Azure Foundry connection
- [ ] Test tool invocations
- [ ] Test context passing
- [ ] Verify chat UI responsiveness
- [ ] Handle error scenarios gracefully

## Implementation Order

### Phase 1: Infrastructure (Foundation)
1. Add NuGet packages to StructuredLogViewer.csproj
2. Create configuration classes
3. Create basic IChatClient implementation for Azure Foundry
4. Test basic LLM connectivity

### Phase 2: Core Services
5. Create CopilotChatService with basic chat history
6. Create BinlogContextProvider to extract current node context
7. Create BinlogToolDefinitions (start with 2-3 basic tools)
8. Create BinlogToolExecutor with implementations

### Phase 3: UI Implementation
9. Create CopilotChatControl.xaml and code-behind
10. Add Copilot button to MainWindow toolbar
11. Integrate chat panel into BuildControl layout
12. Wire up event handlers and data flow

### Phase 4: Polish & Testing
13. Test end-to-end flow with real binlog files
14. Add error handling and user feedback
15. Add loading indicators and status messages
16. Document environment variable setup

## File Structure

```
src/StructuredLogViewer/
├── Copilot/
│   ├── CopilotChatService.cs
│   ├── CopilotConfiguration.cs
│   ├── AzureFoundryLLMClient.cs
│   ├── BinlogContextProvider.cs
│   ├── BinlogToolDefinitions.cs
│   └── BinlogToolExecutor.cs
├── Controls/
│   ├── CopilotChatControl.xaml
│   ├── CopilotChatControl.xaml.cs
│   ├── BuildControl.xaml (modified)
│   └── BuildControl.xaml.cs (modified)
├── MainWindow.xaml (modified - add button)
└── MainWindow.xaml.cs (modified - wire handlers)
```

## Environment Variables Configuration

Users will need to set:
```
AZURE_FOUNDRY_ENDPOINT=https://your-endpoint.azure.com
AZURE_FOUNDRY_API_KEY=your-api-key
AZURE_FOUNDRY_MODEL_NAME=gpt-4
```

## Future Enhancements (Not in this phase)
- GitHub Copilot service integration
- Chat history persistence
- More sophisticated tools
- Export chat to markdown
- Voice input
- Multi-turn conversation improvements

## Success Criteria
- ✅ Copilot button visible in UI
- ✅ Chat panel opens and closes properly
- ✅ Can send messages to Azure Foundry LLM
- ✅ LLM receives current node context
- ✅ At least 3 tools are callable by LLM
- ✅ Chat history displays correctly
- ✅ Error handling works gracefully
- ✅ User can have functional conversation about binlog
