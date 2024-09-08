using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public class SplitterPanel : Grid
    {
        public GridLength FirstChildRelativeSize
        {
            get { return (GridLength)GetValue(FirstChildRelativeSizeProperty); }
            set { SetValue(FirstChildRelativeSizeProperty, value); }
        }

        public static readonly DependencyProperty FirstChildRelativeSizeProperty =
            DependencyProperty.Register("FirstChildRelativeSize", typeof(GridLength), typeof(SplitterPanel), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

        public GridLength SecondChildRelativeSize
        {
            get { return (GridLength)GetValue(SecondChildRelativeSizeProperty); }
            set { SetValue(SecondChildRelativeSizeProperty, value); }
        }

        public static readonly DependencyProperty SecondChildRelativeSizeProperty =
            DependencyProperty.Register("SecondChildRelativeSize", typeof(GridLength), typeof(SplitterPanel), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

        private readonly GridSplitter gridSplitter = new()
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
                if (firstChild != null)
                {
                    firstChild.IsVisibleChanged -= OnChildIsVisibleChanged;
                }

                firstChild = value;
                if (firstChild != null)
                {
                    UpdateRowColumnInfo();
                    firstChild.IsVisibleChanged += OnChildIsVisibleChanged;
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
                if (secondChild != null)
                {
                    secondChild.IsVisibleChanged -= OnChildIsVisibleChanged;
                }

                secondChild = value;
                if (secondChild != null)
                {
                    UpdateRowColumnInfo();
                    secondChild.IsVisibleChanged += OnChildIsVisibleChanged;
                }
            }
        }

        private void OnChildIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateSplitterVisibility();
        }

        private void UpdateRowColumnInfo()
        {
            Children.Clear();
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();

            if (oldFirstSize == default(GridLength))
            {
                oldFirstSize = FirstChildRelativeSize;
            }

            if (oldSecondSize == default(GridLength))
            {
                oldSecondSize = SecondChildRelativeSize;
            }

            if (Orientation == Orientation.Horizontal)
            {
                ColumnDefinitions.Add(new ColumnDefinition() { Width = oldFirstSize });
                ColumnDefinitions.Add(new ColumnDefinition() { Width = separatorSize });
                ColumnDefinitions.Add(new ColumnDefinition() { Width = oldSecondSize });
                RowDefinitions.Add(new RowDefinition());
                SetRow(gridSplitter, 0);
                SetColumn(gridSplitter, 1);
                gridSplitter.ResizeDirection = GridResizeDirection.Columns;
                gridSplitter.Width = 5;
                gridSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                gridSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                RowDefinitions.Add(new RowDefinition() { Height = oldFirstSize });
                RowDefinitions.Add(new RowDefinition() { Height = separatorSize });
                RowDefinitions.Add(new RowDefinition() { Height = oldSecondSize });
                ColumnDefinitions.Add(new ColumnDefinition());
                SetRow(gridSplitter, 1);
                SetColumn(gridSplitter, 0);
                gridSplitter.ResizeDirection = GridResizeDirection.Rows;
                gridSplitter.Height = 5;
                gridSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                gridSplitter.VerticalAlignment = VerticalAlignment.Stretch;
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

            UpdateSplitterVisibility();
        }

        private GridLength oldFirstSize;
        private GridLength oldSecondSize;
        private static readonly GridLength zero = new(0);
        private static readonly GridLength separatorSize = new(5);

        private void UpdateSplitterVisibility()
        {
            bool isFirstChildVisible = FirstChild != null && FirstChild.IsVisible;
            bool isSecondChildVisible = SecondChild != null && SecondChild.IsVisible;
            bool areBothVisible = isFirstChildVisible && isSecondChildVisible;
            gridSplitter.Visibility = areBothVisible ? Visibility.Visible : Visibility.Collapsed;

            if (Orientation == Orientation.Horizontal)
            {
                if (ColumnDefinitions.Count == 3)
                {
                    if (isFirstChildVisible)
                    {
                        if (oldFirstSize == default(GridLength) || oldFirstSize == zero)
                        {
                            oldFirstSize = FirstChildRelativeSize;
                        }

                        if (ColumnDefinitions[0].Width == zero)
                        {
                            ColumnDefinitions[0].Width = oldFirstSize;
                        }
                    }
                    else
                    {
                        if (ColumnDefinitions[0].Width != zero)
                        {
                            oldFirstSize = ColumnDefinitions[0].Width;
                        }

                        ColumnDefinitions[0].Width = zero;
                    }

                    if (areBothVisible)
                    {
                        ColumnDefinitions[1].Width = separatorSize;
                    }
                    else
                    {
                        ColumnDefinitions[1].Width = zero;
                    }

                    if (isSecondChildVisible)
                    {
                        if (oldSecondSize == default(GridLength) || oldSecondSize == zero)
                        {
                            oldSecondSize = SecondChildRelativeSize;
                        }

                        if (ColumnDefinitions[2].Width == zero)
                        {
                            ColumnDefinitions[2].Width = oldSecondSize;
                        }
                    }
                    else
                    {
                        if (ColumnDefinitions[2].Width != zero)
                        {
                            oldSecondSize = ColumnDefinitions[2].Width;
                        }

                        ColumnDefinitions[2].Width = zero;
                    }
                }
            }
        }
    }
}
