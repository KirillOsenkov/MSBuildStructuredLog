using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls;

public class GraphHostControl : DockPanel
{
    public GraphHostControl()
    {
        Initialize();
    }

    public event Action<string> DisplayText;
    public event Action<string> GoToSearch;

    private Digraph graph;
    private GraphControl graphControl;

    public Digraph Graph
    {
        get => graph;
        set
        {
            if (graph == value)
            {
                return;
            }

            if (graph != null)
            {
                graphControl.Digraph = null;
            }

            graph = value;
            if (graph != null)
            {
                graphControl.Digraph = graph;
            }
        }
    }

    private void Initialize()
    {
        var toolbars = new StackPanel();
        var topToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            MinHeight = 26
        };
        var toolbar = new StackPanel()
        {
            Orientation = Orientation.Horizontal,
            MinHeight = 26
        };

        toolbars.Children.Add(topToolbar);
        toolbars.Children.Add(toolbar);

        toolbar.SetResourceReference(Panel.BackgroundProperty, SystemColors.ControlBrushKey);
        topToolbar.SetResourceReference(Panel.BackgroundProperty, SystemColors.ControlBrushKey);
        Children.Add(toolbars);
        SetDock(toolbars, Dock.Top);

        var projectNameTextBlock = new TextBox()
        {
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            Visibility = Visibility.Hidden
        };

        var searchButton = new Button
        {
            Content = "Go to search",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness(),
            Visibility = Visibility.Hidden
        };

        var showTextButton = new Button
        {
            Content = "Show graph text",
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 0, 8, 0),
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness()
        };

        var searchTextBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 200
        };
        var locateButton = new Button
        {
            Content = "Locate on canvas",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness()
        };

        var transitiveReduceCheck = new CheckBox
        {
            Content = "Hide transitive references",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        showTextButton.Click += (s, e) =>
        {
            var text = graph.GetDotText(transitiveReduceCheck.IsChecked == true);
            DisplayText?.Invoke(text);
        };

        toolbar.Children.Add(showTextButton);
        toolbar.Children.Add(transitiveReduceCheck);
        toolbar.Children.Add(searchTextBox);
        toolbar.Children.Add(locateButton);

        topToolbar.Children.Add(searchButton);
        topToolbar.Children.Add(projectNameTextBlock);

        graphControl = new GraphControl();

        graphControl.SelectionChanged += () =>
        {
            var selectedVertex = graphControl.SelectedVertex;
            if (selectedVertex != null)
            {
                projectNameTextBlock.Text = selectedVertex.Value;
                projectNameTextBlock.Visibility = Visibility.Visible;
                searchButton.Visibility = Visibility.Visible;
            }
            else
            {
                projectNameTextBlock.Text = "";
                projectNameTextBlock.Visibility = Visibility.Hidden;
                searchButton.Visibility = Visibility.Hidden;
            }
        };

        locateButton.Click += (s, e) =>
        {
            e.Handled = true;
            Locate();
        };

        searchButton.Click += (s, e) =>
        {
            if (graphControl.SelectedVertex is Vertex vertex)
            {
                GoToSearch?.Invoke($"$projectreference project({vertex.Value})");
            }
        };

        searchTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Return)
            {
                e.Handled = true;
                Locate();
            }
        };

        transitiveReduceCheck.Checked += (s, e) => graphControl.HideTransitiveEdges = true;
        transitiveReduceCheck.Unchecked += (s, e) => graphControl.HideTransitiveEdges = false;

        void Locate()
        {
            var text = searchTextBox.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                graphControl.Locate(text);
            }
        }

        Children.Add(graphControl.Content);
    }
}