using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class TracingControl : UserControl
    {
        // Ratio of time reported to the smallest pixel to render.
        private const double TimeToPixel = 50000;

        private double zoomRatio = 1.0;

        private double OneSecondPixelWidth;

        // Build start time to sync all canvas.
        private long GlobalStartTime;

        // Build end time.
        private long GlobalEndTime;

        private bool _showEvaluation = true;
        private bool _showProject = true;
        private bool _showTarget = true;
        private bool _showTask = true;
        private bool _showOther = true;
        private bool _showNodes = true;

        public bool ShowEvaluation
        {
            get { return _showEvaluation; }
            set { _showEvaluation = value; Draw(); }
        }

        public bool ShowProject
        {
            get { return _showProject; }
            set { _showProject = value; Draw(); }
        }

        public bool ShowTarget
        {
            get { return _showTarget; }
            set { _showTarget = value; Draw(); }
        }

        public bool ShowTask
        {
            get { return _showTask; }
            set { _showTask = value; Draw(); }
        }

        public bool ShowOther
        {
            get { return _showOther; }
            set { _showOther = value; Draw(); }
        }

        public bool ShowNodes
        {
            get { return _showNodes; }
            set { _showNodes = value; Draw(); }
        }

        public TracingControl()
        {
            this.DataContext = this;
            InitializeComponent();
            this.PreviewMouseWheel += TimelineControl_MouseWheel;
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
            if (zoomRatio != value)
            {
                zoomRatio = value;
                Draw();
            }
        }

        private const double minimumZoom = 0.1;
        private const double maximumZoom = 20.0;

        private void TimelineControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

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


        public void SetTimeline(Timeline timeline, long globalStart, long globalEnd)
        {
            Timeline = timeline;
            GlobalStartTime = globalStart;
            GlobalEndTime = globalEnd;

            // quick size check.  If its too much, then disable some "Show" options.
            int totalItems = 0;
            foreach (var lanes in timeline.Lanes)
            {
                totalItems += lanes.Value.Blocks.Count;

                if (totalItems > 10000)
                {
                    this._showEvaluation = false;
                    this._showTask = false;
                    this._showTarget = false;
                    this._showOther = false;
                    this._showNodes = false;
                    break;
                }
            }

            var sample = new TextBlock();
            sample.Text = "W";
            sample.Measure(new Size(10000, 10000));
            textHeight = sample.DesiredSize.Height;

            Draw();
        }

        /// <summary>
        /// Draw Graph 
        /// </summary>
        private void Draw()
        {
            TextBlocks.Clear();
            grid.Children.Clear();

            var lanesPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
            grid.Children.Add(lanesPanel);

            if (Timeline == null)
                return;

            // Compute number of pixel for one second, used by ruler
            OneSecondPixelWidth = ConvertTimeToPixel(TimeSpan.FromSeconds(1).Ticks);

            var keys = Timeline.Lanes.Keys.ToList();
            keys.Sort();
            /*
            var length = Math.Max(keys.Count(), keys.Last());
            Panel[] panels = new Panel[length];
            Parallel.ForEach(keys, (key) =>
            {
                var lane = Timeline.Lanes[key];
                var panel = CreatePanelForLane(lane, GlobalStartTime);
                panels[key] = panel;
            });
            */

            int showMeassurementMod = 0;

            foreach (var key in keys)
            {
                var lane = Timeline.Lanes[key];
                var panel = CreatePanelForLane(lane, GlobalStartTime);

                if (panel != null && panel.Children.Count > 0)
                {
                    if (ShowNodes || showMeassurementMod == 0)
                    {
                        var nodePanal = CreatePanelForNodeDivider(showMeassurementMod % 5 == 0);
                        lanesPanel.Children.Add(nodePanal);
                        showMeassurementMod++;
                    }

                    panel.HorizontalAlignment = HorizontalAlignment.Left;
                    lanesPanel.Children.Add(panel);
                }
            }
        }

        private Panel CreatePanelForNodeDivider(bool showTime)
        {
            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;

            var timeWidth = ConvertTimeToPixel(GlobalEndTime - GlobalStartTime);

            bool fiveSeconds = false;
            double gapWidth;
            if (OneSecondPixelWidth / textHeight < 3) {
                gapWidth = (5 * OneSecondPixelWidth / textHeight) - 0.1;
                fiveSeconds = true;
            } else
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

        private double ConvertTimeToPixel(double time)
        {
            return time / TimeToPixel / zoomRatio;
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

        private Panel CreatePanelForLane(Lane lane, long globalStart)
        {
            var blocks = lane.Blocks.Where(b =>
            {
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

            int level = 0;
            foreach (var endpoint in endpoints)
            {
                if (endpoint.IsStart)
                {
                    level++;
                    endpoint.Block.Indent = level;
                }
                else
                {
                    level--;
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

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;
            double canvasWidth = 0;
            double canvasHeight = 0;
            double minimumDurationToInclude = 1; // ignore duration is less than 1pixel

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

                double indentOffset = textHeight * (block.Indent - 1);

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
