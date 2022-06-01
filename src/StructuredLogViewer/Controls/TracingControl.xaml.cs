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
        private bool _showOther = true;
        private bool _showNodes = true;

        public int numberOfEvaluations = 0;
        public int numberOfProjects = 0;
        public int numberOfTargets = 0;
        public int numberOfTasks = 0;
        public int numberOfNodes = 0;
        public int numberOfOthers = 0;

        public string ShowEvaluationsText => $"Show Evaluations ({numberOfEvaluations})";

        public string ShowProjectsText => $"Show Projects ({numberOfProjects})";

        public string ShowTargetsText => $"Show Targets ({numberOfTargets})";

        public string ShowTasksText => $"Show Tasks ({numberOfTasks})";

        public string ShowNodesText => $"Show Nodes Divider ({numberOfNodes})";

        public string ShowOthersText => $"Show Others ({numberOfOthers})";

        private TimeSpan initTime = TimeSpan.Zero;
        private TimeSpan computeTime = TimeSpan.Zero;
        private TimeSpan drawTime = TimeSpan.Zero;

        public string LoadStatistics => $"Init:{initTime.TotalMilliseconds}ms, Compute:{computeTime.TotalMilliseconds}ms, Draw:{drawTime.TotalMilliseconds}ms";

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

        public bool ShowOther
        {
            get => _showOther;
            set { _showOther = value; if (this.numberOfOthers > 0) ComputeAndDraw(); }
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

        public TracingControl()
        {
            scaleTransform = new ScaleTransform();
            this.DataContext = this;
            InitializeComponent();
            this.PreviewMouseWheel += TimelineControl_MouseWheel;
            grid.LayoutTransform = scaleTransform;
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
            scaleFactor = value;
            scaleTransform.ScaleX = scaleFactor;
            scaleTransform.ScaleY = scaleFactor;
        }

        private const double minimumZoom = 0.1;
        private const double maximumZoom = 4.0;

        private void TimelineControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (scaleFactor < maximumZoom)
                {
                    scaleFactor += 0.1;
                    zoomSlider.Value = scaleFactor;
                }
            }
            else
            {
                if (scaleFactor > minimumZoom + 0.1)
                {
                    scaleFactor -= 0.1;
                    zoomSlider.Value = scaleFactor;
                }
            }

            e.Handled = true;
        }

        public BuildControl BuildControl { get; set; }

        public Dictionary<BaseNode, TextBlock> TextBlocks { get; set; } = new Dictionary<BaseNode, TextBlock>();

        private bool isDoubleClick = false;

        public Timeline Timeline { get; set; }

        // SetTimeline is called from BuildControl which will set the global values
        public void SetTimeline(Timeline timeline, long globalStart, long globalEnd)
        {
            var start = Timestamp;
            Timeline = timeline;
            GlobalStartTime = globalStart;
            GlobalEndTime = globalEnd;

            // quick size check.  If it is too much, then disable some "Show" options.
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
                            this.numberOfTargets++;
                            break;
                        case Microsoft.Build.Logging.StructuredLogger.Task:
                            this.numberOfTasks++;
                            break;
                        default:
                            this.numberOfOthers++;
                            break;
                    }
                }

                if (lanes.Value.Blocks.Count > 0)
                {
                    this.numberOfNodes++;
                }

                if (this._showProject && totalItems > 10000)
                {
                    this._showEvaluation = true;
                    this._showProject = false;
                    this._showTask = true;
                    this._showTarget = false;
                    this._showOther = false;
                    this._showNodes = false;
                }
            }

            var sample = new TextBlock();
            sample.Text = "W";
            sample.Measure(new Size(10000, 10000));
            textHeight = sample.DesiredSize.Height;

            lanesPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
            this.initTime = Timestamp - start;

            ComputeAndDraw();
            grid.Children.Add(lanesPanel);
        }

        private void ComputeAndDraw()
        {
            if (Timeline == null)
            {
                return;
            }

            ComputeTimeline();

            Draw();
        }

        private DateTime Timestamp => DateTime.UtcNow;

        private void ComputeTimeline()
        {
            var start = Timestamp;
            // Sort by the start time of each lane
            var keys1 = Timeline.Lanes.Where(p => p.Value.Blocks.Any()).ToDictionary(key => key.Key, p => p.Value.Blocks.Min(p => p.StartTime.Ticks)).ToList();
            keys1.Sort((l, r) =>
            {
                return l.Value.CompareTo(r.Value);
            });
            var keys = keys1.Select(Key => Key.Key).ToList();

            // Get the max number of lanes
            var length = Math.Max(keys.Count(), keys.Max() + 1);

            var blocksCollectionArray = new List<Block>[length];
            Parallel.ForEach(keys, (key) =>
            {
                var lane = Timeline.Lanes[key];
                blocksCollectionArray[key] = ComputeVisibleBlocks(lane);
            });

            blocksCollection = blocksCollectionArray.Where(p => p != null).ToList();
            this.computeTime = Timestamp - start;
        }

        private int[] ComputerHeatGraphData(double unitDuration = 1)
        {
            var graphData = new int[(int)Math.Floor(ConvertTimeToPixel(GlobalEndTime - GlobalStartTime) / unitDuration)];

            foreach (var blocks in blocksCollection)
            {
                if (blocks == null || blocks.Count == 0)
                    continue;

                foreach (var block in blocks)
                {
                    if (block.Node is Microsoft.Build.Logging.StructuredLogger.Task)
                    {
                        int left = (int)Math.Floor(ConvertTimeToPixel(block.Start - GlobalStartTime) / unitDuration);
                        int right = (int)Math.Floor(ConvertTimeToPixel(block.End - GlobalStartTime) / unitDuration);

                        for (; left <= right; left++)
                        {
                            graphData[left]++;
                        }
                    }
                }
            }

            return graphData;
        }

        private Panel TopRulerNodeDivider;
        private Panel HeatGraph;

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
            if (TopRulerNodeDivider == null)
                TopRulerNodeDivider = CreatePanelForNodeDivider(true);

            if (HeatGraph == null)
                HeatGraph = CreateActivityLineGraph();

            lanesPanel.Children.Add(HeatGraph);
            lanesPanel.Children.Add(TopRulerNodeDivider);

            foreach (var blocks in blocksCollection)
            {
                var panel = CreatePanelForLane(blocks, GlobalStartTime);

                if (panel != null && panel.Children.Count > 0)
                {
                    lanesPanel.Children.Add(panel);
                }
            }

            if (ShowNodes)
                DrawAddNodeDivider();

            this.drawTime = Timestamp - start;
        }

        private Panel CreateActivityLineGraph()
        {
            var timelineWidth = ConvertTimeToPixel(GlobalEndTime - GlobalStartTime);
            var graphHeight = textHeight * 4;

            // WPF is really slow to render, so only render fixed number entries
            var lineWidth = Math.Max(timelineWidth / 4000, 1);
            var graphData = ComputerHeatGraphData(lineWidth);

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;
            canvas.Background = lanesPanel.Background;
            canvas.Height = graphHeight;
            canvas.Width = timelineWidth;

            // compute the largest value but keep it within # of nodes
            int maxData = Math.Min(blocksCollection.Count, graphData.Max());

            double dataGraphHeightRatio = graphHeight / maxData;

            for (int i = 0; i < graphData.Length; i++)
            {
                if (graphData[i] > 0)
                {
                    double normalizedGraphHeight = (double)Math.Min(graphData[i], maxData) * dataGraphHeightRatio;
                    Line barLine = new Line()
                    {
                        Stroke = taskBackground,
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

        private List<Block> ComputeVisibleBlocks(Lane lane)
        {
            double pixelDuration = ConvertPixelToTime(1);
            var blocks = lane.Blocks.Where(b =>
            {
                if (b.Duration.Ticks < pixelDuration)
                    return false;

                switch (b.Node)
                {
                    case ProjectEvaluation:
                        return ShowEvaluation;
                    case Project:
                        return ShowProject;
                    case Target:
                        return ShowTarget;
                    case Microsoft.Build.Logging.StructuredLogger.Task:
                        return ShowTask;
                    default:
                        return ShowOther;
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

        private Canvas CreatePanelForLane(List<Block> blocks, long globalStart)
        {
            if (blocks == null || blocks.Count == 0)
                return null;

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;
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

                var content = new ContentControl();
                var textBlock = new TextBlock();
                textBlock.Text = $"{block.Text} ({TextUtilities.DisplayDuration(block.Duration)})";
                textBlock.Background = ChooseBackground(block);

                double indentOffset = textHeight * block.Indent;

                double left = ConvertTimeToPixel(block.Start - globalStart);
                double duration = ConvertTimeToPixel(block.End - block.Start);

                if (duration < minimumDurationToInclude)
                {
                    continue;
                }

                textBlock.Measure(new Size(10000, 10000));

                textBlock.Width = duration;
                textBlock.Height = textHeight;
                textBlock.ToolTip = block.GetTooltip();
                textBlock.MouseUp += TextBlock_MouseUp;
                textBlock.Tag = block;
                TextBlocks.Add(block.Node, textBlock);

                canvasHeight = Math.Max(indentOffset + textHeight, canvasHeight);
                canvasWidth = Math.Max(duration + left, canvasWidth);

                Canvas.SetLeft(content, left);
                Canvas.SetTop(content, indentOffset);
                content.Content = textBlock;
                content.MouseDoubleClick += Content_MouseDoubleClick;
                canvas.Children.Add(content);
            }

            canvas.Height = canvasHeight;
            canvas.Width = canvasWidth;
            canvas.HorizontalAlignment = HorizontalAlignment.Left;

            return canvas;
        }

        private void Content_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var content = sender as ContentControl;
            if (content.Content is TextBlock textBlock && textBlock.Tag is Block)
            {
                isDoubleClick = true;
            }
        }

        private TextBlock activeTextBlock = null;
        private Border highlight = new Border()
        {
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1)
        };

        private StackPanel lanesPanel;

        private void HighlightTextBlock(TextBlock hit, bool scrollToElement = false)
        {
            if (activeTextBlock == hit)
            {
                if (scrollToElement)
                {
                    ContentControl content = activeTextBlock.Parent as ContentControl;
                    Point p = content.TranslatePoint(new Point(0, 0), grid);
                    horizontalOffset = p.X > 20 ? p.X - 20 : p.X;
                    verticalOffset = p.Y > 20 ? p.Y - 20 : p.Y;
                }

                return;
            }

            if (activeTextBlock != null)
            {
                if (highlight.Parent is Panel parent)
                {
                    parent.Children.Remove(highlight);
                }
            }

            activeTextBlock = hit;

            if (activeTextBlock != null)
            {
                ContentControl content = activeTextBlock.Parent as ContentControl;
                if (content != null && content.Parent is Panel parent)
                {
                    parent.Children.Add(highlight);
                    Canvas.SetLeft(highlight, Canvas.GetLeft(content));
                    Canvas.SetTop(highlight, Canvas.GetTop(content));
                    highlight.Width = activeTextBlock.Width;
                    highlight.Height = activeTextBlock.Height;

                    if (scrollToElement)
                    {
                        Point p = content.TranslatePoint(new Point(0, 0), grid);
                        horizontalOffset = p.X > 20 ? p.X - 20 : p.X;
                        verticalOffset = p.Y > 20 ? p.Y - 20 : p.Y;

                        horizontalOffset = Math.Max(horizontalOffset, 0);
                        verticalOffset = Math.Max(verticalOffset, 0);

                        scrollViewer.ScrollToHorizontalOffset(horizontalOffset);
                        scrollViewer.ScrollToVerticalOffset(verticalOffset);
                    }
                }
            }
        }

        private void TextBlock_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is Block block)
            {
                if (isDoubleClick)
                {
                    isDoubleClick = false;
                    BuildControl.SelectItem(block.Node);
                }
                else
                {
                    HighlightTextBlock(textBlock);
                }
            }
        }

        private static readonly Brush nodeBackground = new SolidColorBrush(Color.FromArgb(20, 180, 180, 180));
        private static readonly Brush projectBackground = new SolidColorBrush(Color.FromArgb(50, 180, 180, 180));
        private static readonly Brush projectEvaluationBackground = new SolidColorBrush(Color.FromArgb(20, 180, 180, 180));
        private static readonly Brush targetBackground = new SolidColorBrush(Color.FromArgb(50, 255, 100, 255));
        private static readonly Brush taskBackground = new SolidColorBrush(Color.FromArgb(60, 100, 255, 255));

        private static Brush ChooseBackground(Block block)
        {
            switch (block.Node)
            {
                case Project _: return projectBackground;
                case ProjectEvaluation _: return projectEvaluationBackground;
                case Target _: return targetBackground;
                case Microsoft.Build.Logging.StructuredLogger.Task _: return taskBackground;
            }

            return Brushes.Transparent;
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            zoomSlider.Value = 1;
        }
    }
}
