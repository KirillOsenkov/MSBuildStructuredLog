using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls;

public class GraphControl
{
    private ScrollViewer scrollViewer;
    private Grid grid;
    private StackPanel layersControl;
    private Canvas canvas;

    private Dictionary<Vertex, FrameworkElement> controlFromVertex = new();
    private HashSet<FrameworkElement> selectedControls = new();

    private Color outgoingColor, incomingColor, border;
    private Brush outgoingBrush, incomingBrush;

    public event Action SelectionChanged;

    public GraphControl()
    {
        DarkTheme = SettingsService.UseDarkTheme;

        layersControl = new StackPanel { Orientation = Orientation.Vertical };

        canvas = new Canvas();

        grid = new Grid();
        grid.Children.Add(canvas);
        grid.Children.Add(layersControl);

        scrollViewer = new ScrollViewer()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        scrollViewer.Content = grid;
    }

    private bool darkTheme;
    public bool DarkTheme
    {
        get => darkTheme;
        set
        {
            darkTheme = value;

            if (DarkTheme)
            {
                outgoingColor = Colors.MediumOrchid;
                border = Colors.DeepSkyBlue;
                incomingColor = Colors.PaleGreen;
            }
            else
            {
                outgoingColor = Colors.MediumOrchid;
                border = Colors.DarkCyan;
                incomingColor = Colors.Green;
            }

            outgoingBrush = new LinearGradientBrush(outgoingColor, border, 90.0);
            incomingBrush = new LinearGradientBrush(border, incomingColor, 90.0);
        }
    }

    private Digraph graph;
    public Digraph Digraph
    {
        get => graph;
        set
        {
            if (graph != null)
            {
                Clear();
            }

            graph = value;

            if (graph != null)
            {
                Populate();
            }
        }
    }

    private void Populate()
    {
        var maxHeight = graph.Vertices.Max(g => g.Height);
        var maxDepth = graph.Vertices.Max(g => g.Depth);

        foreach (var vertexGroup in graph.Vertices.GroupBy(v => v.Height).OrderBy(g => g.Key))
        {
            var layerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var vertex in vertexGroup.OrderByDescending(s => s.InDegree).ThenBy(s => s.Title))
            {
                var depth = vertex.Depth;

                var background = ComputeBackground(depth);

                var paddingHeight = Math.Pow(vertex.InDegree, 0.6);
                var opacity = vertex.InDegree > 1 ? 0.9 : 0.5;
                var vertexControl = new TextBlock()
                {
                    Text = vertex.Title.TrimQuotes(),
                    Margin = new Thickness(4, 2, 4, 2),
                    Padding = new Thickness(2, paddingHeight, 2, paddingHeight),
                    Background = new SolidColorBrush(background),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = opacity,
                    Tag = vertex
                };
                controlFromVertex[vertex] = vertexControl;

                vertexControl.MouseDown += (s, e) =>
                {
                    SelectControl(vertexControl);
                };
                layerPanel.Children.Add(vertexControl);
            }

            layersControl.Children.Add(layerPanel);
        }
    }

    private Color ComputeBackground(int depth)
    {
        byte ratio, halfratio;

        Color background;

        if (DarkTheme)
        {
            ratio = (byte)Math.Min(150, depth * 6);
            halfratio = (byte)Math.Min(100, depth * 4);
            background = Color.FromRgb(40, halfratio, ratio);
        }
        else
        {
            ratio = (byte)Math.Max(200, 255 - depth * 2);
            halfratio = (byte)Math.Max(224, 255 - depth);
            background = Color.FromRgb(ratio, halfratio, 255);
        }

        return background;
    }

    Rect GetRectOnCanvas(FrameworkElement control)
    {
        return new Rect(
            control.TranslatePoint(new Point(0, 0), canvas),
            control.TranslatePoint(new Point(control.ActualWidth, control.ActualHeight), canvas)
        );
    }

    void AddLine(Point sourcePoint, Point destinationPoint, Brush stroke)
    {
        var line = new System.Windows.Shapes.Line
        {
            X1 = sourcePoint.X,
            Y1 = sourcePoint.Y,
            X2 = destinationPoint.X,
            Y2 = destinationPoint.Y,
            Stroke = stroke,
        };
        canvas.Children.Add(line);
    }

    void AddRectangle(Rect rect, Brush stroke, Brush fill = null)
    {
        var rectangleShape = new System.Windows.Shapes.Rectangle
        {
            Width = rect.Width + 2,
            Height = rect.Height + 2,
            Stroke = stroke,
            Fill = fill
        };
        Canvas.SetLeft(rectangleShape, rect.Left - 1);
        Canvas.SetTop(rectangleShape, rect.Top - 1);
        canvas.Children.Add(rectangleShape);
    }

    private void RaiseSelectionChanged()
    {
        SelectionChanged?.Invoke();
    }

    void SelectControl(TextBlock vertexControl)
    {
        var node = vertexControl.Tag as Vertex;

        canvas.Children.Clear();

        if (selectedControls.Contains(vertexControl))
        {
            selectedControls.Remove(vertexControl);
            RaiseSelectionChanged();
            return;
        }

        selectedControls.Clear();
        selectedControls.Add(vertexControl);

        var sourceRect = GetRectOnCanvas(vertexControl);

        if (node.Outgoing != null)
        {
            foreach (var outgoing in node.Outgoing)
            {
                if (controlFromVertex.TryGetValue(outgoing, out var destinationControl))
                {
                    var canvasRect = GetRectOnCanvas(destinationControl);
                    var sourcePoint = new Point(sourceRect.Left + sourceRect.Width / 2, sourceRect.Top);
                    var destinationPoint = new Point(canvasRect.Left + canvasRect.Width / 2, canvasRect.Bottom);
                    AddLine(sourcePoint, destinationPoint, outgoingBrush);
                    AddRectangle(canvasRect, new SolidColorBrush(outgoingColor));
                }
            }
        }

        if (node.Incoming != null)
        {
            foreach (var incoming in node.Incoming)
            {
                if (controlFromVertex.TryGetValue(incoming, out var sourceControl))
                {
                    var canvasRect = GetRectOnCanvas(sourceControl);
                    var sourcePoint = new Point(sourceRect.Left + sourceRect.Width / 2, sourceRect.Bottom);
                    var destinationPoint = new Point(canvasRect.Left + canvasRect.Width / 2, canvasRect.Top);
                    AddLine(sourcePoint, destinationPoint, incomingBrush);
                    AddRectangle(canvasRect, new SolidColorBrush(incomingColor));
                }
            }
        }

        AddRectangle(sourceRect, new SolidColorBrush(border), Brushes.PaleGreen);
    }

    IEnumerable<TextBlock> AllTextBlocks()
    {
        foreach (var layer in layersControl.Children.OfType<Panel>())
        {
            foreach (var textBlock in layer.Children.OfType<TextBlock>())
            {
                yield return textBlock;
            }
        }
    }

    private void Clear()
    {
        controlFromVertex.Clear();
        selectedControls.Clear();
        canvas.Children.Clear();
        layersControl.Children.Clear();
    }

    public void Locate(string text)
    {
        var found = AllTextBlocks()
            .OrderBy(t => t.Text.Length)
            .ThenBy(t => t.Text)
            .FirstOrDefault(t => t.Text.ContainsIgnoreCase(text) && !selectedControls.Contains(t));
        if (found != null)
        {
            found.BringIntoView();
            SelectControl(found);
        }
    }

    public Vertex SelectedVertex => SelectedVertices.FirstOrDefault();

    public IReadOnlyList<Vertex> SelectedVertices =>
        selectedControls.Select(c => c.Tag).OfType<Vertex>().Where(v => v != null).ToArray();

    public UIElement Content => scrollViewer;
}