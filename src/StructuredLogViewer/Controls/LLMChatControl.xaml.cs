using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Win32;
using StructuredLogger.LLM;
using StructuredLogger.LLM.Logging;
using StructuredLogViewer.LLM;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// Template selector for choosing between regular messages and tool call messages.
    /// </summary>
    public class ChatMessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate RegularMessageTemplate { get; set; }
        public DataTemplate ToolCallMessageTemplate { get; set; }
        public DataTemplate QuestionMessageTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessageDisplay message)
            {
                if (message.IsQuestion)
                {
                    return QuestionMessageTemplate;
                }
                return message.IsToolCall ? ToolCallMessageTemplate : RegularMessageTemplate;
            }
            return RegularMessageTemplate;
        }
    }

    /// <summary>
    /// View model for displaying chat messages in the UI
    /// </summary>
    public class ChatMessageDisplay : INotifyPropertyChanged
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public bool IsError { get; set; }

        // Tool call support
        public bool IsToolCall { get; set; }
        public ToolCallViewModel ToolCallData { get; set; }

        // Question/Answer support
        public bool IsQuestion { get; set; }
        public string[] QuestionOptions { get; set; }
        public Action<string> OnAnswerProvided { get; set; }

        public Brush RoleBackground
        {
            get
            {
                if (IsError)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 240, 240));
                }

                if (IsToolCall)
                {
                    return new SolidColorBrush(Color.FromRgb(245, 245, 250));
                }

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
                if (IsError)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 200, 200));
                }

                if (IsToolCall)
                {
                    return new SolidColorBrush(Color.FromRgb(180, 180, 200));
                }

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

    /// <summary>
    /// Simple display model for attached binlog chips in the UI.
    /// </summary>
    public class AttachedBinlogInfo
    {
        public string BuildId { get; set; }
        public string FileName { get; set; }  // Just the filename for display
        public string FullPath { get; set; }  // Full path for tooltip
    }

    /// <summary>
    /// Represents an independent chat session with its own conversation history and LLM context.
    /// </summary>
    public class ChatSession : INotifyPropertyChanged
    {
        private string displayName;

        public string SessionId { get; }
        public ObservableCollection<ChatMessageDisplay> Messages { get; }
        public LLMChatService ChatService { get; set; }
        public AgenticLLMChatService AgenticChatService { get; set; }
        public ChatHistoryService HistoryService { get; set; }

        /// <summary>
        /// Whether the display name has been set from user content (vs. default "Chat N").
        /// </summary>
        public bool HasGeneratedTitle { get; set; }

        public string DisplayName
        {
            get => displayName;
            set
            {
                displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public ChatSession(string sessionId, string name)
        {
            SessionId = sessionId;
            displayName = name;
            Messages = new ObservableCollection<ChatMessageDisplay>();
        }

        /// <summary>
        /// Generates a short title from the first user message.
        /// </summary>
        public void GenerateTitleFromMessage(string userMessage)
        {
            if (HasGeneratedTitle || string.IsNullOrWhiteSpace(userMessage))
            {
                return;
            }

            // Clean up the message: collapse whitespace, trim
            var title = userMessage.Replace('\n', ' ').Replace('\r', ' ').Trim();

            // Truncate to a reasonable length for a dropdown
            const int maxLength = 40;
            if (title.Length > maxLength)
            {
                // Try to break at a word boundary
                var truncated = title.Substring(0, maxLength);
                var lastSpace = truncated.LastIndexOf(' ');
                if (lastSpace > maxLength / 2)
                {
                    truncated = truncated.Substring(0, lastSpace);
                }
                title = truncated + "…";
            }

            DisplayName = title;
            HasGeneratedTitle = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class LLMChatControl : UserControl
    {
        private CancellationTokenSource cancellationTokenSource;
        private LLMConfiguration currentConfig;
        private bool isInitialized;
        private ChatWindowLogger chatLogger;
        private TaskCompletionSource<string> waitingForUserResponse;
        private ChatMessageDisplay currentQuestionMessage;

        // Multi-binlog support
        private MultiBuildContext buildContext;
        private readonly ObservableCollection<AttachedBinlogInfo> attachedBinlogs;

        // Multi-chat session support
        private readonly ObservableCollection<ChatSession> chatSessions;
        private ChatSession activeSession;
        private int nextSessionNumber = 1;
        private bool isSwitchingSession;

        public Build Build { get; private set; }
        public BuildControl BuildControl { get; private set; }

        // Convenience accessors for the active session's services
        private LLMChatService chatService => activeSession?.ChatService;
        private AgenticLLMChatService agenticChatService => activeSession?.AgenticChatService;
        private ChatHistoryService chatHistoryService => activeSession?.HistoryService;
        private ObservableCollection<ChatMessageDisplay> messages => activeSession?.Messages;

        public LLMChatControl()
        {
            InitializeComponent();
            chatSessions = new ObservableCollection<ChatSession>();
            attachedBinlogs = new ObservableCollection<AttachedBinlogInfo>();
            sessionSelector.ItemsSource = chatSessions;
            attachedFilesList.ItemsSource = attachedBinlogs;
        }

        public void Initialize(Build build, BuildControl buildControl)
        {
            if (isInitialized)
            {
                return; // Already initialized
            }

            Build = build ?? throw new ArgumentNullException(nameof(build));
            BuildControl = buildControl;

            // Create multi-build context with the implicit build (from viewer)
            // This is the primary build - additional binlogs can be attached via UI
            buildContext = new MultiBuildContext();
            buildContext.AddBuild(build); // This is the primary/implicit build
            // Note: We don't show the implicit build in attachedBinlogs list
            // Only additional attached binlogs are shown in the UI

            // Create chat window logger
            chatLogger = new ChatWindowLogger((message, isError) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AddMessage(new ChatMessageDisplay
                    {
                        Role = "System",
                        Content = message,
                        IsError = isError
                    });
                });
            }, LoggingLevel.Normal);

            // Load configuration from persisted settings or environment
            currentConfig = LLMConfigurationDialog.LoadPersistedConfiguration();
            chatLogger.Level = currentConfig.LoggingLevel;

            // Discover existing sessions from persisted history, or create a default one
            var binlogPath = build.LogFilePath;
            var existingSessionIds = !string.IsNullOrEmpty(binlogPath)
                ? ChatHistoryService.ListSessions(binlogPath)
                : new System.Collections.Generic.List<string>();

            if (existingSessionIds.Count == 0)
            {
                // Create default first session
                CreateNewSession("Chat 1");
            }
            else
            {
                // Restore existing sessions
                foreach (var sessionId in existingSessionIds)
                {
                    var session = CreateSessionObject(sessionId, sessionId);
                    chatSessions.Add(session);
                    nextSessionNumber = Math.Max(nextSessionNumber, ExtractSessionNumber(sessionId) + 1);
                }
            }

            // Select first session
            isSwitchingSession = true;
            sessionSelector.SelectedIndex = 0;
            isSwitchingSession = false;
            ActivateSession(chatSessions[0]);

            isInitialized = true;

            // Initialize agent mode toggle from config (default is true)
            agentModeToggle.IsChecked = currentConfig.AgentMode;
            UpdateAgentModeUI();

            // Initialize model selector
            PopulateModelSelector();

            // The session's services are created asynchronously in ActivateSession.
            // Welcome/restore messages and status are shown after services are ready.
        }

        private async System.Threading.Tasks.Task CreateLLMServicesAsync(ChatSession session = null)
        {
            if (Build == null || buildContext == null)
            {
                return;
            }

            var targetSession = session ?? activeSession;
            if (targetSession == null)
            {
                return;
            }

            // Dispose old services if they exist
            LLMChatService? oldChatService = targetSession.ChatService;
            AgenticLLMChatService? oldAgenticChatService = targetSession.AgenticChatService;

            // Create new chat service with multi-build context
            var newChatService = await LLMChatService.CreateAsync(buildContext, null, chatLogger);
            newChatService.MessageAdded += OnMessageAdded;
            newChatService.ConversationCleared += OnConversationCleared;
            newChatService.ConversationCompacted += OnConversationCompacted;
            newChatService.ToolCallExecuting += OnToolCallExecuting;
            newChatService.ToolCallExecuted += OnToolCallExecuted;
            newChatService.RequestRetrying += OnRequestRetrying;

            // Register UI interaction tools if BuildControl is available
            // Note: UI tools operate on primary build only (the one open in viewer)
            if (BuildControl != null)
            {
                var uiInteractionExecutor = new BinlogUIInteractionExecutor(Build, BuildControl);
                newChatService.RegisterToolContainer(uiInteractionExecutor);
            }

            // Register AskUser tool if enabled
            if (SettingsService.LLMEnableAskUser)
            {
                var askUserExecutor = new AskUserToolExecutor(new GuiUserInteraction(this));
                newChatService.RegisterToolContainer(askUserExecutor);
            }

            // Reconfigure with current config if available
            if (currentConfig != null)
            {
                await newChatService.ReconfigureAsync(currentConfig);
            }

            targetSession.ChatService = newChatService;

            // Seed in-memory chat history from persisted entries so the LLM has context
            var persistedHistory = targetSession.HistoryService?.Load();
            if (persistedHistory != null && persistedHistory.Count > 0)
            {
                newChatService.SeedChatHistory(persistedHistory);
            }

            // Create agentic service if configured
            if (currentConfig?.IsConfigured == true)
            {
                var newAgenticService = await AgenticLLMChatService.CreateAsync(buildContext, currentConfig, chatLogger);

                // Register UI interaction tools for agentic service
                if (BuildControl != null)
                {
                    var uiInteractionExecutor = new BinlogUIInteractionExecutor(Build, BuildControl);
                    newAgenticService.RegisterToolContainer(uiInteractionExecutor);
                }

                // Register AskUser tool if enabled
                if (SettingsService.LLMEnableAskUser)
                {
                    var askUserExecutor = new AskUserToolExecutor(new GuiUserInteraction(this));
                    newAgenticService.RegisterToolContainer(askUserExecutor);
                }

                newAgenticService.ProgressUpdated += OnAgentProgressUpdated;
                newAgenticService.MessageAdded += OnMessageAdded;
                newAgenticService.ToolCallExecuting += OnToolCallExecuting;
                newAgenticService.ToolCallExecuted += OnToolCallExecuted;
                newAgenticService.RequestRetrying += OnRequestRetrying;

                targetSession.AgenticChatService = newAgenticService;
            }

            _ = System.Threading.Tasks.Task.Delay(1000).ContinueWith(t => { oldChatService?.Dispose(); oldAgenticChatService?.Dispose(); });
        }

        public void SetSelectedNode(BaseNode node)
        {
            chatService?.SetSelectedNode(node);
        }

        private void AddWelcomeMessage()
        {
            var welcomeMsg = "Welcome to LLM Chat! I can help you analyze this MSBuild binlog.\n\n" +
                        "You can ask me about:\n" +
                        "• Build errors and warnings\n" +
                        "• Project and target information\n" +
                        "• Build duration and performance\n" +
                        "• Specific tasks or failures\n\n";

            if (currentConfig?.AgentMode == true)
            {
                welcomeMsg += "**Agent Mode is ON** 🤖\n" +
                             "I'll break down complex questions into research tasks for thorough analysis.\n\n";
            }

            welcomeMsg += "📎 **Multi-Binlog Support**: Use the attachment button to add more binlog files for comparison.\n\n";

            welcomeMsg += "Try asking: \"What errors occurred?\" or \"Show me the build summary\"";

            AddMessage(new ChatMessageDisplay
            {
                Role = "System",
                Content = welcomeMsg
            });
        }

        /// <summary>
        /// Restores chat history from persisted storage, or shows a welcome message if no history exists.
        /// </summary>
        private void RestoreChatHistory()
        {
            var history = chatHistoryService?.Load();
            if (history == null || history.Count == 0)
            {
                AddWelcomeMessage();
                return;
            }

            // Generate title from the first user message in history
            var firstUserMessage = history.FirstOrDefault(h => h.Role == "User");
            if (firstUserMessage != null)
            {
                activeSession?.GenerateTitleFromMessage(firstUserMessage.Content);
            }

            foreach (var entry in history)
            {
                AddMessage(new ChatMessageDisplay
                {
                    Role = entry.Role,
                    Content = entry.Content
                });
            }
        }

        /// <summary>
        /// Saves the current User and Assistant messages to persisted chat history.
        /// </summary>
        private void SaveChatHistory()
        {
            if (chatHistoryService == null)
            {
                return;
            }

            var entries = messages
                .Where(m => m.Role == "User" || m.Role == "Assistant")
                .Select(m => new ChatHistoryEntry
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = DateTime.Now
                })
                .ToList();

            chatHistoryService.Save(entries, activeSession?.DisplayName);
        }

        #region Session Management

        private ChatSession CreateSessionObject(string sessionId, string displayName)
        {
            var session = new ChatSession(sessionId, displayName);
            var binlogPath = Build?.LogFilePath;
            if (!string.IsNullOrEmpty(binlogPath))
            {
                session.HistoryService = new ChatHistoryService(binlogPath, sessionId);

                // Restore persisted display name if available
                var persistedName = session.HistoryService.LoadDisplayName();
                if (!string.IsNullOrEmpty(persistedName))
                {
                    session.DisplayName = persistedName;
                    session.HasGeneratedTitle = true;
                }
            }
            return session;
        }

        private ChatSession CreateNewSession(string displayName = null)
        {
            var sessionId = "Chat " + nextSessionNumber;
            var name = displayName ?? sessionId;
            nextSessionNumber++;

            var session = CreateSessionObject(sessionId, name);
            chatSessions.Add(session);
            return session;
        }

        private void ActivateSession(ChatSession session)
        {
            if (session == null || session == activeSession)
            {
                return;
            }

            // Save current session's history before switching
            SaveChatHistory();

            activeSession = session;

            // Bind the UI to this session's messages
            messagesPanel.ItemsSource = session.Messages;

            // Create LLM services for this session if not yet created
            if (session.ChatService == null)
            {
                // Show loading overlay while services initialize
                ShowLoadingOverlay("Loading chat…");

                _ = CreateLLMServicesAsync(session).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var exception = t.Exception?.GetBaseException();
                        Dispatcher.Invoke(() =>
                        {
                            HideLoadingOverlay();
                            if (exception is UnauthorizedAccessException)
                            {
                                SettingsService.ClearLLMConfiguration();
                                AddMessage(new ChatMessageDisplay
                                {
                                    Role = "System",
                                    Content = $"⚠️ Authentication Error\n\n{exception.Message}\n\n" +
                                             "Your saved credentials have been cleared. Please click 'Configure' to re-authenticate.",
                                    IsError = true
                                });
                            }
                            else
                            {
                                AddMessage(new ChatMessageDisplay
                                {
                                    Role = "System",
                                    Content = $"Failed to initialize LLM services: {exception?.Message}",
                                    IsError = true
                                });
                            }
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // Restore or show welcome after services are ready
                            if (session.Messages.Count == 0)
                            {
                                RestoreChatHistory();
                            }
                            HideLoadingOverlay();
                        });
                    }
                });
            }
            else
            {
                // Services already created, hide loading immediately
                HideLoadingOverlay();
            }

            // Clear agent progress for the newly activated session
            agentProgressPanel.Clear();

            // Update delete button state
            deleteSessionButton.IsEnabled = chatSessions.Count > 1;
        }

        private void SessionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isSwitchingSession || sessionSelector.SelectedItem is not ChatSession selected)
            {
                return;
            }

            ActivateSession(selected);
        }

        private void NewSessionButton_Click(object sender, RoutedEventArgs e)
        {
            var session = CreateNewSession();

            isSwitchingSession = true;
            sessionSelector.SelectedItem = session;
            isSwitchingSession = false;

            ActivateSession(session);

            // Show welcome for new session
            if (chatService?.IsConfigured == true && session.Messages.Count == 0)
            {
                AddWelcomeMessage();
            }
        }

        private void DeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (chatSessions.Count <= 1 || activeSession == null)
            {
                return;
            }

            var sessionToDelete = activeSession;
            var index = chatSessions.IndexOf(sessionToDelete);

            // Delete persisted history
            sessionToDelete.HistoryService?.Delete();

            // Dispose services
            sessionToDelete.ChatService?.Dispose();
            sessionToDelete.AgenticChatService?.Dispose();

            // Remove and select another session
            chatSessions.Remove(sessionToDelete);

            var newIndex = Math.Min(index, chatSessions.Count - 1);
            isSwitchingSession = true;
            sessionSelector.SelectedIndex = newIndex;
            isSwitchingSession = false;
            ActivateSession(chatSessions[newIndex]);

            deleteSessionButton.IsEnabled = chatSessions.Count > 1;
        }

        private static int ExtractSessionNumber(string sessionId)
        {
            // Try to extract number from "Chat N" pattern
            if (sessionId.StartsWith("Chat ") && int.TryParse(sessionId.Substring(5), out var num))
            {
                return num;
            }
            return 0;
        }

        #endregion

        private void OnAgentProgressUpdated(object sender, AgentProgressEventArgs e)
        {
            agentProgressPanel.UpdateProgress(e);
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

        private void OnToolCallExecuting(object sender, ToolCallInfo toolCallInfo)
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Show status banner indicating tool is executing
                ShowStatus($"Executing tool: {toolCallInfo.ToolName} (In Progress...)");

                AddMessage(new ChatMessageDisplay
                {
                    Role = "Tool",
                    IsToolCall = true,
                    ToolCallData = new ToolCallViewModel(toolCallInfo),
                    Content = string.Empty // Not used for tool calls
                });
            });
        }

        private void OnToolCallExecuted(object sender, ToolCallInfo toolCallInfo)
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Clear the status banner since tool execution is complete
                HideStatus();

                // Try to find an existing in-progress message with the same CallId
                var existingMessage = messages.FirstOrDefault(m =>
                    m.IsToolCall &&
                    m.ToolCallData != null &&
                    m.ToolCallData.CallId == toolCallInfo.CallId);

                if (existingMessage != null)
                {
                    // Update the existing message with completion data
                    existingMessage.ToolCallData.UpdateWithCompletion(toolCallInfo);
                }
                else
                {
                    // No existing message found (shouldn't happen, but handle gracefully)
                    AddMessage(new ChatMessageDisplay
                    {
                        Role = "Tool",
                        IsToolCall = true,
                        ToolCallData = new ToolCallViewModel(toolCallInfo),
                        Content = string.Empty
                    });
                }
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

        private void OnConversationCompacted(object sender, string summary)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowStatus("Context compacted — older messages summarized to save tokens.");
                SaveChatHistory();
            });
        }

        private void OnRequestRetrying(object sender, ResilienceEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowStatus($"{e.Message} (attempt {e.Attempt}/{e.MaxAttempts})");
            });
        }

        private void AddMessage(ChatMessageDisplay message)
        {
            if (messages == null)
            {
                return;
            }

            messages.Add(message);

            // Scroll to bottom
            Dispatcher.InvokeAsync(() =>
            {
                chatScrollViewer.ScrollToBottom();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Asks the user for input via the chat interface.
        /// Used by the AskUser tool to clarify ambiguous requirements.
        /// </summary>
        public async Task<string> AskUserForInput(string question, string[]? options = null)
        {
            // Create a completion source to wait for user response
            waitingForUserResponse = new TaskCompletionSource<string>();

            // Display the question in the chat with interactive options
            currentQuestionMessage = new ChatMessageDisplay
            {
                Role = "Assistant",
                Content = $"🤔 **Clarification Needed**\n\n{question}",
                IsQuestion = true,
                QuestionOptions = options,
                OnAnswerProvided = (answer) =>
                {
                    // User provided an answer
                    waitingForUserResponse?.TrySetResult(answer);
                }
            };

            AddMessage(currentQuestionMessage);

            // Enable input for user to respond and show Send button
            inputTextBox.IsEnabled = true;
            sendButton.Visibility = Visibility.Visible;
            cancelButton.Visibility = Visibility.Collapsed;
            inputTextBox.Focus();

            // Wait for the user to respond asynchronously
            string response;
            try
            {
                response = await waitingForUserResponse.Task;

                // Display the user's response in the chat
                AddMessage(new ChatMessageDisplay
                {
                    Role = "User",
                    Content = response
                });

                // Clear the question state
                currentQuestionMessage = null;
                waitingForUserResponse = null;

                // Restore to "in progress" state - disable input and show Cancel button
                inputTextBox.IsEnabled = false;
                sendButton.Visibility = Visibility.Collapsed;
                cancelButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // If user input fails, log and return empty string and restore UI state
                chatLogger?.LogError($"Failed to get user input: {ex.Message}");
                response = string.Empty;
                inputTextBox.IsEnabled = false;
                sendButton.Visibility = Visibility.Collapsed;
                cancelButton.Visibility = Visibility.Visible;
            }

            return response ?? string.Empty;
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

        private void ShowLoadingOverlay(string text = "Loading chat…")
        {
            loadingText.Text = text;
            loadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoadingOverlay()
        {
            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void OptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string option)
            {
                // User clicked an option button
                currentQuestionMessage?.OnAnswerProvided?.Invoke(option);
            }
        }

        private void SetQueryInProgress()
        {
            sendButton.Visibility = Visibility.Collapsed;
            cancelButton.Visibility = Visibility.Visible;
            inputTextBox.IsEnabled = false;
            agentModeToggle.IsEnabled = false;
        }

        private void SetQueryIdle()
        {
            sendButton.Visibility = Visibility.Visible;
            cancelButton.Visibility = Visibility.Collapsed;
            inputTextBox.IsEnabled = true;
            agentModeToggle.IsEnabled = true;
            inputTextBox.Focus();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel the current operation
            cancellationTokenSource?.Cancel();

            // Dismount all events from current services
            if (chatService != null)
            {
                chatService.MessageAdded -= OnMessageAdded;
                chatService.ConversationCleared -= OnConversationCleared;
                chatService.ToolCallExecuting -= OnToolCallExecuting;
                chatService.ToolCallExecuted -= OnToolCallExecuted;
                chatService.RequestRetrying -= OnRequestRetrying;
            }

            if (agenticChatService != null)
            {
                agenticChatService.ProgressUpdated -= OnAgentProgressUpdated;
                agenticChatService.MessageAdded -= OnMessageAdded;
                agenticChatService.ToolCallExecuting -= OnToolCallExecuting;
                agenticChatService.ToolCallExecuted -= OnToolCallExecuted;
                agenticChatService.RequestRetrying -= OnRequestRetrying;
            }

            // Recreate service instances to prevent any late events
            _ = CreateLLMServicesAsync();

            // Update UI state
            SetQueryIdle();

            // Clear agent progress
            agentProgressPanel.Clear();

            ShowStatus("Request cancelled", isError: false);

            // Hide status after 2 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                HideStatus();
                timer.Stop();
            };
            timer.Start();
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Get current configuration to check AutoSendOnEnter setting
            var config = chatService?.GetConfiguration();
            bool autoSendEnabled = config?.AutoSendOnEnter ?? true;

            if (autoSendEnabled)
            {
                // Send on Enter (without Shift)
                if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    e.Handled = true;
                    _ = SendMessageAsync();
                }
            }
            // If autoSend is disabled, Enter will create a new line (default TextBox behavior)
        }

        private async System.Threading.Tasks.Task SendMessageAsync()
        {
            if (chatService == null || !chatService.IsConfigured)
            {
                ShowStatus("LLM is not configured", isError: true);
                return;
            }

            var message = inputTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Clear input
            inputTextBox.Text = string.Empty;

            // Auto-generate session title from the first user message
            activeSession?.GenerateTitleFromMessage(message);

            // Check if we're waiting for a response to a question
            if (waitingForUserResponse != null)
            {
                // User is answering a question
                currentQuestionMessage?.OnAnswerProvided?.Invoke(message);
                return;
            }

            // Cancel any existing operation
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            // Set query in progress and show cancel button
            SetQueryInProgress();
            ShowStatus(currentConfig?.AgentMode == true ? "Agent thinking..." : "Thinking...");

            try
            {
                if (currentConfig?.AgentMode == true && agenticChatService != null)
                {
                    // Use agent mode
                    AddMessage(new ChatMessageDisplay
                    {
                        Role = "User",
                        Content = message
                    });

                    var response = await agenticChatService.ExecuteAgenticWorkflowAsync(
                        message,
                        cancellationTokenSource.Token);

                    AddMessage(new ChatMessageDisplay
                    {
                        Role = "Assistant",
                        Content = response
                    });
                }
                else
                {
                    // Use regular interactive mode
                    await chatService.SendMessageAsync(message, cancellationTokenSource.Token);
                }

                HideStatus();
                SaveChatHistory();
            }
            catch (OperationCanceledException)
            {
                ShowStatus("Request cancelled", isError: false);
                // Hide status after 2 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    HideStatus();
                    timer.Stop();
                };
                timer.Start();
            }
            catch (UnauthorizedAccessException uaEx)
            {
                // GitHub token expired or invalid - clear persisted config and prompt user
                SettingsService.ClearLLMConfiguration();

                AddMessage(new ChatMessageDisplay
                {
                    Role = "System",
                    Content = $"⚠️ Authentication Error\n\n{uaEx.Message}\n\n" +
                             "Your saved credentials have been cleared. Please click 'Configure' to re-authenticate.",
                    IsError = true
                });

                ShowStatus("Authentication failed - please reconfigure", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", isError: true);
            }
            finally
            {
                SetQueryIdle();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel any ongoing operation
            cancellationTokenSource?.Cancel();

            // Update state immediately
            SetQueryIdle();

            // Clear the conversation
            chatService?.ClearConversation();
            messages.Clear();

            // Delete persisted chat history
            chatHistoryService?.Delete();

            // Clear agent progress
            agentProgressPanel.Clear();

            // Add welcome message back
            if (chatService?.IsConfigured == true)
            {
                AddWelcomeMessage();
            }

            HideStatus();
            inputTextBox.Text = string.Empty;
        }

        /// <summary>
        /// Attach additional binlog file(s) for multi-build comparison.
        /// </summary>
        private void AttachBinlog_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Binary Log Files (*.binlog)|*.binlog",
                Title = "Attach Additional Binlog",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filePath in openFileDialog.FileNames)
                {
                    // Skip if already attached (check by full path)
                    if (buildContext?.GetAllBuilds().Any(b =>
                        b.FullPath?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true) == true)
                    {
                        AddMessage(new ChatMessageDisplay
                        {
                            Role = "System",
                            Content = $"'{Path.GetFileName(filePath)}' is already loaded.",
                            IsError = false
                        });
                        continue;
                    }

                    try
                    {
                        var build = BinaryLog.ReadBuild(filePath);
                        var buildId = buildContext.AddBuild(build);

                        // Add to visible list (only additional binlogs shown, not the implicit one)
                        attachedBinlogs.Add(new AttachedBinlogInfo
                        {
                            BuildId = buildId,
                            FileName = Path.GetFileName(filePath),
                            FullPath = filePath
                        });

                        AddMessage(new ChatMessageDisplay
                        {
                            Role = "System",
                            Content = $"📎 Attached: {Path.GetFileName(filePath)} [{buildId}]\n" +
                                     $"You can now compare and analyze multiple builds."
                        });
                    }
                    catch (Exception ex)
                    {
                        AddMessage(new ChatMessageDisplay
                        {
                            Role = "System",
                            Content = $"Failed to load '{Path.GetFileName(filePath)}': {ex.Message}",
                            IsError = true
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Detach/remove an attached binlog.
        /// </summary>
        private void DetachBinlog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string buildId)
            {
                // Remove from context
                try
                {
                    buildContext?.RemoveBuild(buildId);

                    // Remove from UI list
                    var item = attachedBinlogs.FirstOrDefault(b => b.BuildId == buildId);
                    if (item != null)
                    {
                        attachedBinlogs.Remove(item);
                        AddMessage(new ChatMessageDisplay
                        {
                            Role = "System",
                            Content = $"Detached: {item.FileName}"
                        });
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Cannot remove primary build
                    AddMessage(new ChatMessageDisplay
                    {
                        Role = "System",
                        Content = ex.Message,
                        IsError = true
                    });
                }
            }
        }

        private void AgentModeToggle_Click(object sender, RoutedEventArgs e)
        {
            // Update config
            if (currentConfig != null)
            {
                currentConfig.AgentMode = agentModeToggle.IsChecked == true;
            }

            // Update UI
            UpdateAgentModeUI();

            // Add notification message
            var modeMessage = currentConfig?.AgentMode == true
                ? "🤖 **Agent Mode Enabled**\n\nI'll now break down complex questions into research tasks for thorough analysis."
                : "💬 **Interactive Mode**\n\nBack to single-turn conversations.";

            AddMessage(new ChatMessageDisplay
            {
                Role = "System",
                Content = modeMessage
            });
        }

        private void UpdateAgentModeUI()
        {
            var isEnabled = currentConfig?.AgentMode == true;
            agentModeToggle.Content = isEnabled ? "🤖" : "💬";
            agentModeToggle.FontWeight = isEnabled ? FontWeights.Bold : FontWeights.Normal;
            agentModeToggle.ToolTip = isEnabled
                ? "Agent Mode ON: Multi-step reasoning for complex queries (click to disable)"
                : "Interactive Mode: Single-turn conversations (click to enable agent mode)";
        }

        private bool isPopulatingModels;

        private void PopulateModelSelector()
        {
            isPopulatingModels = true;
            try
            {
                modelSelector.Items.Clear();

                var models = currentConfig?.AvailableModels;
                var currentModel = currentConfig?.ModelName ?? "";

                if (models != null && models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        modelSelector.Items.Add(model);
                    }

                    // Ensure current model is in the list
                    if (!string.IsNullOrEmpty(currentModel) && !models.Contains(currentModel))
                    {
                        modelSelector.Items.Insert(0, currentModel);
                    }
                }
                else if (!string.IsNullOrEmpty(currentModel))
                {
                    // No available models list, just show the current one
                    modelSelector.Items.Add(currentModel);
                }

                // Select current model
                if (!string.IsNullOrEmpty(currentModel))
                {
                    modelSelector.SelectedItem = currentModel;
                }
            }
            finally
            {
                isPopulatingModels = false;
            }
        }

        private async void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isPopulatingModels || modelSelector.SelectedItem is not string selectedModel)
            {
                return;
            }

            if (currentConfig == null || selectedModel == currentConfig.ModelName)
            {
                return;
            }

            var oldModel = currentConfig.ModelName;
            currentConfig.ModelName = selectedModel;
            currentConfig.UpdateType();

            try
            {
                // Reconfigure the chat service with the new model
                if (chatService != null)
                {
                    await chatService.ReconfigureAsync(currentConfig);
                }

                // Persist the model change
                SettingsService.LLMModel = selectedModel;

                AddMessage(new ChatMessageDisplay
                {
                    Role = "System",
                    Content = $"Model switched to **{selectedModel}**"
                });
            }
            catch (Exception ex)
            {
                // Revert on failure
                currentConfig.ModelName = oldModel;
                currentConfig.UpdateType();

                isPopulatingModels = true;
                modelSelector.SelectedItem = oldModel;
                isPopulatingModels = false;

                ShowStatus($"Failed to switch model: {ex.Message}", isError: true);
            }
        }

        private void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            // Get current configuration
            var configForDialog = this.currentConfig ?? LLMConfigurationDialog.LoadPersistedConfiguration();
            var wasConfigured = chatService?.IsConfigured ?? false;
            var oldAgentMode = configForDialog.AgentMode;

            // Show configuration dialog
            var dialog = new LLMConfigurationDialog(configForDialog, chatLogger)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Check if configuration actually changed
                    bool endpointChanged = configForDialog.Endpoint != dialog.Endpoint;
                    bool modelChanged = configForDialog.ModelName != dialog.Model;
                    bool apiKeyChanged = configForDialog.ApiKey != dialog.ApiKey;
                    bool autoSendChanged = configForDialog.AutoSendOnEnter != dialog.AutoSendOnEnter;
                    bool agentModeChanged = configForDialog.AgentMode != dialog.AgentMode;
                    bool loggingLevelChanged = configForDialog.LoggingLevel != dialog.LoggingLevel;

                    // Services only need to be recreated if model settings changed
                    bool needsServiceRecreation = endpointChanged || modelChanged || apiKeyChanged;

                    bool hasChanges = needsServiceRecreation || autoSendChanged || agentModeChanged || loggingLevelChanged;

                    if (!hasChanges)
                    {
                        // No changes made
                        return;
                    }

                    // Store old model name for message
                    var oldModel = configForDialog.ModelName;

                    // Create new configuration with user-provided values
                    var newConfig = new LLMConfiguration
                    {
                        Endpoint = dialog.Endpoint,
                        ModelName = dialog.Model,
                        ApiKey = dialog.ApiKey,
                        AutoSendOnEnter = dialog.AutoSendOnEnter,
                        AgentMode = dialog.AgentMode,
                        LoggingLevel = dialog.LoggingLevel
                    };
                    newConfig.UpdateType();

                    // Preserve available models if endpoint hasn't changed, otherwise clear them
                    if (endpointChanged)
                    {
                        newConfig.AvailableModels = null;
                    }
                    else
                    {
                        // Copy from either dialog (if newly fetched) or existing config
                        newConfig.AvailableModels = dialog.AvailableModels ?? configForDialog.AvailableModels;
                    }

                    // Update current config reference
                    currentConfig = newConfig;

                    // Update logger level if it changed
                    if (loggingLevelChanged && chatLogger != null)
                    {
                        chatLogger.Level = dialog.LoggingLevel;
                    }

                    // Only recreate services if model settings actually changed
                    if (needsServiceRecreation)
                    {
                        _ = CreateLLMServicesAsync();
                    }
                    else if (chatService != null)
                    {
                        // Just reconfigure existing service with new settings
                        _ = chatService.ReconfigureAsync(newConfig);
                    }

                    // Update agent mode toggle enablement
                    if (newConfig.IsConfigured)
                    {
                        agentModeToggle.IsEnabled = true;
                    }

                    // Update agent mode toggle UI to match new config
                    agentModeToggle.IsChecked = newConfig.AgentMode;
                    UpdateAgentModeUI();

                    // Update model selector to reflect new config
                    PopulateModelSelector();

                    // Notify about agent mode change if it changed
                    if (agentModeChanged && oldAgentMode != newConfig.AgentMode)
                    {
                        var modeMessage = newConfig.AgentMode
                            ? "🤖 **Agent Mode Enabled**\n\nI'll now break down complex questions into research tasks for thorough analysis."
                            : "💬 **Interactive Mode**\n\nBack to single-turn conversations.";

                        AddMessage(new ChatMessageDisplay
                        {
                            Role = "System",
                            Content = modeMessage
                        });
                    }

                    if (chatService?.IsConfigured == true)
                    {
                        sendButton.IsEnabled = true;

                        // Add configuration change message to chat
                        string changeMessage;
                        if (!wasConfigured)
                        {
                            // Transitioning from unconfigured to configured - show welcome
                            AddWelcomeMessage();
                            changeMessage = $"LLM configured: {newConfig.ModelName}";
                        }
                        else if (modelChanged)
                        {
                            changeMessage = $"Model changed from {oldModel} to {newConfig.ModelName}";
                        }
                        else
                        {
                            changeMessage = "LLM configuration updated";
                        }

                        AddMessage(new ChatMessageDisplay
                        {
                            Role = "System",
                            Content = changeMessage
                        });

                        ShowStatus("Configuration updated successfully!");

                        // Hide status after 2 seconds
                        var timer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(2)
                        };
                        timer.Tick += (s, args) =>
                        {
                            HideStatus();
                            timer.Stop();
                        };
                        timer.Start();
                    }
                    else
                    {
                        ShowStatus("Configuration failed. Please check your settings.", isError: true);
                        sendButton.IsEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    ShowStatus($"Configuration error: {ex.Message}", isError: true);
                    sendButton.IsEnabled = false;
                }
            }
        }
    }
}
