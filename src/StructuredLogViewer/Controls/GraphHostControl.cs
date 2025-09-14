using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls;

public class GraphHostControl : DockPanel
{
    private readonly string initialSelection;
    
    public GraphHostControl() : this(null)
    {
    }
    
    public GraphHostControl(string initialSelection)
    {
        this.initialSelection = initialSelection;
        Initialize();
    }

    public static double DpiX;
    public static double DpiY;

    static GraphHostControl()
    {
        IntPtr dc = GetDC(IntPtr.Zero);

        if (dc != IntPtr.Zero)
        {
            const int LOGPIXELSX = 88;
            const int LOGPIXELSY = 90;
            DpiX = GetDeviceCaps(dc, LOGPIXELSX) / (double)96;
            DpiY = GetDeviceCaps(dc, LOGPIXELSY) / (double)96;

            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("Gdi32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

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
                
                // Apply initial selection if specified
                if (!string.IsNullOrEmpty(initialSelection))
                {
                    // Delay until loaded
                    Dispatcher.BeginInvoke(() => Locate(initialSelection), DispatcherPriority.Loaded);
                }
            }

            UpdateVisibility();
        }
    }

    /// <summary>
    /// Locates and selects a node in the graph by searching for the specified text.
    /// </summary>
    /// <param name="text">The text to search for in the graph nodes</param>
    public void Locate(string text)
    {
        graphControl?.Locate(text);
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

        var directReferencesOnlyCheck = new CheckBox
        {
            Content = "Direct references only",
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

        var copyImageButton = new Button
        {
            Content = "Copy screenshot",
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

        copyImageButton.Click += (s, e) =>
        {
            CopyImage();
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
        toolbar.Children.Add(directReferencesOnlyCheck);
        toolbar.Children.Add(depthCheckbox);
        toolbar.Children.Add(horizontalCheckbox);
        toolbar.Children.Add(invertedCheckbox);
        toolbar.Children.Add(searchTextBox);
        toolbar.Children.Add(locateButton);
        toolbar.Children.Add(copyImageButton);
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
                GoToSearch?.Invoke(vertex.Value);
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

        directReferencesOnlyCheck.Checked += (s, e) => graphControl.DirectReferencesOnly = true;
        directReferencesOnlyCheck.Unchecked += (s, e) => graphControl.DirectReferencesOnly = false;

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

    private void CopyImage()
    {
        var visual = graphControl.CanvasElement;

        var renderTarget = new RenderTargetBitmap(
            (int)(visual.DesiredSize.Width * DpiX),
            (int)(visual.DesiredSize.Height * DpiY),
            96 * DpiX,
            96 * DpiY,
            PixelFormats.Pbgra32);

        renderTarget.Render(visual);

        Clipboard.SetImage(renderTarget);
    }
}