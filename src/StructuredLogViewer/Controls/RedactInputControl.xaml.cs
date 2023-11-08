using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// Interaction logic for RedactInputControl.xaml
    /// </summary>
    public partial class RedactInputControl : Window
    {
        private readonly Func<string> _getSaveAsDestination;
        public string DestinationFile { get; private set; }
        public bool RedactCommonCredentials { get; private set; } = true;
        public bool RedactUsername { get; set; } = true;
        public bool RedactEmbeddedFiles { get; set; } = true;
        public bool DistinguishSecretsReplacements { get; set; } = true;
        public string SecretsBlock
        {
            get { return ChckbxCustomSecrets.IsChecked == true ? TxtSecrets.Text : null; }
        }

        public RedactInputControl(Func<string> getSaveAsDestination)
        {
            _getSaveAsDestination = getSaveAsDestination;
            InitializeComponent();
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void btnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var destination = _getSaveAsDestination();
            if (destination != null)
            {
                this.DestinationFile = destination;
                this.DialogResult = true;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            ChckbxUsername.IsChecked = RedactUsername;
            ChckbxCommonCredentials.IsChecked = RedactCommonCredentials;
            ChckbxCustomSecrets.IsChecked = false;
            TxtSecrets.IsEnabled = false;
            ChckbxEmbeddedFiles.IsChecked = RedactEmbeddedFiles;
            ChckbxDistinguishReplacements.IsChecked = DistinguishSecretsReplacements;

            TxtSecrets.SelectAll();
            TxtSecrets.Focus();
        }

        private void ChckbxCustomSecrets_OnChanged(object sender, RoutedEventArgs e)
        {
            TxtSecrets.IsEnabled = ChckbxCustomSecrets.IsChecked == true;
        }

        private void ChckbxUsername_OnChanged(object sender, RoutedEventArgs e)
        {
            RedactUsername = ChckbxUsername.IsChecked == true;
        }

        private void ChckbxCommonCredentials_OnChanged(object sender, RoutedEventArgs e)
        {
            RedactCommonCredentials = ChckbxCommonCredentials.IsChecked == true;
        }

        private void ChckbxEmbeddedFiles_OnChanged(object sender, RoutedEventArgs e)
        {
            RedactEmbeddedFiles = ChckbxEmbeddedFiles.IsChecked == true;
        }

        private void ChckbxDistinguishReplacements_OnChanged(object sender, RoutedEventArgs e)
        {
            DistinguishSecretsReplacements = ChckbxDistinguishReplacements.IsChecked == true;
        }
    }
}
