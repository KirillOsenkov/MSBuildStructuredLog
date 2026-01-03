using System;
using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// Dialog for collecting user responses when the LLM needs clarification.
    /// </summary>
    public partial class UserResponseDialog : Window
    {
        public string UserResponse { get; private set; }

        public UserResponseDialog(string question, string[]? options = null)
        {
            InitializeComponent();

            // Set the question in the response textbox
            responseTextBox.Text = question;
            responseTextBox.SelectAll();

            // If options are provided, show them in the list
            if (options != null && options.Length > 0)
            {
                optionsListBox.Visibility = Visibility.Visible;
                for (int i = 0; i < options.Length; i++)
                {
                    optionsListBox.Items.Add($"{i + 1}. {options[i]}");
                }
            }

            // Focus on the textbox
            Loaded += (s, e) => responseTextBox.Focus();
        }

        private void OptionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (optionsListBox.SelectedItem != null)
            {
                var selectedText = optionsListBox.SelectedItem.ToString();
                // Remove the number prefix (e.g., "1. ")
                var dotIndex = selectedText.IndexOf(". ");
                if (dotIndex > 0)
                {
                    selectedText = selectedText.Substring(dotIndex + 2);
                }
                responseTextBox.Text = selectedText;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UserResponse = responseTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(UserResponse))
            {
                MessageBox.Show("Please provide a response.", "Response Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}
