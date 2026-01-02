using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace StructuredLogViewer.Dialogs
{
    /// <summary>
    /// Dialog for displaying GitHub OAuth device code authentication flow.
    /// </summary>
    public partial class GitHubDeviceCodeDialog : Window
    {
        private readonly string userCode;
        private readonly string verificationUrl;
        private DispatcherTimer autoCloseTimer;

        public GitHubDeviceCodeDialog(string userCode, string verificationUrl)
        {
            InitializeComponent();

            this.userCode = userCode;
            this.verificationUrl = verificationUrl;

            userCodeText.Text = userCode;
            verificationUrlText.Text = verificationUrl;

            // Auto-close after 5 minutes (device codes typically expire after 15 minutes)
            autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            autoCloseTimer.Tick += (s, e) =>
            {
                autoCloseTimer.Stop();
                DialogResult = false;
                Close();
            };
            autoCloseTimer.Start();

            // Automatically copy code to clipboard on show
            Loaded += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(userCode);
                    statusText.Text = "Code copied to clipboard! Waiting for authentication...";
                }
                catch
                {
                    // Clipboard operations can fail in some environments
                }
            };
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(userCode);
                statusText.Text = "Code copied to clipboard!";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to copy to clipboard: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = verificationUrl,
                    UseShellExecute = true
                });
                statusText.Text = "Browser opened. Please complete authentication.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open browser: {ex.Message}\n\nPlease manually navigate to:\n{verificationUrl}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            autoCloseTimer?.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            autoCloseTimer?.Stop();
        }

        /// <summary>
        /// Updates the status text from an external thread.
        /// </summary>
        public void UpdateStatus(string status)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                statusText.Text = status;
            }));
        }

        /// <summary>
        /// Closes the dialog from an external thread with success.
        /// </summary>
        public void CloseWithSuccess()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                autoCloseTimer?.Stop();
                statusText.Text = "Authentication successful!";

                // Wait a moment to show the success message, then close
                var closeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5)
                };
                closeTimer.Tick += (s, e) =>
                {
                    closeTimer.Stop();
                    Close();
                };
                closeTimer.Start();
            }));
        }

        /// <summary>
        /// Closes the dialog from an external thread with error.
        /// </summary>
        public void CloseWithError(string errorMessage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                autoCloseTimer?.Stop();
                statusText.Text = $"Error: {errorMessage}";
                MessageBox.Show(
                    errorMessage,
                    "Authentication Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Close();
            }));
        }
    }
}
