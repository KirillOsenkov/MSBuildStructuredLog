using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public Build Build { get; set; }
        public TreeViewItem SelectedTreeViewItem { get; private set; }

        private TypingConcurrentOperation typingConcurrentOperation = new TypingConcurrentOperation();
        private ScrollViewer scrollViewer;

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
            typingConcurrentOperation.DisplayResults += results => DisplaySearchResults(results);
        }

        private void BreadCrumb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var node = breadCrumb.SelectedItem as TreeNode;
            if (node != null)
            {
                SelectItem(node);
                treeView.Focus();
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = treeView.SelectedItem;
            if (item != null)
            {
                UpdateBreadcrumb(item);
            }
        }

        public void UpdateBreadcrumb(object item)
        {
            var parentedNode = item as ParentedNode;
            var chain = parentedNode.GetParentChain().Skip(1).Concat(new[] { item });
            chain = IntersperseWithSeparators(chain).ToArray();
            breadCrumb.ItemsSource = chain;
            breadCrumb.SelectedIndex = -1;
        }

        private IEnumerable<object> IntersperseWithSeparators(IEnumerable<object> list)
        {
            bool first = true;
            foreach (var item in list)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    yield return new Separator();
                }

                yield return item;
            }
        }

        private void BuildControl_Loaded(object sender, RoutedEventArgs e)
        {
            scrollViewer = treeView.Template.FindName("_tv_scrollviewer_", treeView) as ScrollViewer;

            searchTextBox.Focus();
            if (!Build.Succeeded)
            {
                var firstError = Build.FindFirstInSubtreeIncludingSelf<Error>();
                if (firstError != null)
                {
                    SelectItem(firstError);
                    treeView.Focus();
                }
            }
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = resultsList.SelectedItem as ParentedNode;
            if (item != null)
            {
                SelectItem(item);
            }
        }

        private void SelectItem(ParentedNode item)
        {
            // skip the actual Build object and add the item itself
            var parentChain = item.GetParentChain().Skip(1).Concat(new[] { item });
            treeView.SelectContainerFromItem<object>(parentChain);
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Delete)
            {
                Delete();
                args.Handled = true;
            }
            else if (args.Key == Key.C && args.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                Copy();
                args.Handled = true;
            }
        }

        public void Delete()
        {
            var node = treeView.SelectedItem as TreeNode;
            if (node != null)
            {
                MoveSelectionOut(node);
                node.IsVisible = false;
            }
        }

        public void Copy()
        {
            var treeNode = treeView.SelectedItem;
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

        private void MoveSelectionOut(ParentedNode node)
        {
            var parent = node.Parent;
            if (parent == null)
            {
                return;
            }

            var next = parent.FindNextChild<BaseNode>(node);
            if (next != null)
            {
                node.IsSelected = false;
                next.IsSelected = true;
                return;
            }

            var previous = parent.FindPreviousChild<BaseNode>(node);
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

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = searchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                typingConcurrentOperation.Reset();
                DisplaySearchResults(null);
                return;
            }

            typingConcurrentOperation.Build = Build;
            typingConcurrentOperation.TextChanged(searchText);
        }

        private void DisplaySearchResults(IEnumerable<object> results)
        {
            if (results == null)
            {
                watermark.Visibility = Visibility.Visible;
            }
            else
            {
                watermark.Visibility = Visibility.Collapsed;
                if (!results.Any())
                {
                    results = new object[] { new Message { Text = "No results found." } };
                }
            }

            resultsList.ItemsSource = results;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var treeViewItem = (TreeViewItem)sender;

            if (PresentationSource.FromDependencyObject(treeViewItem) == null)
            {
                // the item might have disconnected by the time we run this
                return;
            }

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

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            SelectedTreeViewItem = e.OriginalSource as TreeViewItem;
        }
    }
}
