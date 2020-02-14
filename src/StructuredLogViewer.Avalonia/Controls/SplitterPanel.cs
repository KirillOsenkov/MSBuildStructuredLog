using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class SplitterPanel : Grid
    {
        public GridLength FirstChildRelativeSize
        {
            get { return GetValue(FirstChildRelativeSizeProperty); }
            set { SetValue(FirstChildRelativeSizeProperty, value); }
        }

        public static readonly StyledProperty<GridLength> FirstChildRelativeSizeProperty =
            AvaloniaProperty.Register<SplitterPanel, GridLength>(nameof(FirstChildRelativeSize), new GridLength(1, GridUnitType.Star));

        public GridLength SecondChildRelativeSize
        {
            get { return GetValue(SecondChildRelativeSizeProperty); }
            set { SetValue(SecondChildRelativeSizeProperty, value); }
        }

        public static readonly StyledProperty<GridLength> SecondChildRelativeSizeProperty =
            AvaloniaProperty.Register<SplitterPanel, GridLength>(nameof(SecondChildRelativeSize), new GridLength(1, GridUnitType.Star));

        private readonly GridSplitter gridSplitter = new GridSplitter();

        public Orientation Orientation
        {
            get { return GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<SplitterPanel, Orientation>(nameof(Orientation), Orientation.Horizontal);

        static SplitterPanel()
        {
            AffectsMeasure<SplitterPanel>(OrientationProperty);
        }

        private Control firstChild;
        public Control FirstChild
        {
            get { return firstChild; }
            set
            {
                if (firstChild != null)
                {
                    firstChild.PropertyChanged -= OnChildIsVisibleChanged;
                }

                firstChild = value;
                if (firstChild != null)
                {
                    UpdateRowColumnInfo();
                    firstChild.PropertyChanged += OnChildIsVisibleChanged;
                }
            }
        }

        private Control secondChild;
        public Control SecondChild
        {
            get { return secondChild; }
            set
            {
                if (secondChild != null)
                {
                    secondChild.PropertyChanged -= OnChildIsVisibleChanged;
                }

                secondChild = value;
                if (secondChild != null)
                {
                    UpdateRowColumnInfo();
                    secondChild.PropertyChanged += OnChildIsVisibleChanged;
                }
            }
        }

        private void OnChildIsVisibleChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty)
            {
                UpdateSplitterVisibility();
            }
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
        private static readonly GridLength zero = new GridLength(0);
        private static readonly GridLength separatorSize = new GridLength(5);

        private void UpdateSplitterVisibility()
        {
            bool isFirstChildVisible = FirstChild != null && FirstChild.IsVisible;
            bool isSecondChildVisible = SecondChild != null && SecondChild.IsVisible;
            bool areBothVisible = isFirstChildVisible && isSecondChildVisible;
            gridSplitter.IsVisible = areBothVisible;

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
