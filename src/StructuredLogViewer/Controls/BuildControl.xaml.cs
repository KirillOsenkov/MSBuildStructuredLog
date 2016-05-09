using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public BuildControl(Build build)
        {
            InitializeComponent();
            DataContext = build;
            Build = build;

            var existingTreeViewItemStyle = (Style)Application.Current.Resources[typeof(TreeViewItem)];
            var treeViewItemStyle = new Style(typeof(TreeViewItem), existingTreeViewItemStyle);
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.VisibilityProperty, new Binding("IsVisible") { Mode = BindingMode.TwoWay, Converter = new BooleanToVisibilityConverter() }));
            treeViewItemStyle.Setters.Add(new EventSetter(MouseDoubleClickEvent, (MouseButtonEventHandler)OnItemDoubleClick));
            treeViewItemStyle.Setters.Add(new EventSetter(RequestBringIntoViewEvent, (RequestBringIntoViewEventHandler)TreeViewItem_RequestBringIntoView));
            //treeViewItemStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, contextMenu));

            treeView.ItemContainerStyle = treeViewItemStyle;
            treeView.KeyDown += TreeView_KeyDown;
            treeView.SelectedItemChanged += TreeView_SelectedItemChanged;

            resultsList.SelectionChanged += ResultsList_SelectionChanged;

            breadCrumb.SelectionChanged += BreadCrumb_SelectionChanged;

            Loaded += BuildControl_Loaded;
        }

        private void BreadCrumb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var node = breadCrumb.SelectedItem as TreeNode;
            if (node != null)
            {
                SelectItem(node);
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = treeView.SelectedItem as TreeNode;
            if (item != null)
            {
                UpdateBreadcrumb(item);
            }
        }

        private void UpdateBreadcrumb(TreeNode item)
        {
            breadCrumb.ItemsSource = item.GetParentChain().Skip(1).Concat(new[] { item });
        }

        private void BuildControl_Loaded(object sender, RoutedEventArgs e)
        {
            searchTextBox.Focus();
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = resultsList.SelectedItem as TreeNode;
            if (item != null)
            {
                SelectItem(item);
            }
        }

        private void SelectItem(TreeNode item)
        {
            // skip the actual Build object and add the item itself
            var parentChain = item.GetParentChain().Skip(1).Concat(new[] { item });
            treeView.SelectContainerFromItem(parentChain);
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Delete)
            {
                var node = treeView.SelectedItem as TreeNode;
                if (node != null)
                {
                    MoveSelectionOut(node);
                    node.IsVisible = false;
                    args.Handled = true;
                }
            }
            else if (args.Key == Key.C && args.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                CopyToClipboard(treeView.SelectedItem as TreeNode);
            }
        }

        private void CopyToClipboard(TreeNode treeNode)
        {
            if (treeNode == null)
            {
                return;
            }

            var text = StringWriter.GetString(treeNode);
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception)
            {
                // clipboard API is notoriously flaky
            }
        }

        private void MoveSelectionOut(TreeNode node)
        {
            var parent = node.Parent;
            if (parent == null)
            {
                return;
            }

            var next = node.FindNext<TreeNode>();
            if (next != null)
            {
                node.IsSelected = false;
                next.IsSelected = true;
                return;
            }

            var previous = node.FindPrevious<TreeNode>();
            if (previous != null)
            {
                node.IsSelected = false;
                previous.IsSelected = true;
            }
            else
            {
                node.IsSelected = false;
                parent.IsSelected = true;
            }
        }

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs args)
        {
            TreeNode treeNode = GetNode(args);
            if (treeNode != null)
            {
                // TODO: handle double-click on node
                args.Handled = true;
            }
        }

        private static TreeNode GetNode(RoutedEventArgs args)
        {
            var treeViewItem = args.Source as TreeViewItem;
            var treeNode = treeViewItem?.DataContext as TreeNode;
            return treeNode;
        }

        public Build Build { get; set; }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = searchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                resultsList.ItemsSource = null;
                watermark.Visibility = Visibility.Visible;
                return;
            }

            Search(searchText);
        }

        private void Search(string searchText)
        {
            var tree = treeView;
            var search = new Search(Build);
            var results = search.FindNodes(searchText);
            resultsList.ItemsSource = results;
            watermark.Visibility = Visibility.Collapsed;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var treeViewItem = (TreeViewItem)sender;
            var scrollViewer = treeView.Template.FindName("_tv_scrollviewer_", treeView) as ScrollViewer;

            Point topLeftInTreeViewCoordinates = treeViewItem.TransformToAncestor(treeView).Transform(new Point(0, 0));
            var treeViewItemTop = topLeftInTreeViewCoordinates.Y;
            if (treeViewItemTop < 0
                || treeViewItemTop + treeViewItem.ActualHeight > scrollViewer.ViewportHeight
                || treeViewItem.ActualHeight > scrollViewer.ViewportHeight)
            {
                // if the item is not visible or too "tall", don't do anything; let them scroll it into view
                return;
            }

            // if the item is already fully within the viewport vertically, disallow horizontal scrolling
            e.Handled = true;
        }
    }
}
