using System;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using StructuredLogViewer.LLM;

namespace StructuredLogViewer.Controls
{
    public partial class LLMConfigurationDialog : Window
    {
        private bool isApiKeyVisible = false;

        public string Endpoint { get; private set; }
        public string Model { get; private set; }
        public string ApiKey { get; private set; }

        public LLMConfigurationDialog(LLMConfiguration currentConfig)
        {
            InitializeComponent();

            // Pre-populate with current configuration
            if (currentConfig != null)
            {
                endpointTextBox.Text = currentConfig.Endpoint ?? "";
                modelTextBox.Text = currentConfig.ModelName ?? "";
                
                if (!string.IsNullOrWhiteSpace(currentConfig.ApiKey))
                {
                    apiKeyPasswordBox.Password = currentConfig.ApiKey;
                    apiKeyTextBox.Text = currentConfig.ApiKey;
                }
            }

            // Focus on first empty field
            Loaded += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(endpointTextBox.Text))
                    endpointTextBox.Focus();
                else if (string.IsNullOrWhiteSpace(modelTextBox.Text))
                    modelTextBox.Focus();
                else
                    apiKeyPasswordBox.Focus();
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
            // Validate inputs
            Endpoint = endpointTextBox.Text?.Trim();
            Model = modelTextBox.Text?.Trim();
            ApiKey = isApiKeyVisible ? apiKeyTextBox.Text?.Trim() : apiKeyPasswordBox.Password?.Trim();

            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                MessageBox.Show("Please enter an endpoint URL.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                endpointTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                MessageBox.Show("Please enter a model/deployment name.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                modelTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                MessageBox.Show("Please enter an API key.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                if (isApiKeyVisible)
                    apiKeyTextBox.Focus();
                else
                    apiKeyPasswordBox.Focus();
                return;
            }

            DialogResult = true;
        }
    }
}
