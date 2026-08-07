using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class RedactInputControl : Window
    {
        private readonly Func<Task<string>> getSaveAsDestination;

        private readonly CheckBox chckbxEmbeddedFiles;
        private readonly CheckBox chckbxDistinguishReplacements;
        private readonly CheckBox chckbxCommonCredentials;
        private readonly CheckBox chckbxUsername;
        private readonly CheckBox chckbxCustomSecrets;
        private readonly TextBox txtSecrets;

        public string DestinationFile { get; private set; }
        public bool RedactCommonCredentials => chckbxCommonCredentials.IsChecked == true;
        public bool RedactUsername => chckbxUsername.IsChecked == true;
        public bool RedactEmbeddedFiles => chckbxEmbeddedFiles.IsChecked == true;
        public bool DistinguishSecretsReplacements => chckbxDistinguishReplacements.IsChecked == true;
        public string SecretsBlock => chckbxCustomSecrets.IsChecked == true ? txtSecrets.Text : null;

        public RedactInputControl(Func<Task<string>> getSaveAsDestination)
        {
            this.getSaveAsDestination = getSaveAsDestination;

            Title = "Redact Binlog";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontSize = 12;

            chckbxEmbeddedFiles = new CheckBox { Content = "Process embedded files", Margin = new Thickness(0, 5), IsChecked = true };
            chckbxDistinguishReplacements = new CheckBox { Content = "Use distinct replacements per secret type", Margin = new Thickness(0, 5), IsChecked = true };
            chckbxCommonCredentials = new CheckBox { Content = "Autodetect and redact common credentials", Margin = new Thickness(0, 5), IsChecked = true };
            chckbxUsername = new CheckBox { Content = "Autodetect and redact username", Margin = new Thickness(0, 5), IsChecked = true };
            chckbxCustomSecrets = new CheckBox { Content = "Redact following newline-separated explicit strings", Margin = new Thickness(0, 5), IsChecked = false };

            txtSecrets = new TextBox
            {
                Margin = new Thickness(0, 5),
                IsEnabled = false,
                MinHeight = 200,
                MinWidth = 400,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };

            chckbxCustomSecrets.IsCheckedChanged += (s, e) => txtSecrets.IsEnabled = chckbxCustomSecrets.IsChecked == true;

            var checkboxes = new StackPanel();
            checkboxes.Children.Add(chckbxEmbeddedFiles);
            checkboxes.Children.Add(chckbxDistinguishReplacements);
            checkboxes.Children.Add(chckbxCommonCredentials);
            checkboxes.Children.Add(chckbxUsername);
            checkboxes.Children.Add(chckbxCustomSecrets);

            var contentPanel = new DockPanel { Margin = new Thickness(10) };
            DockPanel.SetDock(checkboxes, global::Avalonia.Controls.Dock.Top);
            contentPanel.Children.Add(checkboxes);
            contentPanel.Children.Add(txtSecrets);

            var redactInPlaceButton = new Button { Content = "_Redact In Place", MinWidth = 75, MinHeight = 23, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(15, 2), IsDefault = true };
            var saveAsButton = new Button { Content = "_Save As", MinWidth = 75, MinHeight = 23, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(15, 2) };
            var cancelButton = new Button { Content = "_Cancel", MinWidth = 75, MinHeight = 23, Padding = new Thickness(15, 2), IsCancel = true };

            redactInPlaceButton.Click += (s, e) => Close(true);
            saveAsButton.Click += async (s, e) =>
            {
                var destination = await this.getSaveAsDestination();
                if (destination != null)
                {
                    DestinationFile = destination;
                    Close(true);
                }
            };
            cancelButton.Click += (s, e) => Close(false);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            buttonPanel.Children.Add(redactInPlaceButton);
            buttonPanel.Children.Add(saveAsButton);
            buttonPanel.Children.Add(cancelButton);

            var warning = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10),
                Child = new TextBlock
                {
                    Text = "Binlog redaction is an experimental feature. Please be cautious and use at your own risk. " +
                        "Avoid sharing binlogs unless absolutely sure they don't contain any secrets. " +
                        "This tool only redacts some secrets on a best effort basis.",
                    Foreground = Brushes.Gray,
                    Width = 600,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto,Auto")
            };
            Grid.SetRow(contentPanel, 0);
            Grid.SetRow(buttonPanel, 1);
            Grid.SetRow(warning, 2);
            grid.Children.Add(contentPanel);
            grid.Children.Add(buttonPanel);
            grid.Children.Add(warning);

            Content = grid;
        }
    }
}
