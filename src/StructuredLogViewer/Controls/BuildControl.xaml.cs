using System.Windows.Controls;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public BuildControl(Build build)
        {
            InitializeComponent();
            DataContext = build;
            Build = build;
        }

        public Build Build { get; set; }
    }
}
