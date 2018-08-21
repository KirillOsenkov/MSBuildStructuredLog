﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Core.Timeline;

namespace StructuredLogViewer.Controls
{
    public partial class TimelineControl : UserControl
    {
        public TimelineControl()
        {
            scaleTransform = new ScaleTransform();
            InitializeComponent();
            this.PreviewMouseWheel += TimelineControl_MouseWheel;
            grid.LayoutTransform = scaleTransform;
        }

        private double scaleFactor = 1;

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

        private const double minimumZoom = 0.3;
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

            endpoints.Sort((l, r) => l.Timestamp.CompareTo(r.Timestamp));

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
                    textBlock.Text = $"{block.Text} ({Microsoft.Build.Logging.StructuredLogger.Utilities.DisplayDuration(block.Duration)})";
                    textBlock.Background = ChooseBackground(block);

                    double left = 24 * block.Indent;

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

            canvas.MouseMove += Canvas_MouseMove;

            return canvas;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed || 
                e.RightButton == MouseButtonState.Pressed)
            {
                return;
            }

            Canvas canvas = sender as Canvas;
            if (canvas == null)
            {
                return;
            }

            var hit = canvas.InputHitTest(e.GetPosition(canvas)) as TextBlock;
            if (hit != null)
            {
                HighlightTextBlock(hit);
            }
        }

        private TextBlock activeTextBlock = null;
        private Border highlight = new Border()
        {
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1)
        };

        private void HighlightTextBlock(TextBlock hit)
        {
            if (activeTextBlock == hit)
            {
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
                if (activeTextBlock.Parent is Panel parent)
                {
                    parent.Children.Add(highlight);
                    Canvas.SetLeft(highlight, Canvas.GetLeft(activeTextBlock));
                    Canvas.SetTop(highlight, Canvas.GetTop(activeTextBlock));
                    highlight.Width = activeTextBlock.ActualWidth;
                    highlight.Height = activeTextBlock.ActualHeight;
                }
            }
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
        private readonly ScaleTransform scaleTransform;

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

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            zoomSlider.Value = 1;
        }
    }
}
