using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Copilot;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// View model for displaying chat messages in the UI
    /// </summary>
    public class ChatMessageDisplay : INotifyPropertyChanged
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public bool IsError { get; set; }

        public Brush RoleBackground
        {
            get
            {
                if (IsError) return new SolidColorBrush(Color.FromRgb(255, 240, 240));
                return Role switch
                {
                    "User" => new SolidColorBrush(Color.FromRgb(230, 240, 255)),
                    "Assistant" => new SolidColorBrush(Color.FromRgb(240, 255, 240)),
                    _ => new SolidColorBrush(Color.FromRgb(250, 250, 250))
                };
            }
        }

        public Brush RoleBorder
        {
            get
            {
                if (IsError) return new SolidColorBrush(Color.FromRgb(255, 200, 200));
                return Role switch
                {
                    "User" => new SolidColorBrush(Color.FromRgb(180, 200, 255)),
                    "Assistant" => new SolidColorBrush(Color.FromRgb(180, 255, 180)),
                    _ => new SolidColorBrush(Color.FromRgb(200, 200, 200))
                };
            }
        }

        public Brush RoleForeground => new SolidColorBrush(Color.FromRgb(60, 60, 60));
        public Brush ContentForeground => IsError ? 
            new SolidColorBrush(Color.FromRgb(180, 0, 0)) : 
            new SolidColorBrush(Color.FromRgb(40, 40, 40));

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class CopilotChatControl : UserControl
    {
        private CopilotChatService chatService;
        private CancellationTokenSource cancellationTokenSource;
        private readonly ObservableCollection<ChatMessageDisplay> messages;

        public Build Build { get; private set; }

        public CopilotChatControl()
        {
            InitializeComponent();
            messages = new ObservableCollection<ChatMessageDisplay>();
            messagesPanel.ItemsSource = messages;
        }

        public void Initialize(Build build)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            
            // Dispose old service if exists
            chatService?.Dispose();
            
            // Create new chat service
            chatService = new CopilotChatService(build);
            chatService.MessageAdded += OnMessageAdded;
            chatService.ConversationCleared += OnConversationCleared;

            // Show initial status
            ShowStatus(chatService.ConfigurationStatus);

            // Add welcome message
            if (chatService.IsConfigured)
            {
                AddWelcomeMessage();
            }
            else
            {
                AddMessage(new ChatMessageDisplay
                {
                    Role = "System",
                    Content = "Copilot is not configured. Set one of these options:\n\n" +
                            "Option 1 - Azure OpenAI (recommended):\n" +
                            "• AZURE_OPENAI_ENDPOINT (e.g., https://your-resource.openai.azure.com)\n" +
                            "• AZURE_OPENAI_API_KEY\n" +
                            "• AZURE_OPENAI_DEPLOYMENT (your deployment name, e.g., gpt-4)\n\n" +
                            "Option 2 - Azure AI Foundry:\n" +
                            "• AZURE_FOUNDRY_ENDPOINT\n" +
                            "• AZURE_FOUNDRY_API_KEY\n" +
                            "• AZURE_FOUNDRY_MODEL_NAME\n\n" +
                            "Restart the application after setting these variables.",
                    IsError = true
                });
                sendButton.IsEnabled = false;
            }
        }

        public void SetSelectedNode(BaseNode node)
        {
            chatService?.SetSelectedNode(node);
        }

        private void AddWelcomeMessage()
        {
            AddMessage(new ChatMessageDisplay
            {
                Role = "System",
                Content = "Welcome to Copilot Chat! I can help you analyze this MSBuild binlog.\n\n" +
                        "You can ask me about:\n" +
                        "• Build errors and warnings\n" +
                        "• Project and target information\n" +
                        "• Build duration and performance\n" +
                        "• Specific tasks or failures\n\n" +
                        "Try asking: \"What errors occurred?\" or \"Show me the build summary\""
            });
        }

        private void OnMessageAdded(object sender, ChatMessageViewModel e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                AddMessage(new ChatMessageDisplay
                {
                    Role = e.Role,
                    Content = e.Content,
                    IsError = e.IsError
                });
            });
        }

        private void OnConversationCleared(object sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                messages.Clear();
                AddWelcomeMessage();
            });
        }

        private void AddMessage(ChatMessageDisplay message)
        {
            messages.Add(message);
            
            // Scroll to bottom
            Dispatcher.InvokeAsync(() =>
            {
                chatScrollViewer.ScrollToBottom();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ShowStatus(string status, bool isError = false)
        {
            statusText.Text = status;
            statusBar.Visibility = Visibility.Visible;
            
            if (isError)
            {
                statusBar.Background = new SolidColorBrush(Color.FromRgb(255, 240, 240));
            }
            else
            {
                statusBar.Background = (Brush)FindResource("Theme_InfoBarBackground");
            }
        }

        private void HideStatus()
        {
            statusBar.Visibility = Visibility.Collapsed;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Send on Enter (without Shift)
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                _ = SendMessageAsync();
            }
        }

        private async System.Threading.Tasks.Task SendMessageAsync()
        {
            if (chatService == null || !chatService.IsConfigured)
            {
                ShowStatus("Copilot is not configured", isError: true);
                return;
            }

            var message = inputTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Clear input
            inputTextBox.Text = string.Empty;

            // Disable send button during processing
            sendButton.IsEnabled = false;
            inputTextBox.IsEnabled = false;
            ShowStatus("Thinking...");

            // Cancel any existing operation
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await chatService.SendMessageAsync(message, cancellationTokenSource.Token);
                HideStatus();
            }
            catch (OperationCanceledException)
            {
                ShowStatus("Request cancelled", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", isError: true);
            }
            finally
            {
                sendButton.IsEnabled = true;
                inputTextBox.IsEnabled = true;
                inputTextBox.Focus();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel any ongoing operation
            cancellationTokenSource?.Cancel();
            
            // Clear the conversation
            chatService?.ClearConversation();
            messages.Clear();
            
            // Add welcome message back
            if (chatService?.IsConfigured == true)
            {
                AddWelcomeMessage();
            }
            
            HideStatus();
            inputTextBox.Text = string.Empty;
            inputTextBox.Focus();
        }
    }
}
