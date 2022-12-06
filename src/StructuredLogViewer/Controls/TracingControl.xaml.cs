using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class TracingControl : UserControl
    {
        // Ratio of time reported to the smallest pixel to render.
        private const double TimeToPixel = 72000;

        private readonly ScaleTransform scaleTransform;

        private readonly Size textBlockSize = new Size(10000, 10000);

        private double OneSecondPixelWidth;

        // Build start time to sync all canvas.
        private long GlobalStartTime;

        // Build end time.
        private long GlobalEndTime;

        private List<List<Block>> blocksCollection = new List<List<Block>>();

        private bool _showEvaluation = true;
        private bool _showProject = true;
        private bool _showTarget = true;
        private bool _showTask = true;
        private bool _showCpp = false;
        private bool _showNodes = true;
        private bool _groupByNodes = true;
        private bool _showProjectReferenceSelection = true;


        public int numberOfEvaluations = 0;
        public int numberOfProjects = 0;
        public int numberOfTargets = 0;
        public int numberOfTasks = 0;
        public int numberOfNodes = 0;
        public int numberOfCpp = 0;

        public string ShowEvaluationsText => $"Show Evaluations ({numberOfEvaluations})";

        public string ShowProjectsText => $"Show Projects ({numberOfProjects})";

        public string ShowTargetsText => $"Show Targets ({numberOfTargets})";

        public string ShowTasksText => $"Show Tasks ({numberOfTasks})";

        public string ShowNodesText => $"Show Nodes Divider ({numberOfNodes})";

        public string ShowCppText => $"Show Cpp Details ({numberOfCpp})";

        private TimeSpan computeTime = TimeSpan.Zero;
        private TimeSpan drawTime = TimeSpan.Zero;

        public string LoadStatistics => $"Compute:{computeTime.TotalMilliseconds}ms, Draw:{drawTime.TotalMilliseconds}ms";

        public bool ShowEvaluation
        {
            get => _showEvaluation;
            set { _showEvaluation = value; ComputeAndDraw(); }
        }

        public bool ShowProject
        {
            get => _showProject;
            set { _showProject = value; ComputeAndDraw(); }
        }

        public bool ShowTarget
        {
            get => _showTarget;
            set { _showTarget = value; ComputeAndDraw(); }
        }

        public bool ShowTask
        {
            get => _showTask;
            set { _showTask = value; ComputeAndDraw(); }
        }

        public bool ShowCpp
        {
            get => _showCpp;
            set { _showCpp = value; if (this.numberOfCpp > 0) { ComputeAndDraw(); } }
        }

        public bool ShowNodes
        {
            get => _showNodes;
            set
            {
                _showNodes = value;
                if (_showNodes)
                {
                    DrawAddNodeDivider();
                }
                else
                {
                    DrawRemoveNodeDivider();
                }
            }
        }

        public bool GroupByNodes
        {
            get => _groupByNodes;
            set
            {
                _groupByNodes = value;
                if (numberOfNodes > 1)
                {
                    ComputeAndDraw();
                }
            }
        }

        public bool ShowProjectReferenceSelection
        {
            get => _showProjectReferenceSelection;
            set
            {
                _showProjectReferenceSelection = value;
                DrawHighLight();
            }
        }

        public TracingControl()
        {
            scaleTransform = new ScaleTransform();
            this.DataContext = this;
            InitializeComponent();
            this.PreviewMouseWheel += TimelineControl_MouseWheel;
            grid.LayoutTransform = scaleTransform;
            overlayGrid.LayoutTransform = scaleTransform;
        }

        private double scaleFactor = 1;
        private double horizontalOffset = 0;
        private double verticalOffset = 0;
        private double textHeight;

        private void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(horizontalOffset);
            scrollViewer.ScrollToVerticalOffset(verticalOffset);
        }

        private void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            horizontalOffset = scrollViewer.HorizontalOffset;
            verticalOffset = scrollViewer.VerticalOffset;
        }

        private void zoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double ratio = zoomSlider.Value;
            if (Math.Abs(zoomSlider.Value - 1) <= 0.001)
            {
                ratio = 1;
            }

            Zoom(ratio);
            resetZoomButton.Visibility = ratio == 1 ? Visibility.Hidden : Visibility.Visible;
        }

        private void Zoom(double value)
        {
            double delta = value / scaleFactor;
            scaleFactor = value;
            scaleTransform.ScaleX = scaleFactor;
            scaleTransform.ScaleY = scaleFactor;

            UpdatedGraph(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth);
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset * delta);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset * delta);
        }

        private const double minimumZoom = 0.1;
        private const double maximumZoom = 4.0;

        private void TimelineControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double delta = 0;
            if (e.Delta > 0)
            {
                if (scaleFactor < maximumZoom)
                {
                    delta = 1.1;
                }
            }
            else
            {
                if (scaleFactor > minimumZoom + 0.1)
                {
                    delta = 0.9;
                }
            }

            if (delta != 0)
            {
                scaleFactor *= delta;
                zoomSlider.Value = scaleFactor;

                UpdatedGraph(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth);
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset * delta);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset * delta);
            }

            e.Handled = true;
        }

        public BuildControl BuildControl { get; set; }

        public Dictionary<BaseNode, TextBlock> TextBlocks { get; set; } = new Dictionary<BaseNode, TextBlock>();

        private DateTime lastClickTimestamp = DateTime.MinValue;

        public Timeline Timeline { get; set; }

        // SetTimeline is called from BuildControl which will set the global values
        public void SetTimeline(Timeline timeline, long globalStart, long globalEnd)
        {
            Timeline = timeline;
            GlobalStartTime = globalStart;
            GlobalEndTime = globalEnd;

            // Global end time might not be set because the stream could had been terminated early.
            // Locate the last block and use that as the global end time instead.
            if (globalEnd < globalStart || globalEnd < 0)
            {
                long maxEndTime = 0;
                foreach (var lanes in timeline.Lanes)
                {
                    foreach (var block in lanes.Value.Blocks)
                    {
                        maxEndTime = Math.Max(maxEndTime, block.EndTime.Ticks);
                    }
                }

                GlobalEndTime = maxEndTime;
            }

            // Quick size count
            int totalItems = 0;
            foreach (var lanes in timeline.Lanes)
            {
                totalItems += lanes.Value.Blocks.Count;

                foreach (var block in lanes.Value.Blocks)
                {
                    switch (block.Node)
                    {
                        case ProjectEvaluation:
                            this.numberOfEvaluations++;
                            break;
                        case Project:
                            this.numberOfProjects++;
                            break;
                        case Target:
                            if (!ignoreCommonP2PTargets.Contains((block.Node as Target).Name))
                            {
                                this.numberOfTargets++;
                            }
                            break;
                        case Microsoft.Build.Logging.StructuredLogger.Task:
                            this.numberOfTasks++;
                            break;
                        default:
                            this.numberOfCpp++;
                            break;
                    }
                }

                if (lanes.Value.Blocks.Count > 0)
                {
                    this.numberOfNodes++;
                }
            }

            var sample = new TextBlock();
            sample.Text = "W";
            sample.Measure(new Size(10000, 10000));
            textHeight = sample.DesiredSize.Height;

            lanesPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
            overlayCanvas = new Canvas();
            overlayGrid.Children.Add(overlayCanvas);

            grid.Children.Add(lanesPanel);
            ComputeAndDraw();
        }

        private void ComputeAndDraw()
        {
            if (Timeline == null)
            {
                return;
            }

            ComputeTimeline();
            this.lastRenderTimeStamp = 0;
            Draw();
            DrawHighLight(false);
        }

        private void DrawHighLight(bool draw = true)
        {
            // Remove and Redraw Highlight
            if (activeTextBlock != null)
            {
                var hit = activeTextBlock.Tag;
                activeTextBlock = null;
                overlayCanvas.Children.Clear(); // clear highlight

                // Note: I want to redraw the Highlight but sometimes the Canvas position isn't ready yet.
                if (draw && hit is Block block && TextBlocks.TryGetValue(block.Node, out TextBlock foundBlock))
                {
                    HighlightTextBlock(foundBlock);
                }
            }
        }

        private DateTime Timestamp => DateTime.UtcNow;

        private void ComputeTimeline()
        {
            var start = Timestamp;
            blocksCollection.Clear();

            if (!_groupByNodes)
            {
                var allBlocks = Timeline.Lanes.SelectMany(p => p.Value.Blocks);
                blocksCollection.Add(ComputeVisibleBlocks(allBlocks));
            }
            else
            {
                // Sort by the start time of each lane
                var sortedKeys = Timeline.Lanes.Where(p => p.Value.Blocks.Any()).ToDictionary(key => key.Key, p => p.Value.Blocks.Min(p => p.StartTime.Ticks)).ToList();
                sortedKeys.Sort((l, r) =>
                {
                    return l.Value.CompareTo(r.Value);
                });
                var keys = sortedKeys.Select(Key => Key.Key).ToList();

                // Get the max number of lanes
                // Note: Max() throws if keys is empty.
                int keysMax = keys.Any() ? keys.Max() + 1 : 1;
                var length = Math.Max(keys.Count(), keysMax);

                var blocksCollectionArray = new List<Block>[length];
                Parallel.ForEach(keys, (key) =>
                {
                    blocksCollectionArray[key] = ComputeVisibleBlocks(Timeline.Lanes[key].Blocks);
                });

                blocksCollection = blocksCollectionArray.Where(p => p != null).ToList();
            }
            this.computeTime = Timestamp - start;
        }

        private struct HeatGraphNode
        {
            public HeatGraphNode() { }
            public double Height = 0;
            public bool HasError = false;
        }

        private HeatGraphNode[] ComputeHeatGraphData(double unitDuration = 1)
        {
            var graphLength = (int)Math.Floor(ConvertTimeToPixel(GlobalEndTime - GlobalStartTime) / unitDuration) + 1;
            var graphData = new HeatGraphNode[graphLength];

            foreach (var blocks in blocksCollection)
            {
                if (blocks == null || blocks.Count == 0)
                {
                    continue;
                }

                foreach (var block in blocks)
                {
                    if (!(block.Node is ProjectEvaluation ||
                        block.Node is Project ||
                        block.Node is Target))
                    {
                        int left = (int)Math.Floor(ConvertTimeToPixel(block.Start - GlobalStartTime) / unitDuration);
                        int right = (int)Math.Floor(ConvertTimeToPixel(block.End - GlobalStartTime) / unitDuration);

                        if (left >= graphLength)
                        {
                            continue;
                        }

                        // Round the edge to a percentage.
                        // 0-----1-----2-----4
                        // |-----|-----|-----|
                        //    <---Task--->
                        //    60%, 100%, 40%

                        graphData[left].HasError |= block.HasError;

                        // If the start and end are in the same unit.
                        if (left == right)
                        {
                            graphData[left].Height += ConvertTimeToPixel(block.End - block.Start) % unitDuration / unitDuration;
                            continue;
                        }

                        // Left edge
                        graphData[left].Height += (unitDuration - ConvertTimeToPixel(block.Start - GlobalStartTime) % unitDuration) / unitDuration;

                        // Right edge, safeguard right edge is truely to the right.
                        if (left < right && right < graphLength)
                        {
                            graphData[right].Height += ConvertTimeToPixel(block.End - GlobalStartTime) % unitDuration / unitDuration;
                            graphData[right].HasError |= block.HasError;
                        }

                        left++;

                        while (left < right && left < graphLength)
                        {
                            graphData[left].Height++;
                            graphData[left].HasError |= block.HasError;
                            left++;
                        }
                    }
                }
            }

            return graphData;
        }

        private Panel TopRulerNodeDivider;
        private Panel HeatGraph;
        private double lastRenderTimeStamp = 0;

        /// <summary>
        /// Draw Graph 
        /// </summary>
        private void Draw()
        {
            var start = Timestamp;
            if (Timeline == null)
            {
                return;
            }

            TextBlocks?.Clear();
            lanesPanel.Children.Clear();

            // Compute number of pixel for one second, used by ruler
            OneSecondPixelWidth = ConvertTimeToPixel(TimeSpan.FromSeconds(1).Ticks);

            // Add Top Timeline Ruler
            TopRulerNodeDivider ??= CreatePanelForNodeDivider(true);
            HeatGraph ??= CreateActivityLineGraph();

            lanesPanel.Children.Add(HeatGraph);
            lanesPanel.Children.Add(TopRulerNodeDivider);

            // scrollViewer may not have been initialized, fallback to BuildControl for size.
            var offset = Math.Max(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth, BuildControl.ActualWidth);
            var renderWidthTimeStamp = GlobalStartTime + ConvertPixelToTime(offset / scaleTransform.ScaleX);

            for (int i = 0; i < blocksCollection.Count; i++)
            {
                var blocks = blocksCollection[i];

                (double maxHeight, double maxWidth) = CreateTextBlocks(blocks);

                var culledBlocks = blocks.Where(block => !(lastRenderTimeStamp > block.Start || block.Start >= renderWidthTimeStamp));

                var panel = CreatePanelForLane(culledBlocks, maxHeight, maxWidth);
                if (panel != null)
                {
                    panel.Name = $"node{i}";
                    lanesPanel.Children.Add(panel);
                }
            }

            this.lastRenderTimeStamp = renderWidthTimeStamp;

            if (ShowNodes)
            {
                DrawAddNodeDivider();
            }

            this.drawTime = Timestamp - start;
        }

        private Panel CreateActivityLineGraph()
        {
            var timelineWidth = ConvertTimeToPixel(GlobalEndTime - GlobalStartTime);
            var graphHeight = textHeight * 4;

            // WPF is really slow to render, so only render fixed number entries
            var lineWidth = Math.Max(timelineWidth / 4000, 1);
            var graphData = ComputeHeatGraphData(lineWidth);

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;
            canvas.Background = lanesPanel.Background;
            canvas.Height = graphHeight;
            canvas.Width = timelineWidth;

            // compute the largest value but keep it within number of nodes
            double maxData = graphData.Select(g => g.Height).Max();
            maxData = _groupByNodes ? Math.Min(blocksCollection.Count, maxData) : maxData;

            double dataGraphHeightRatio = graphHeight / maxData;

            for (int i = 0; i < graphData.Length; i++)
            {
                if (graphData[i].Height > 0)
                {
                    double normalizedGraphHeight = Math.Min(graphData[i].Height, maxData) * dataGraphHeightRatio;
                    Line barLine = new Line()
                    {
                        Stroke = graphData[i].HasError ? errorBackground : taskBackground,
                        StrokeThickness = lineWidth,
                        X1 = i * lineWidth,
                        X2 = i * lineWidth,
                        Y1 = graphHeight,
                        Y2 = graphHeight - normalizedGraphHeight,
                    };

                    canvas.Children.Add(barLine);
                }
            }

            return canvas;
        }

        private void DrawAddNodeDivider()
        {
            int showMeasurementMod = 1;

            // Start from second element to account for the top ruler
            for (int index = 3; index < lanesPanel.Children.Count; index += 2)
            {
                lanesPanel.Children.Insert(index, CreatePanelForNodeDivider(showMeasurementMod % 5 == 0));
                showMeasurementMod++;
            }
        }

        private void DrawRemoveNodeDivider()
        {
            // Start from second element to account for the top ruler
            for (int index = 2; index < lanesPanel.Children.Count; index++)
            {
                if (lanesPanel.Children[index] is Canvas foobar)
                {
                    if (foobar.Background == nodeBackground)
                    {
                        lanesPanel.Children.RemoveAt(index);
                    }
                }
            }
        }

        private Panel CreatePanelForNodeDivider(bool showTime)
        {
            var timeWidth = ConvertTimeToPixel(GlobalEndTime - GlobalStartTime);

            bool fiveSeconds = false;
            double gapWidth;
            if (OneSecondPixelWidth / textHeight < 3)
            {
                gapWidth = (5 * OneSecondPixelWidth / textHeight) - 0.1;
                fiveSeconds = true;
            }
            else
            {
                gapWidth = (OneSecondPixelWidth / textHeight) - 0.1;
            }

            // A dash or gap relative to the Thickness of the pen
            Line nodeLine = new Line()
            {
                StrokeDashArray = new DoubleCollection { 0.1, gapWidth },
                Stroke = Brushes.Black,
                StrokeThickness = textHeight,
                Height = textHeight,
                X2 = timeWidth,
                Y1 = textHeight / 2,
                Y2 = textHeight / 2,
            };

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;
            canvas.Children.Add(nodeLine);

            if (showTime)
            {
                for (int i = 0; i < timeWidth / OneSecondPixelWidth; i++)
                {
                    if (!fiveSeconds || i % 5 == 0)
                    {
                        var textBlock = new TextBlock();
                        textBlock.Text = $"{i}s";

                        // add textHeight/2 pixels of front padding
                        Canvas.SetLeft(textBlock, textHeight / 2 + i * OneSecondPixelWidth);
                        canvas.Children.Add(textBlock);
                    }
                }
            }

            canvas.VerticalAlignment = VerticalAlignment.Top;
            canvas.Background = nodeBackground;
            canvas.Height = textHeight;
            canvas.Width = timeWidth;
            return canvas;
        }

        private static double ConvertTimeToPixel(double time)
        {
            return time / TimeToPixel;
        }

        private static double ConvertPixelToTime(double pixel)
        {
            return pixel * TimeToPixel;
        }

        public void GoToTimedNode(TimedNode node)
        {
            TextBlock textblock = null;
            foreach (TimedNode timedNode in node.GetParentChainIncludingThis().OfType<TimedNode>().Reverse())
            {
                if (TextBlocks.TryGetValue(timedNode, out textblock))
                {
                    HighlightTextBlock(textblock, scrollToElement: true);
                    break;
                }
            }

            if (textblock == null && activeTextBlock != null)
            {
                if (highlight.Parent is Panel parent)
                {
                    parent.Children.Remove(highlight);
                }

                scrollViewer.ScrollToVerticalOffset(0);
                scrollViewer.ScrollToHorizontalOffset(0);
            }
        }

        private static readonly string[] ignoreCommonP2PTargets = {
            "Build",
            "ResolveProjectReferences",
            "GetTargetFrameworksWithPlatformFromInnerBuilds",
            "DispatchToInnerBuilds",
            "BuildGenerateSources",
            "BuildGenerateSourcesTraverse",
            "BuildCompile",
            "BuildCompileTraverse",
            "BuildLink",
            "BuildLinkTraverse"
        };

        private List<Block> ComputeVisibleBlocks(IEnumerable<Block> enumBlocks)
        {
            double pixelDuration = ConvertPixelToTime(1);
            bool showCppBlocks = ShowCpp && this.numberOfCpp > 0;
            var blocks = enumBlocks.Where(b =>
            {
                if (b.Duration.Ticks < pixelDuration)
                {
                    return false;
                }

                switch (b.Node)
                {
                    case ProjectEvaluation:
                        return ShowEvaluation;
                    case Project:
                        return ShowProject;
                    case Target:
                        return ShowTarget && !ignoreCommonP2PTargets.Contains((b.Node as Target).Name);
                    case Microsoft.Build.Logging.StructuredLogger.Task node:
                        // When ShowCpp is enabled, hide the task and show the messages so that only one of them will appear.
                        if (showCppBlocks && node is CppAnalyzer.CppTask cppNode && cppNode.HasTimedBlocks)
                        {
                            return false;
                        }
                        return ShowTask;
                    case Message:
                        return ShowCpp && ShowTask;
                    default:
                        return false;
                }
            }).ToList();

            if (blocks.Count == 0)
            {
                return null;
            }

            var endpoints = new List<BlockEndpoint>();

            foreach (var block in blocks)
            {
                block.StartPoint = new BlockEndpoint()
                {
                    Block = block,
                    Timestamp = block.StartTime.Ticks,
                    IsStart = true
                };
                block.EndPoint = new BlockEndpoint()
                {
                    Block = block,
                    Timestamp = block.EndTime.Ticks
                };
                endpoints.Add(block.StartPoint);
                endpoints.Add(block.EndPoint);
            }

            endpoints.Sort();
            List<long> indentList = new List<long>(5);

            foreach (var endpoint in endpoints)
            {
                if (endpoint.IsStart)
                {
                    int i = 0;
                    while (i < indentList.Count)
                    {
                        if (indentList[i] <= endpoint.Timestamp)
                        {
                            endpoint.Block.Indent = i;
                            indentList[i] = endpoint.Block.EndTime.Ticks;
                            break;
                        }

                        i++;
                    }

                    if (i == indentList.Count)
                    {
                        endpoint.Block.Indent = i;
                        indentList.Add(endpoint.Block.EndTime.Ticks);
                    }
                }
            }

            blocks.Sort((l, r) =>
            {
                var startDifference = l.StartTime.Ticks.CompareTo(r.StartTime.Ticks);
                if (startDifference != 0)
                {
                    return startDifference;
                }

                return l.Length.CompareTo(r.Length);
            });

            foreach (var block in blocks)
            {
                block.Start = block.StartTime.Ticks;
                block.End = block.EndTime.Ticks;
            }

            return blocks;
        }

        private Canvas CreatePanelForLane(IEnumerable<Block> blocks, double maxHeight, double maxWidth)
        {
            var canvas = new Canvas();
            canvas.Height = maxHeight;
            canvas.Width = maxWidth;
            canvas.VerticalAlignment = VerticalAlignment.Top;
            UpdatePanelForLane(canvas, blocks);
            canvas.HorizontalAlignment = HorizontalAlignment.Left;
            return canvas;
        }

        // Create all the TextBlock so that GoToTimedNode() can locate the node without it being drawn.
        private (double, double) CreateTextBlocks(IEnumerable<Block> blocks)
        {
            if (blocks == null || !blocks.Any())
            {
                return (0, 0);
            }

            double canvasWidth = 0;
            double canvasHeight = 0;

            double minimumDurationToInclude = 1; // ignore durations less than 1 pixel

            foreach (var block in blocks)
            {
                /*
                 * |-----Project -----------------|
                 *   |---Target --||---Target --|
                 *     |---Task -|  |---Task --|
                 */

                double left = ConvertTimeToPixel(block.Start - GlobalStartTime);
                double duration = ConvertTimeToPixel(block.End - block.Start);
                double indentOffset = textHeight * block.Indent;

                if (duration < minimumDurationToInclude)
                {
                    continue;
                }

                var textBlock = new TextBlock();
                textBlock.Text = $"{block.Text} ({TextUtilities.DisplayDuration(block.Duration)})";
                textBlock.Background = ChooseBackground(block);
                textBlock.Measure(textBlockSize);
                textBlock.Width = duration;
                textBlock.Height = textHeight;
                textBlock.ToolTip = block.GetTooltip();
                textBlock.MouseUp += TextBlock_MouseUp;
                textBlock.Tag = block;

                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, indentOffset);

                TextBlocks.Add(block.Node, textBlock);

                canvasHeight = Math.Max(indentOffset + textHeight, canvasHeight);
                canvasWidth = Math.Max(duration + left, canvasWidth);
            }

            return (canvasHeight, canvasWidth);
        }

        private void UpdatePanelForLane(Canvas canvas, IEnumerable<Block> blocks)
        {
            if (blocks == null || !blocks.Any())
            {
                return;
            }

            foreach (var block in blocks)
            {
                if (TextBlocks.TryGetValue(block.Node, out TextBlock textBlock))
                {
                    canvas.Children.Add(textBlock);
                }
            }
        }

        private void UpdatedGraph(double widthOffset)
        {
            // Load more blocks when scroll to the right.
            var renderWidthTimeStamp = GlobalStartTime + ConvertPixelToTime(widthOffset / scaleTransform.ScaleX);
            if (lastRenderTimeStamp > renderWidthTimeStamp)
            {
                return;
            }

            if (lanesPanel != null)
            {
                foreach (var lane in lanesPanel.Children)
                {
                    if (lane is Canvas canvas && canvas.Name.StartsWith("node"))
                    {
                        if (Int32.TryParse(canvas.Name.Substring("node".Length), out int parsedInt))
                        {
                            var blocks = blocksCollection[parsedInt];
                            var culledBlocks = blocks.Where(block => !(lastRenderTimeStamp > block.Start || block.Start >= renderWidthTimeStamp));
                            UpdatePanelForLane(canvas, culledBlocks);
                        }
                    }
                }

                lastRenderTimeStamp = renderWidthTimeStamp;
            }
        }

        private TextBlock activeTextBlock = null;
        private Border highlight = new Border()
        {
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1)
        };

        private StackPanel lanesPanel;
        private Canvas overlayCanvas;

        private void HighlightTextBlock(TextBlock hit, bool scrollToElement = false)
        {
            if (activeTextBlock == hit)
            {
                if (hit != null && scrollToElement)
                {
                    ScrollToElement(hit);
                }

                return;
            }

            if (activeTextBlock != null)
            {
                overlayCanvas.Children.Clear();
            }

            activeTextBlock = hit;

            if (activeTextBlock != null)
            {
                // Highlight node
                Point activePoint = activeTextBlock.TranslatePoint(new Point(0, 0), overlayGrid);
                Canvas.SetLeft(highlight, activePoint.X);
                Canvas.SetTop(highlight, activePoint.Y);
                highlight.Width = activeTextBlock.Width;
                highlight.Height = activeTextBlock.Height;
                overlayCanvas.Children.Add(highlight);

                // If it is a Project node, then draw lines to those node.
                if (ShowProjectReferenceSelection && activeTextBlock.Tag is Block b && b.Node is Project proj)
                {
                    HighlightProjectTextBlock(proj, activePoint);
                }

                if (scrollToElement)
                {
                    ScrollToElement(activeTextBlock);
                }
            }
        }

        private void HighlightProjectTextBlock(Project originProject, Point originPoint)
        {
            // Get Parent Project
            var parent = originProject.GetNearestParent<Project>();
            if (parent != null)
            {
                if (TextBlocks.TryGetValue(parent, out TextBlock relativeTextBlock))
                {
                    Point parentPoint = relativeTextBlock.TranslatePoint(new Point(0, 0), overlayGrid);
                    if (parentPoint.X >= 0 && parentPoint.Y >= 0)
                    {
                        DrawHorizontalLine(parentPoint, originPoint);
                    }
                }
            }

            var relatedProjectNode = originProject.FindImmediateChildrenOfType<Project>();
            foreach (var relatedProject in relatedProjectNode)
            {
                if (TextBlocks.TryGetValue(relatedProject, out TextBlock relativeTextBlock))
                {
                    Point destinationPoint = relativeTextBlock.TranslatePoint(new Point(0, 0), overlayGrid);
                    if (destinationPoint.X == 0 && destinationPoint.Y == 0)
                    {
                        continue;
                    }

                    DrawHorizontalLine(originPoint, destinationPoint);
                }
            }
        }

        private void DrawHorizontalLine(Point originPoint, Point destinationPoint)
        {
            /* 0  .
             * 1  ^ActivePoint
             * 2 --------O-------
             * 3         |
             * 4         |-------
             * 5         O<-DestinationPoint
             */

            // start the line from edge of the selection.
            double originY;
            double destinationY;
            if (originPoint.Y < destinationPoint.Y)
            {
                // Below
                originY = originPoint.Y + textHeight;
                destinationY = destinationPoint.Y + textHeight;
            }
            else
            {
                // Above
                originY = originPoint.Y;
                destinationY = destinationPoint.Y;
            }

            // Draw a line up or down.
            Line lineDown = new Line()
            {
                X1 = destinationPoint.X,
                X2 = destinationPoint.X,
                Y1 = originY,
                Y2 = destinationY,
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 1
            };
            overlayCanvas.Children.Add(lineDown);
        }

        private void ScrollToElement(TextBlock hit)
        {
            Point p = hit.TranslatePoint(new Point(0, 0), grid);
            horizontalOffset = p.X > 20 ? p.X - 20 : p.X;
            verticalOffset = p.Y > 20 ? p.Y - 20 : p.Y;

            horizontalOffset = Math.Max(horizontalOffset, 0);
            verticalOffset = Math.Max(verticalOffset, 0);

            scrollViewer.ScrollToHorizontalOffset(horizontalOffset);
            scrollViewer.ScrollToVerticalOffset(verticalOffset);
        }

        private void TextBlock_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is Block block)
            {
                if (activeTextBlock != null && activeTextBlock == textBlock && (Timestamp - lastClickTimestamp).TotalMilliseconds < 200)
                {
                    BuildControl.SelectItem(block.Node);
                }
                else
                {
                    lastClickTimestamp = Timestamp;
                    HighlightTextBlock(textBlock);
                }
            }
        }

        private static readonly Brush nodeBackground = new SolidColorBrush(Color.FromArgb(20, 180, 180, 180));
        private static readonly Brush projectBackground = new SolidColorBrush(Color.FromArgb(50, 180, 180, 180));
        private static readonly Brush projectEvaluationBackground = new SolidColorBrush(Color.FromArgb(20, 180, 180, 180));
        private static readonly Brush targetBackground = new SolidColorBrush(Color.FromArgb(50, 255, 100, 255));
        private static readonly Brush taskBackground = new SolidColorBrush(Color.FromArgb(60, 100, 255, 255));
        private static readonly Brush messageBackground = new SolidColorBrush(Color.FromArgb(60, 100, 255, 255));
        private static readonly Brush errorBackground = new SolidColorBrush(Color.FromArgb(60, 255, 86, 86));

        private static Brush ChooseBackground(Block block)
        {
            if (block.HasError)
            {
                return errorBackground;
            }

            switch (block.Node)
            {
                case Microsoft.Build.Logging.StructuredLogger.Task: return taskBackground;
                case Target: return targetBackground;
                case Project: return projectBackground;
                case ProjectEvaluation: return projectEvaluationBackground;
                case Message: return messageBackground;
            }

            return Brushes.Transparent;
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            zoomSlider.Value = 1;
            UpdatedGraph(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth);
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange == 0)
            {
                return;
            }

            UpdatedGraph(e.HorizontalOffset + e.ViewportWidth);
            e.Handled = true;
        }
    }
}
