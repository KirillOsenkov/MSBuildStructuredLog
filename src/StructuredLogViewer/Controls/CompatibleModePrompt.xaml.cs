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
    /// Interaction logic for CompatibleModePrompt.xaml
    /// </summary>
    public partial class CompatibleModePrompt : Window
    {
        public CompatibleModePrompt(string promptText)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(promptText))
            {
                this.PromptText.Text = promptText;
            }
        }

        public bool DoNotAskAgain
        {
            get { return ChckbxDoNotAsk.IsChecked == true; }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            ConfirmButtonClicked(true);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmButtonClicked(false);
        }

        private void ConfirmButtonClicked(bool useForwardCompat)
        {
            SettingsService.UseForwardCompatibility = DoNotAskAgain ? useForwardCompat : null;
            this.DialogResult = useForwardCompat;
        }
    }
}
