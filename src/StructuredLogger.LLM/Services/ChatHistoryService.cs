using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Persists chat history per binlog file and session under the user's local app data folder.
    /// History files are stored as JSON, keyed by a hash of the binlog file path and session ID.
    /// </summary>
    public class ChatHistoryService
    {
        private static readonly string ChatHistoryFolder = Path.Combine(
            GetRootPath(), "ChatHistory");

        private readonly string historyFilePath;
        private readonly string binlogFileKey;

        /// <summary>
        /// The default session ID used when no explicit session is specified.
        /// </summary>
        public const string DefaultSessionId = "default";

        public string SessionId { get; }

        public ChatHistoryService(string binlogFilePath, string sessionId = null)
        {
            if (string.IsNullOrEmpty(binlogFilePath))
            {
                throw new ArgumentNullException(nameof(binlogFilePath));
            }

            SessionId = string.IsNullOrEmpty(sessionId) ? DefaultSessionId : sessionId;
            binlogFileKey = ComputeFileKey(binlogFilePath);
            historyFilePath = GetSessionFilePath(binlogFileKey, SessionId);
        }

        /// <summary>
        /// Saves the list of chat messages to disk.
        /// Only User and Assistant messages should be passed in.
        /// </summary>
        public void Save(IEnumerable<ChatHistoryEntry> messages, string displayName = null)
        {
            try
            {
                Directory.CreateDirectory(ChatHistoryFolder);

                var data = new ChatHistoryData
                {
                    DisplayName = displayName,
                    Messages = messages.ToList()
                };

                var json = JsonSerializer.Serialize(data, ChatHistoryJsonContext.Default.ChatHistoryData);
                File.WriteAllText(historyFilePath, json);
            }
            catch
            {
                // Silently fail - history is non-critical
            }
        }

        /// <summary>
        /// Loads previously saved chat messages for the bound binlog file and session.
        /// Returns an empty list if no history exists.
        /// </summary>
        public List<ChatHistoryEntry> Load()
        {
            try
            {
                if (!File.Exists(historyFilePath))
                {
                    return new List<ChatHistoryEntry>();
                }

                var json = File.ReadAllText(historyFilePath);
                var data = JsonSerializer.Deserialize(json, ChatHistoryJsonContext.Default.ChatHistoryData);
                return data?.Messages ?? new List<ChatHistoryEntry>();
            }
            catch
            {
                return new List<ChatHistoryEntry>();
            }
        }

        /// <summary>
        /// Loads the persisted display name for this session.
        /// Returns null if no history or no name is saved.
        /// </summary>
        public string LoadDisplayName()
        {
            try
            {
                if (!File.Exists(historyFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(historyFilePath);
                var data = JsonSerializer.Deserialize(json, ChatHistoryJsonContext.Default.ChatHistoryData);
                return data?.DisplayName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes the history file for the bound binlog file and session.
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(historyFilePath))
                {
                    File.Delete(historyFilePath);
                }
            }
            catch
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Lists all session IDs that have persisted history for the given binlog file.
        /// </summary>
        public static List<string> ListSessions(string binlogFilePath)
        {
            var sessions = new List<string>();
            try
            {
                if (!Directory.Exists(ChatHistoryFolder))
                {
                    return sessions;
                }

                var key = ComputeFileKey(binlogFilePath);
                var prefix = key + "_";
                var files = Directory.GetFiles(ChatHistoryFolder, prefix + "*.json");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.StartsWith(prefix))
                    {
                        var sessionId = fileName.Substring(prefix.Length);
                        if (!string.IsNullOrEmpty(sessionId))
                        {
                            sessions.Add(sessionId);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail
            }

            return sessions;
        }

        private static string GetSessionFilePath(string binlogKey, string sessionId)
        {
            return Path.Combine(ChatHistoryFolder, binlogKey + "_" + sessionId + ".json");
        }

        private static string ComputeFileKey(string binlogFilePath)
        {
            var normalized = Path.GetFullPath(binlogFilePath).ToLowerInvariant();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                // Use first 16 bytes (32 hex chars) for a short but collision-resistant key
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static string GetRootPath()
        {
            if (Environment.GetEnvironmentVariable("MSBUILDSTRUCTUREDLOG_DATA_DIR") is string dataDir
                && !string.IsNullOrEmpty(dataDir))
            {
                return Path.GetFullPath(dataDir);
            }

            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, "Microsoft", "MSBuildStructuredLog");
            return path;
        }
    }

    /// <summary>
    /// A single entry in persisted chat history.
    /// </summary>
    public class ChatHistoryEntry
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Root object for the persisted chat history JSON file.
    /// </summary>
    public class ChatHistoryData
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("messages")]
        public List<ChatHistoryEntry> Messages { get; set; } = new List<ChatHistoryEntry>();
    }

    /// <summary>
    /// Source-generated JSON serialization context for AOT compatibility.
    /// </summary>
    [JsonSerializable(typeof(ChatHistoryData))]
    [JsonSerializable(typeof(ChatHistoryEntry))]
    [JsonSerializable(typeof(List<ChatHistoryEntry>))]
    internal partial class ChatHistoryJsonContext : JsonSerializerContext
    {
    }
}
