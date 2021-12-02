using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class TimelineControl : UserControl
    {
        // Ratio of time reported to the smallest pixel to render.
        private const double TimeToPixel = 50000;

        public TimelineControl()
        {
            scaleTransform = new ScaleTransform();
            InitializeComponent();
            this.PreviewMouseWheel += TimelineControl_MouseWheel;
            grid.LayoutTransform = scaleTransform;
        }

        private double scaleFactor = 1;
        private double horizontalOffset = 0;
        private double verticalOffset = 0;

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
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

            if (e.Delta > 0)
            {
                if (scaleFactor < 4)
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

        public long GlobalStart;

        public void SetTimeline(Timeline timeline, long globalStart)
        {
            Timeline = timeline;
            GlobalStart = globalStart;

            DrawTimeline();
        }

        // Re-drawTimeline
        private void DrawTimeline()
        {
            grid.Children.Clear();
            var lanesPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
            grid.Children.Add(lanesPanel);

            foreach (var lane in Timeline.Lanes)
            {
                var panel = CreatePanelForLane(lane, GlobalStart);
                if (panel != null && panel.Children.Count > 0)
                {
                    panel.HorizontalAlignment = HorizontalAlignment.Left;
                    lanesPanel.Children.Add(panel);
                }
            }
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

        private Panel CreatePanelForLane(KeyValuePair<int, Lane> laneAndId, long globalStart)
        {
            var lane = laneAndId.Value;
            var blocks = lane.Blocks;
            if (blocks.Count == 0)
            {
                return null;
            }

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;

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

            endpoints.Sort((l, r) => {
                int timeCompare = l.Timestamp.CompareTo(r.Timestamp);
                if (timeCompare == 0)
                {
                    // Sometimes task and targets can start and end at the same time.
                    // Finish the existing node first
                    if (l.IsStart == r.IsStart)
                    {
                        // Favor Project >> Targets >> Task >> Others
                        int lValue, rValue;

                        switch (l.Block.Node)
                        {
                            case Project:
                                lValue = 3; break;
                            case Target:
                                lValue = 2; break;
                            case Task:
                                lValue = 1; break;
                            default:
                                lValue = 0;break;
                        }

                        switch (r.Block.Node)
                        {
                            case Project:
                                rValue = 3; break;
                            case Target:
                                rValue = 2; break;
                            case Task:
                                rValue = 1; break;
                            default:
                                rValue = 0; break;
                        }

                        if (rValue == lValue)
                        {
                            if (l.IsStart)
                                return l.Block.Text.CompareTo(r.Block.Text);
                            else
                                return r.Block.Text.CompareTo(l.Block.Text);
                        }

                        // ascending or decending edge
                        if (l.IsStart)
                            return rValue - lValue;
                        else
                            return lValue - rValue;
                    }

                    return l.IsStart ? 1 : -1;
                }
                return timeCompare;
                });

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

            var sample = new TextBlock();
            sample.Text = "W";
            sample.Measure(new Size(10000, 10000));
            var textHeight = sample.DesiredSize.Height;

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

                double left = (block.Start - globalStart) / TimeToPixel;
                double duration = (block.End - block.Start) / TimeToPixel;

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
            if (content.Content is TextBlock textBlock && textBlock.Tag is Block block)
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

        private static readonly Brush projectBackground = new SolidColorBrush(Color.FromArgb(40, 180, 180, 180));
        private static readonly Brush projectEvaluationBackground = new SolidColorBrush(Color.FromArgb(20, 100, 255, 150));
        private static readonly Brush targetBackground = new SolidColorBrush(Color.FromArgb(50, 255, 100, 255));
        private static readonly Brush taskBackground = new SolidColorBrush(Color.FromArgb(60, 100, 255, 255));
        private readonly ScaleTransform scaleTransform;

        private static Brush ChooseBackground(Block block)
        {
            switch (block.Node)
            {
                case Project _: return projectBackground;
                case ProjectEvaluation _: return projectEvaluationBackground;
                case Target _: return targetBackground;
                case Task _: return taskBackground;
            }

            return Brushes.Transparent;
        }

        private static Panel CreatePanelForLane2(KeyValuePair<int, Lane> lane)
        {
            var stackPanel = new StackPanel();

            foreach (var block in lane.Value.Blocks)
            {
                var textBlock = new TextBlock();
                textBlock.Text = block.Text;
                textBlock.Background = Brushes.AliceBlue;
                stackPanel.Children.Add(textBlock);
            }

            return stackPanel;
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            zoomSlider.Value = 1;
        }
    }
}
