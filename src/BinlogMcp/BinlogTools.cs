using System.ComponentModel;
using ModelContextProtocol.Server;

namespace BinlogMcp
{
    [McpServerToolType]
    public static class BinlogTools
    {
        [McpServerTool(Name = "load_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Loads an MSBuild .binlog file. Must be called before any other tool.")]
        public static string LoadBinlog(
            [Description("Absolute path to a .binlog file")] string path)
        {
            return $"loaded {path}";
        }
    }
}
