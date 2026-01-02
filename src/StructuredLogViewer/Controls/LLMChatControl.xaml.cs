using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;
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

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessageDisplay message)
            {
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

    public partial class LLMChatControl : UserControl
    {
        private LLMChatService chatService;
        private AgenticLLMChatService agenticChatService;
        private CancellationTokenSource cancellationTokenSource;
        private readonly ObservableCollection<ChatMessageDisplay> messages;
        private LLMConfiguration currentConfig;
        private bool isInitialized;
        private ChatWindowLogger chatLogger;

        public Build Build { get; private set; }
        public BuildControl BuildControl { get; private set; }

        public LLMChatControl()
        {
            InitializeComponent();
            messages = new ObservableCollection<ChatMessageDisplay>();
            messagesPanel.ItemsSource = messages;
        }

        public void Initialize(Build build, BuildControl buildControl)
        {
            if (isInitialized)
            {
                return; // Already initialized
            }

            Build = build ?? throw new ArgumentNullException(nameof(build));
            BuildControl = buildControl;
            
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
            
            // Load configuration
            currentConfig = LLMConfiguration.LoadFromEnvironment();
            chatLogger.Level = currentConfig.LoggingLevel;
            
            // Create and configure LLM services
            CreateLLMServices();
            
            isInitialized = true;

            // Initialize agent mode toggle from config (default is true)
            agentModeToggle.IsChecked = currentConfig.AgentMode;
            UpdateAgentModeUI();

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
                    Content = "LLM is not configured. Set these environment variables:\n\n" +
                            "• LLM_ENDPOINT (e.g., https://your-resource.openai.azure.com/)\n" +
                            "• LLM_API_KEY (your API key)\n" +
                            "• LLM_MODEL (e.g., gpt-4, claude-sonnet-4-5-2)\n\n" +
                            "Or use 'Configure' menu to configure/login.",
                    IsError = true
                });
                sendButton.IsEnabled = false;
                agentModeToggle.IsEnabled = false;
            }
        }

        private void CreateLLMServices()
        {
            if (Build == null)
            {
                return;
            }

            // Dispose old services if they exist
            LLMChatService? oldChatService = chatService;
            AgenticLLMChatService? oldAgenticChatService = agenticChatService;
            
            // Create new chat service with logger
            chatService = new LLMChatService(Build, null, chatLogger);
            chatService.MessageAdded += OnMessageAdded;
            chatService.ConversationCleared += OnConversationCleared;
            chatService.ToolCallExecuting += OnToolCallExecuting;
            chatService.ToolCallExecuted += OnToolCallExecuted;
            chatService.RequestRetrying += OnRequestRetrying;

            // Register UI interaction tools if BuildControl is available
            if (BuildControl != null)
            {
                var uiInteractionExecutor = new BinlogUIInteractionExecutor(Build, BuildControl);
                chatService.RegisterToolContainer(uiInteractionExecutor);
            }

            // Reconfigure with current config if available
            if (currentConfig != null)
            {
                chatService.Reconfigure(currentConfig);
            }
            
            // Create agentic service if configured
            if (currentConfig?.IsConfigured == true)
            {
                agenticChatService = new AgenticLLMChatService(Build, currentConfig, chatLogger);
                
                // Register UI interaction tools for agentic service
                if (BuildControl != null)
                {
                    var uiInteractionExecutor = new BinlogUIInteractionExecutor(Build, BuildControl);
                    agenticChatService.RegisterToolContainer(uiInteractionExecutor);
                }
                
                agenticChatService.ProgressUpdated += OnAgentProgressUpdated;
                agenticChatService.MessageAdded += OnMessageAdded;
                agenticChatService.ToolCallExecuting += OnToolCallExecuting;
                agenticChatService.ToolCallExecuted += OnToolCallExecuted;
                agenticChatService.RequestRetrying += OnRequestRetrying;

                // Initialize services asynchronously (needed for GitHub Copilot device flow)
                _ = InitializeServicesAsync();
            }

            System.Threading.Tasks.Task.Delay(1000).ContinueWith(t => { oldChatService?.Dispose(); oldAgenticChatService?.Dispose(); });
        }

        private async System.Threading.Tasks.Task InitializeServicesAsync()
        {
            try
            {
                if (chatService != null)
                {
                    await chatService.InitializeAsync();
                }

                if (agenticChatService != null)
                {
                    await agenticChatService.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    AddMessage(new ChatMessageDisplay
                    {
                        Role = "System",
                        Content = $"Failed to initialize LLM services: {ex.Message}",
                        IsError = true
                    });
                });
            }
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
            
            welcomeMsg += "Try asking: \"What errors occurred?\" or \"Show me the build summary\"";

            AddMessage(new ChatMessageDisplay
            {
                Role = "System",
                Content = welcomeMsg
            });
        }

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

        private void OnRequestRetrying(object sender, ResilienceEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowStatus($"{e.Message} (attempt {e.Attempt}/{e.MaxAttempts})");
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
            CreateLLMServices();
            
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

        private void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            // Get current configuration
            var configForDialog = this.currentConfig ?? LLMConfiguration.LoadFromEnvironment();
            var wasConfigured = chatService?.IsConfigured ?? false;
            var oldAgentMode = configForDialog.AgentMode;
            
            // Show configuration dialog
            var dialog = new LLMConfigurationDialog(configForDialog)
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
                        CreateLLMServices();
                    }
                    else if (chatService != null)
                    {
                        // Just reconfigure existing service with new settings
                        chatService.Reconfigure(newConfig);
                    }
                    
                    // Update agent mode toggle enablement
                    if (newConfig.IsConfigured)
                    {
                        agentModeToggle.IsEnabled = true;
                    }
                    
                    // Update agent mode toggle UI to match new config
                    agentModeToggle.IsChecked = newConfig.AgentMode;
                    UpdateAgentModeUI();
                    
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
