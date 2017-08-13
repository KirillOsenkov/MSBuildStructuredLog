using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class TimelineControl : UserControl
    {
        public TimelineControl()
        {
            InitializeComponent();
        }

        public BuildControl BuildControl { get; set; }

        public void SetTimeline(Timeline timeline)
        {
            var lanesPanel = new StackPanel { Orientation = Orientation.Horizontal };
            grid.Children.Add(lanesPanel);

            foreach (var lane in timeline.Lanes)
            {
                var panel = CreatePanelForLane(lane);
                if (panel != null && panel.Children.Count > 0)
                {
                    lanesPanel.Children.Add(panel);
                }
            }
        }

        private Panel CreatePanelForLane(KeyValuePair<int, Lane> laneAndId)
        {
            var lane = laneAndId.Value;
            var blocks = lane.Blocks;
            if (blocks.Count == 0)
            {
                return null;
            }

            var canvas = new Canvas();
            canvas.VerticalAlignment = VerticalAlignment.Top;

            blocks.Sort((l, r) =>
            {
                var startDifference = l.StartTime.Ticks.CompareTo(r.StartTime.Ticks);
                if (startDifference != 0)
                {
                    return startDifference;
                }

                return l.Length.CompareTo(r.Length);
            });

            DateTime minDateTime = blocks[0].StartTime;
            DateTime maxDateTime = blocks[blocks.Count - 1].StartTime;

            foreach (var block in blocks)
            {
                block.Start = block.StartTime.Ticks;
                block.End = block.EndTime.Ticks;
            }

            double start = minDateTime.Ticks;
            double end = maxDateTime.Ticks;
            double totalDuration = end - start;
            if (totalDuration == 0)
            {
                totalDuration = 1;
            }

            double width = 0;

            var sample = new TextBlock();
            sample.Text = "W";
            sample.Measure(new Size(10000, 10000));
            var textHeight = sample.DesiredSize.Height;

            double preferredTotalHeight = textHeight * blocks.Count(b => b.Length > totalDuration / 2000);

            double currentHeight = 0;
            double totalHeight = 0;

            foreach (var block in blocks)
            {
                //if (block.Length > minimumDurationToInclude)
                {
                    var textBlock = new TextBlock();
                    textBlock.Text = $"{block.Text} ({Utilities.DisplayDuration(block.Duration)})";
                    textBlock.Background = ChooseBackground(block);

                    double left = 24 * (block.Indent - 1);

                    double top = (block.Start - start) / totalDuration * preferredTotalHeight;
                    double height = (block.End - block.Start) / totalDuration * preferredTotalHeight;
                    if (height < textHeight)
                    {
                        height = textHeight;
                        continue;
                    }

                    textBlock.Measure(new Size(10000, 10000));
                    double currentTotalWidth = left + textBlock.DesiredSize.Width;
                    if (currentTotalWidth > width)
                    {
                        width = currentTotalWidth;
                    }

                    double minimumTop = currentHeight;
                    if (minimumTop > top)
                    {
                        double adjustment = minimumTop - top;
                        if (height > adjustment + textHeight)
                        {
                            height = height - adjustment;
                            top = minimumTop;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    textBlock.Height = height;
                    textBlock.ToolTip = block.GetTooltip();
                    textBlock.MouseUp += TextBlock_MouseUp;
                    textBlock.Tag = block;

                    currentHeight = top + textHeight;

                    if (totalHeight < top + height)
                    {
                        totalHeight = top + height;
                    }

                    Canvas.SetLeft(textBlock, left);
                    Canvas.SetTop(textBlock, top);
                    canvas.Children.Add(textBlock);
                }
            }

            canvas.Height = totalHeight;
            canvas.Width = width;

            return canvas;
        }

        private void TextBlock_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is Block block && block.Node is ParentedNode node)
            {
                BuildControl.SelectItem(node);
            }
        }

        private static readonly Brush projectBackground = new SolidColorBrush(Color.FromArgb(10, 180, 180, 180));
        private static readonly Brush targetBackground = new SolidColorBrush(Color.FromArgb(20, 255, 100, 255));
        private static readonly Brush taskBackground = new SolidColorBrush(Color.FromArgb(30, 100, 255, 255));

        private static Brush ChooseBackground(Block block)
        {
            switch (block.Node)
            {
                case Project _: return projectBackground;
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
    }
}
