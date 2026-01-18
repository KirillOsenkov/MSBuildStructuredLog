using System;
using System.Threading.Tasks;
using System.Windows;
using StructuredLogger.LLM;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// GUI-based implementation of IUserInteraction for StructuredLogViewer.
    /// Uses the chat panel to display questions and collect responses from the user.
    /// </summary>
    public class GuiUserInteraction : IUserInteraction
    {
        private readonly LLMChatControl chatControl;

        public GuiUserInteraction(LLMChatControl chatControl)
        {
            this.chatControl = chatControl ?? throw new ArgumentNullException(nameof(chatControl));
        }

        public async Task<string> AskUser(string question, string[]? options = null)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                throw new ArgumentException("Question cannot be null or empty", nameof(question));
            }

            // Must be invoked on UI thread
            if (chatControl.Dispatcher.CheckAccess())
            {
                return await chatControl.AskUserForInput(question, options);
            }
            else
            {
                return await chatControl.Dispatcher.InvokeAsync(async () =>
                {
                    return await chatControl.AskUserForInput(question, options);
                }).Task.Unwrap();
            }
        }
    }
}
