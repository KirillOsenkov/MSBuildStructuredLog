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

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
