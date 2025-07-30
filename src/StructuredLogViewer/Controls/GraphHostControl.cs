using System;
using System.Diagnostics;
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
    private Button searchButton;
    private Button showTextButton;

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

            UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        var textVisibility = DisplayText != null ? Visibility.Visible : Visibility.Collapsed;
        var searchVisibility =
            GoToSearch != null &&
            graphControl != null &&
            graphControl.SelectedVertex != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        showTextButton.Visibility = textVisibility;
        searchButton.Visibility = searchVisibility;
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

        searchButton = new Button
        {
            Content = "Go to search",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness(),
            Visibility = Visibility.Hidden
        };

        showTextButton = new Button
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

        var helpButton = new Button
        {
            Content = "Help",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness()
        };

        var depthCheckbox = new CheckBox
        {
            Content = "Layer by depth",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var horizontalCheckbox = new CheckBox
        {
            Content = "Horizontal",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var invertedCheckbox = new CheckBox
        {
            Content = "Invert",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        helpButton.Click += (s, e) =>
        {
            Process.Start(new ProcessStartInfo("https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/Graph") { UseShellExecute = true });
        };

        showTextButton.Click += (s, e) =>
        {
            var text = graph.GetDotText(transitiveReduceCheck.IsChecked == true);
            DisplayText?.Invoke(text);
        };

        depthCheckbox.Checked += (s, e) =>
        {
            graphControl.LayerByDepth = true;
        };
        depthCheckbox.Unchecked += (s, e) =>
        {
            graphControl.LayerByDepth = false;
        };

        horizontalCheckbox.Checked += (s, e) =>
        {
            graphControl.Horizontal = true;
        };
        horizontalCheckbox.Unchecked += (s, e) =>
        {
            graphControl.Horizontal = false;
        };

        invertedCheckbox.Checked += (s, e) =>
        {
            graphControl.Inverted = true;
        };
        invertedCheckbox.Unchecked += (s, e) =>
        {
            graphControl.Inverted = false;
        };

        toolbar.Children.Add(showTextButton);
        toolbar.Children.Add(transitiveReduceCheck);
        toolbar.Children.Add(depthCheckbox);
        toolbar.Children.Add(horizontalCheckbox);
        toolbar.Children.Add(invertedCheckbox);
        toolbar.Children.Add(searchTextBox);
        toolbar.Children.Add(locateButton);
        toolbar.Children.Add(helpButton);

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
                searchButton.Visibility = GoToSearch != null ? Visibility.Visible : Visibility.Collapsed;
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