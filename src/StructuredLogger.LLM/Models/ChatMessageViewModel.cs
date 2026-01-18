using System;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Represents a chat message in the conversation.
    /// Used by chat services to represent messages exchanged between user and LLM.
    /// </summary>
    public class ChatMessageViewModel
    {
        public string Role { get; set; } // "User", "Assistant", "System", "Agent"
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsError { get; set; }

        public ChatMessageViewModel(string role, string content, bool isError = false)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
            IsError = isError;
        }
    }
}
