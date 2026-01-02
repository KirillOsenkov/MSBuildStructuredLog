using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using StructuredLogViewer.Dialogs;
using StructuredLogger.LLM;
using StructuredLogger.LLM.Clients.GitHub;
using StructuredLogger.LLM.Logging;

namespace StructuredLogViewer.Controls
{
    public partial class LLMConfigurationDialog : Window
    {
        private bool isApiKeyVisible = false;
        private bool shouldPersist = false;

        public string Endpoint { get; private set; }
        public string Model { get; private set; }
        public string ApiKey { get; private set; }
        public bool AutoSendOnEnter { get; private set; }
        public bool AgentMode { get; private set; }
        public LoggingLevel LoggingLevel { get; private set; }
        public System.Collections.Generic.List<string>? AvailableModels { get; private set; }
        public bool ShouldPersist => shouldPersist;

        /// <summary>
        /// Loads LLM configuration from persisted settings.
        /// Returns configuration from SettingsService if available, otherwise from environment variables.
        /// </summary>
        public static LLMConfiguration LoadPersistedConfiguration()
        {
            var config = new LLMConfiguration();
            
            // Try to load from persisted settings first
            var persistedEndpoint = SettingsService.LLMEndpoint;
            if (!string.IsNullOrEmpty(persistedEndpoint))
            {
                config.Endpoint = persistedEndpoint;
                config.ModelName = SettingsService.LLMModel ?? string.Empty;
                
                // Decrypt API key
                var encryptedKey = SettingsService.LLMApiKeyEncrypted;
                config.ApiKey = SettingsService.DecryptString(encryptedKey) ?? string.Empty;
                
                config.AutoSendOnEnter = SettingsService.LLMAutoSendOnEnter;
                config.AgentMode = SettingsService.LLMAgentMode;
                config.LoggingLevel = (LoggingLevel)SettingsService.LLMLoggingLevel;
                
                // Parse available models
                var modelsString = SettingsService.LLMAvailableModels;
                if (!string.IsNullOrEmpty(modelsString))
                {
                    config.AvailableModels = modelsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => m.Trim())
                        .ToList();
                }
                
                config.UpdateType();
                return config;
            }
            
            // Fallback to environment variables
            return LLMConfiguration.LoadFromEnvironment();
        }

        public LLMConfigurationDialog(LLMConfiguration currentConfig)
        {
            InitializeComponent();

            // Pre-populate with current configuration
            if (currentConfig != null)
            {
                endpointTextBox.Text = currentConfig.Endpoint ?? "";
                
                // Restore available models if present
                if (currentConfig.AvailableModels != null && currentConfig.AvailableModels.Count > 0)
                {
                    modelComboBox.Items.Clear();
                    foreach (var model in currentConfig.AvailableModels)
                    {
                        modelComboBox.Items.Add(model);
                    }
                    modelComboBox.IsEditable = false;
                    modelComboBox.SelectedItem = currentConfig.ModelName;
                }
                else
                {
                    // No available models - start with editable textbox
                    modelComboBox.IsEditable = true;
                    modelComboBox.Text = currentConfig.ModelName ?? "";
                }
                
                if (!string.IsNullOrWhiteSpace(currentConfig.ApiKey))
                {
                    apiKeyPasswordBox.Password = currentConfig.ApiKey;
                    apiKeyTextBox.Text = currentConfig.ApiKey;
                }
                
                autoSendOnEnterCheckBox.IsChecked = currentConfig.AutoSendOnEnter;
                agentModeCheckBox.IsChecked = currentConfig.AgentMode;
                loggingLevelComboBox.SelectedIndex = (int)currentConfig.LoggingLevel;
            }
            else
            {
                // Default to editable textbox
                modelComboBox.IsEditable = true;
                loggingLevelComboBox.SelectedIndex = 1; // Default to Normal
            }

            // Focus on first empty field
            Loaded += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(endpointTextBox.Text))
                {
                    endpointTextBox.Focus();
                }
                else if (string.IsNullOrWhiteSpace(modelComboBox.Text))
                {
                    modelComboBox.Focus();
                }
                else
                {
                    apiKeyPasswordBox.Focus();
                }
            };
        }

        private void ToggleApiKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            isApiKeyVisible = !isApiKeyVisible;

            if (isApiKeyVisible)
            {
                // Show plain text
                apiKeyTextBox.Text = apiKeyPasswordBox.Password;
                apiKeyPasswordBox.Visibility = Visibility.Collapsed;
                apiKeyTextBox.Visibility = Visibility.Visible;
                toggleApiKeyButton.Content = "🙈";
            }
            else
            {
                // Show password box
                apiKeyPasswordBox.Password = apiKeyTextBox.Text;
                apiKeyTextBox.Visibility = Visibility.Collapsed;
                apiKeyPasswordBox.Visibility = Visibility.Visible;
                toggleApiKeyButton.Content = "👁";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateAndSaveSettings(false);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateAndSaveSettings(false);
        }

        private void ApplyAndPersistButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateAndSaveSettings(true);
        }

        private void ValidateAndSaveSettings(bool persist)
        {
            // Validate inputs
            Endpoint = endpointTextBox.Text?.Trim();
            Model = modelComboBox.IsEditable ? modelComboBox.Text?.Trim() : (modelComboBox.SelectedItem as string)?.Trim();
            ApiKey = isApiKeyVisible ? apiKeyTextBox.Text?.Trim() : apiKeyPasswordBox.Password?.Trim();
            AutoSendOnEnter = autoSendOnEnterCheckBox.IsChecked ?? true;
            AgentMode = agentModeCheckBox.IsChecked ?? true;
            LoggingLevel = (LoggingLevel)loggingLevelComboBox.SelectedIndex;
            shouldPersist = persist;

            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                MessageBox.Show("Please enter an endpoint URL.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                endpointTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                MessageBox.Show("Please enter or select a model/deployment name.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                modelComboBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                // Check if it's GitHub Copilot - API key is optional for OAuth flow
                var isGitHubCopilot = Endpoint?.Contains("github", StringComparison.OrdinalIgnoreCase) == true ||
                                      Endpoint?.Equals("github-copilot", StringComparison.OrdinalIgnoreCase) == true;
                
                if (!isGitHubCopilot)
                {
                    MessageBox.Show("Please enter an API key.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (isApiKeyVisible)
                    {
                        apiKeyTextBox.Focus();
                    }
                    else
                    {
                        apiKeyPasswordBox.Focus();
                    }

                    return;
                }
            }

            // If persisting, save to SettingsService
            if (persist)
            {
                SettingsService.LLMEndpoint = Endpoint;
                SettingsService.LLMModel = Model;
                SettingsService.LLMApiKeyEncrypted = SettingsService.EncryptString(ApiKey);
                SettingsService.LLMAutoSendOnEnter = AutoSendOnEnter;
                SettingsService.LLMAgentMode = AgentMode;
                SettingsService.LLMLoggingLevel = (int)LoggingLevel;
                
                // Store available models as comma-separated list
                if (AvailableModels != null && AvailableModels.Count > 0)
                {
                    SettingsService.LLMAvailableModels = string.Join(",", AvailableModels);
                }
                else
                {
                    SettingsService.LLMAvailableModels = null;
                }
            }

            DialogResult = true;
        }

        private async void GitHubLoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                githubLoginButton.IsEnabled = false;
                githubLoginButton.Content = "⏳ Authenticating...";

                GitHubDeviceCodeDialog? deviceDialog = null;

                // Create device code callback
                void DeviceCodeCallback(string userCode, string verificationUrl)
                {
                    Dispatcher.Invoke(() =>
                    {
                        deviceDialog = new GitHubDeviceCodeDialog(userCode, verificationUrl);
                        deviceDialog.Owner = this;
                        deviceDialog.Show();
                    });
                }

                // Start authentication
                var authenticator = new GitHubDeviceFlowAuthenticator(DeviceCodeCallback);
                var githubToken = await authenticator.AuthenticateAsync();

                // Close device dialog on success
                if (deviceDialog != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        deviceDialog.CloseWithSuccess();
                    });
                }

                // Set the token in the API key field
                if (isApiKeyVisible)
                {
                    apiKeyTextBox.Text = githubToken;
                }
                else
                {
                    apiKeyPasswordBox.Password = githubToken;
                }

                // Try to fetch models from GitHub Copilot API
                githubLoginButton.Content = "⏳ Loading models...";
                bool modelsLoaded = await TryLoadGitHubCopilotModelsAsync(githubToken);
                
                if (modelsLoaded)
                {
                    githubLoginButton.Content = "✓ Logged In";
                }
                else
                {
                    // If models couldn't be loaded, keep textbox editable
                    modelComboBox.IsEditable = true;
                    if (string.IsNullOrWhiteSpace(modelComboBox.Text))
                    {
                        modelComboBox.Text = "claude-sonnet-4.5";
                    }
                    githubLoginButton.Content = "✓ Logged In";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"GitHub authentication failed:\n\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                githubLoginButton.Content = "🔑 GitHub Login";
                githubLoginButton.IsEnabled = true;
            }
        }

        private async Task<bool> TryLoadGitHubCopilotModelsAsync(string githubToken)
        {
            try
            {
                // Create token provider to get Copilot token
                var tokenProvider = new GitHubCopilotTokenProvider(githubToken);
                var copilotToken = await tokenProvider.GetCopilotTokenAsync();
                
                // Fetch models from API
                using var httpClient = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{copilotToken.BaseUrl}/models");
                
                // Add all required Copilot API headers (same as GitHubCopilotChatClient)
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {copilotToken.Token}");
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("User-Agent", "GitHubCopilotChat/0.35.0");
                request.Headers.TryAddWithoutValidation("Editor-Version", "vscode/1.107.0");
                request.Headers.TryAddWithoutValidation("Editor-Plugin-Version", "copilot-chat/0.35.0");
                request.Headers.TryAddWithoutValidation("Copilot-Integration-Id", "vscode-chat");
                request.Headers.TryAddWithoutValidation("openai-intent", "conversation-panel");
                request.Headers.TryAddWithoutValidation("x-request-id", Guid.NewGuid().ToString());
                request.Headers.TryAddWithoutValidation("X-Initiator", "user");
                
                var response = await httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Models endpoint returned: {response.StatusCode}");
                    return false;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var modelsResponse = System.Text.Json.JsonSerializer.Deserialize<ModelsResponse>(json);
                
                if (modelsResponse?.Data == null || modelsResponse.Data.Count == 0)
                {
                    return false;
                }
                
                // Filter models: only include those enabled by policy and model picker
                var availableModels = modelsResponse.Data
                    .Where(m => m.ModelPickerEnabled && 
                               m.Policy != null && 
                               m.Policy.State == "enabled")
                    .ToList();
                
                if (availableModels.Count == 0)
                {
                    return false;
                }
                
                // Successfully fetched models - populate dropdown
                Dispatcher.Invoke(() =>
                {
                    modelComboBox.Items.Clear();
                    
                    foreach (var model in availableModels)
                    {
                        modelComboBox.Items.Add(model.Id);
                    }
                    
                    // Select default model (prefer Claude Sonnet 4.5)
                    if (modelComboBox.Items.Contains("claude-sonnet-4.5"))
                    {
                        modelComboBox.SelectedItem = "claude-sonnet-4.5";
                    }
                    else if (modelComboBox.Items.Count > 0)
                    {
                        modelComboBox.SelectedIndex = 0;
                    }
                    
                    modelComboBox.IsEditable = false;
                    
                    // Store the available models list for persistence
                    AvailableModels = availableModels.Select(m => m.Id).ToList();
                });
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load GitHub Copilot models: {ex.Message}");
                return false;
            }
        }
    }
}
