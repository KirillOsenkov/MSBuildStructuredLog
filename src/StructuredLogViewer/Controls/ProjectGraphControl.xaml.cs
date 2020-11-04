﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

namespace StructuredLogViewer.Controls
{
    public partial class ProjectGraphControl : UserControl
    {
        public ProjectGraphControl()
        {
            InitializeComponent();
        }

        public BuildControl BuildControl { get; set; }

        public void SetGraph(Microsoft.Msagl.Drawing.Graph graph)
        {
            graph.IsVisible = true;

            GraphViewer graphViewer = new GraphViewer();

            graphViewer.BindToPanel(Panel);

            graphViewer.Graph = graph;
        }

        private void TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is Block block)
            {
                BuildControl.SelectItem(block.Node);
            }
        }
    }
}
