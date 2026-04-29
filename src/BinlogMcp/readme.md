# Binlog MCP

Model Context Protocol to read and analyze MSBuild .binlog files.

Install:

```
dotnet tool update -g binlogmcp
```

Configure your MCP-aware client. For VS Code, add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "binlogmcp": {
      "type": "stdio",
      "command": "binlogmcp"
    }
  }
}
```

Then just ask the LLM to open a binlog and paste the path.
It can call `get_llm_guide` if it needs help.