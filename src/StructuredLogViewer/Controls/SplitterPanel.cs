 using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public class SplitterPanel : Grid
    {
        private readonly GridSplitter gridSplitter = new GridSplitter()
        {
            ResizeBehavior = GridResizeBehavior.PreviousAndNext
        };

        public Orientation Orientation
        {
            get
            {
                return (Orientation)GetValue(OrientationProperty);
            }

            set
            {
                SetValue(OrientationProperty, value);
                UpdateRowColumnInfo();
            }
        }

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                "Orientation",
                typeof(Orientation),
                typeof(SplitterPanel),
                new FrameworkPropertyMetadata(
                    Orientation.Horizontal,
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        private UIElement firstChild;
        public UIElement FirstChild
        {
            get
            {
                return firstChild;
            }

            set
            {
                firstChild = value;
                if (firstChild != null)
                {
                    UpdateRowColumnInfo();
                }
            }
        }

        private UIElement secondChild;
        public UIElement SecondChild
        {
            get
            {
                return secondChild;
            }

            set
            {
                secondChild = value;
                if (secondChild != null)
                {
                    UpdateRowColumnInfo();
                }
            }
        }

        private void UpdateRowColumnInfo()
        {
            Children.Clear();
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();

            if (Orientation == Orientation.Horizontal)
            {
                ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });
                ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5) });
                ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(3.0, GridUnitType.Star) });
                SetRow(gridSplitter, 0);
                SetColumn(gridSplitter, 1);
                gridSplitter.ResizeDirection = GridResizeDirection.Columns;
                gridSplitter.Width = 5;
            }
            else
            {
                RowDefinitions.Add(new RowDefinition());
                RowDefinitions.Add(new RowDefinition() { Height = new GridLength(5) });
                RowDefinitions.Add(new RowDefinition());
                SetRow(gridSplitter, 1);
                SetColumn(gridSplitter, 0);
                gridSplitter.ResizeDirection = GridResizeDirection.Rows;
                gridSplitter.Height = 5;
            }

            if (FirstChild != null)
            {
                SetRow(FirstChild, 0);
                SetColumn(FirstChild, 0);
                SetRowSpan(FirstChild, 1);
                SetColumnSpan(FirstChild, 1);
                Children.Add(FirstChild);
            }

            Children.Add(gridSplitter);

            if (SecondChild != null)
            {
                if (Orientation == Orientation.Horizontal)
                {
                    SetRow(SecondChild, 0);
                    SetColumn(SecondChild, 2);
                }
                else
                {
                    SetRow(SecondChild, 2);
                    SetColumn(SecondChild, 0);
                }

                SetRowSpan(SecondChild, 1);
                SetColumnSpan(SecondChild, 1);
                Children.Add(SecondChild);
            }
        }
    }
}
