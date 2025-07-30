using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        canvas.SetResourceReference(Panel.BackgroundProperty, "Theme_WhiteBackground");

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

    public void Redraw()
    {
        Clear();
        Populate();
    }

    private bool hideTransitiveEdges;
    public bool HideTransitiveEdges
    {
        get => hideTransitiveEdges;
        set
        {
            if (hideTransitiveEdges == value)
            {
                return;
            }

            hideTransitiveEdges = value;
            SelectControls(selectedControls.ToArray());
        }
    }

    private bool layerByDepth;
    public bool LayerByDepth
    {
        get => layerByDepth;
        set
        {
            if (layerByDepth == value)
            {
                return;
            }

            layerByDepth = value;
            Redraw();
        }
    }

    private bool horizontal;
    public bool Horizontal
    {
        get => horizontal;
        set
        {
            if (horizontal == value)
            {
                return;
            }

            horizontal = value;
            Redraw();
        }
    }

    private bool inverted;
    public bool Inverted
    {
        get => inverted;
        set
        {
            if (inverted == value)
            {
                return;
            }

            inverted = value;
            Redraw();
        }
    }

    private void Populate()
    {
        var maxHeight = graph.Vertices.Max(g => g.Height);
        var maxDepth = graph.Vertices.Max(g => g.Depth);
        var primaryOrientation = Orientation.Vertical;
        var secondaryOrientation = Orientation.Horizontal;
        if (Horizontal)
        {
            primaryOrientation = Orientation.Horizontal;
            secondaryOrientation = Orientation.Vertical;
        }

        layersControl.Orientation = primaryOrientation;

        Func<Vertex, int> groupBy = v => v.Height;
        if (LayerByDepth)
        {
            groupBy = v => maxDepth - v.Depth;
        }

        var groups = graph.Vertices.GroupBy(groupBy).OrderBy(g => g.Key).ToArray();
        if (Inverted)
        {
            groups = groups.Reverse().ToArray();
        }

        foreach (var vertexGroup in groups)
        {
            var layerPanel = new StackPanel { Orientation = secondaryOrientation };

            foreach (var vertex in vertexGroup.OrderByDescending(s => s.InDegree).ThenBy(s => s.Title))
            {
                var depthOrHeight = vertex.Depth;
                if (LayerByDepth)
                {
                    depthOrHeight = maxHeight - vertex.Height;
                }

                var background = ComputeBackground(depthOrHeight);

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

                vertexControl.MouseDown += (s, args) =>
                {
                    if (selectedControls.Contains(vertexControl))
                    {
                        SelectControls(selectedControls.Where(c => c != vertexControl).ToArray());
                    }
                    else
                    {
                        if (Keyboard.Modifiers is ModifierKeys.Shift or ModifierKeys.Control)
                        {
                            SelectControls(selectedControls.Take(1).Append(vertexControl).ToArray());
                        }
                        else
                        {
                            SelectControls([vertexControl]);
                        }
                    }
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

    void AddLine(FrameworkElement fromControl, FrameworkElement toControl, Brush stroke)
    {
        var sourceRect = GetRectOnCanvas(fromControl);
        var destinationRect = GetRectOnCanvas(toControl);
        var sourcePoint = new Point(sourceRect.Left + sourceRect.Width / 2, sourceRect.Top);
        var destinationPoint = new Point(destinationRect.Left + destinationRect.Width / 2, destinationRect.Bottom);
        AddLine(sourcePoint, destinationPoint, stroke);
    }

    void AddRectangle(FrameworkElement element, Brush stroke, Brush fill = null)
    {
        var rect = GetRectOnCanvas(element);
        AddRectangle(rect, stroke, fill);
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

    void SelectControls(IEnumerable<FrameworkElement> controls)
    {
        canvas.Children.Clear();
        selectedControls.Clear();

        if (controls != null)
        {
            var toSelect = controls.Take(2).ToArray();
            selectedControls.UnionWith(controls);
            if (toSelect.Length == 1)
            {
                SelectControl(toSelect[0]);
            }
            else if (toSelect.Length == 2)
            {
                SelectControls(toSelect[0], toSelect[1]);
            }
        }

        RaiseSelectionChanged();
    }

    private void SelectControls(FrameworkElement fromControl, FrameworkElement toControl)
    {
        Vertex from = (Vertex)fromControl.Tag;
        Vertex to = (Vertex)toControl.Tag;

        if (from.Height < to.Height)
        {
            (from, to) = (to, from);
            (fromControl, toControl) = (toControl, fromControl);
        }

        var highlighted = new HashSet<FrameworkElement>();

        var edges = new HashSet<(FrameworkElement start, FrameworkElement end)>();

        Digraph.FindAllPaths(from, to, path =>
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (controlFromVertex.TryGetValue(path[i], out var control))
                {
                    highlighted.Add(control);

                    var target = i < path.Count - 1 ? path[i + 1] : to;
                    if (controlFromVertex.TryGetValue(target, out var targetControl))
                    {
                        edges.Add((control, targetControl));
                    }
                }
            }
        });

        highlighted.Remove(fromControl);
        highlighted.Remove(toControl);

        foreach (var highlight in highlighted)
        {
            AddRectangle(highlight, new SolidColorBrush(Colors.Blue), Brushes.Azure);
        }

        AddRectangle(fromControl, Brushes.Red, Brushes.Pink);
        AddRectangle(toControl, Brushes.Red, Brushes.Pink);

        foreach (var edge in edges)
        {
            AddLine(edge.start, edge.end, outgoingBrush);
        }
    }

    void SelectControl(FrameworkElement vertexControl)
    {
        var sourceRect = GetRectOnCanvas(vertexControl);

        var node = vertexControl.Tag as Vertex;

        AddOutgoingEdges(sourceRect, node);

        AddIncomingEdges(sourceRect, node);

        AddRectangle(sourceRect, new SolidColorBrush(border), Brushes.PaleGreen);
    }

    private void AddIncomingEdges(Rect destinationRect, Vertex destinationVertex)
    {
        foreach (var incoming in destinationVertex.Incoming)
        {
            if (HideTransitiveEdges &&
                incoming.TransitiveOutgoing != null &&
                incoming.TransitiveOutgoing.Contains(destinationVertex))
            {
                continue;
            }

            if (controlFromVertex.TryGetValue(incoming, out var sourceControl))
            {
                var sourceRect = GetRectOnCanvas(sourceControl);
                var sourcePoint = new Point(sourceRect.Left + sourceRect.Width / 2, sourceRect.Top);
                var destinationPoint = new Point(destinationRect.Left + destinationRect.Width / 2, destinationRect.Bottom);
                AddLine(sourcePoint, destinationPoint, incomingBrush);
                AddRectangle(sourceRect, new SolidColorBrush(incomingColor));
            }
        }
    }

    private void AddOutgoingEdges(Rect sourceRect, Vertex node)
    {
        IEnumerable<Vertex> list = HideTransitiveEdges ? node.NonRedundantOutgoing : node.Outgoing;

        foreach (var outgoing in list)
        {
            if (controlFromVertex.TryGetValue(outgoing, out var destinationControl))
            {
                var destinationRect = GetRectOnCanvas(destinationControl);
                var sourcePoint = new Point(sourceRect.Left + sourceRect.Width / 2, sourceRect.Top);
                var destinationPoint = new Point(destinationRect.Left + destinationRect.Width / 2, destinationRect.Bottom);
                AddLine(sourcePoint, destinationPoint, outgoingBrush);
                AddRectangle(destinationRect, new SolidColorBrush(outgoingColor));
            }
        }
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
        if (string.IsNullOrWhiteSpace(text))
        {
            SelectControls(null);
            return;
        }

        var parts = text.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        var found = parts.Select(p => FindControlByText(p)).Where(c => c != null).Take(2).ToArray();
        foreach (var foundControl in found)
        {
            foundControl.BringIntoView();
        }

        SelectControls(found);
    }

    private FrameworkElement FindControlByText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();

        return AllTextBlocks()
            .OrderBy(t => selectedControls.Contains(t))
            .OrderBy(t => t.Text.Length)
            .ThenBy(t => t.Text)
            .FirstOrDefault(t => t.Text.ContainsIgnoreCase(text));
    }

    public Vertex SelectedVertex => SelectedVertices.FirstOrDefault();

    public IReadOnlyList<Vertex> SelectedVertices =>
        selectedControls.Select(c => c.Tag).OfType<Vertex>().Where(v => v != null).ToArray();

    public UIElement Content => scrollViewer;
}