using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
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

            resultsList.ItemContainerStyle = treeViewItemStyle;
            resultsList.SelectedItemChanged += ResultsList_SelectionChanged;

            breadCrumb.SelectionChanged += BreadCrumb_SelectionChanged;

            Loaded += BuildControl_Loaded;
            typingConcurrentOperation.DisplayResults += results => DisplaySearchResults(results);
        }

        /// <summary>
        /// This is needed as a workaround for a weird bug. When the breadcrumb spans multiple lines
        /// and we click on an item on the first line, it truncates the breadcrumb up to that item.
        /// The fact that the breadcrumb moves down while the Mouse is captured results in a MouseMove
        /// in the ListBox, which triggers moving selection to top and selecting the first item.
        /// Without this "reentrancy" guard the event would be handled twice, with just the root
        /// of the chain left in the breadcrumb at the end.
        /// </summary>
        private bool isProcessingBreadcrumbClick = false;
        internal static TimeSpan Elapsed;

        private void BreadCrumb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isProcessingBreadcrumbClick)
            {
                return;
            }

            isProcessingBreadcrumbClick = true;
            var node = breadCrumb.SelectedItem as TreeNode;
            if (node != null)
            {
                SelectItem(node);
                treeView.Focus();
                e.Handled = true;
            }

            // turn it off only after the storm of layouts caused by the mouse click has subsided
            Dispatcher.InvokeAsync(() => { isProcessingBreadcrumbClick = false; }, DispatcherPriority.Background);
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = treeView.SelectedItem;
            if (item != null)
            {
                UpdateBreadcrumb(item);
            }
        }

        private void ResultsList_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var proxy = resultsList.SelectedItem as ProxyNode;
            if (proxy != null)
            {
                var item = proxy.Original as ParentedNode;
                if (item != null)
                {
                    SelectItem(item);
                }
            }
        }

        public void UpdateBreadcrumb(object item)
        {
            var parentedNode = item as ParentedNode;
            IEnumerable<object> chain = parentedNode.GetParentChain();
            if (chain == null || !chain.Any())
            {
                chain = new[] { item };
            }
            else
            {
                chain = IntersperseWithSeparators(chain).ToArray();
            }

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

                searchTextBox.Text = "$error";
            }
        }

        private void SelectItem(ParentedNode item)
        {
            var parentChain = item.GetParentChain();
            if (!parentChain.Any())
            {
                return;
            }

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

        private void DisplaySearchResults(IEnumerable<SearchResult> results)
        {
            if (results == null)
            {
                watermark.Visibility = Visibility.Visible;
            }
            else
            {
                watermark.Visibility = Visibility.Collapsed;
            }

            resultsList.ItemsSource = BuildResultTree(results);
        }

        private IEnumerable BuildResultTree(IEnumerable<SearchResult> results)
        {
            if (results == null)
            {
                return results;
            }

            var root = new Folder();

            // root.Children.Add(new Message { Text = "Elapsed " + Elapsed.ToString() });

            foreach (var result in results)
            {
                TreeNode parent = root;

                var parentedNode = result.Node as ParentedNode;
                if (parentedNode != null)
                {
                    var chain = parentedNode.GetParentChain();
                    var project = parentedNode.GetNearestParent<Project>();
                    if (project != null)
                    {
                        var projectProxy = root.GetOrCreateNodeWithName<ProxyNode>(project.Name);
                        projectProxy.Original = project;
                        if (projectProxy.Highlights.Count == 0)
                        {
                            projectProxy.Highlights.Add(project.Name);
                        }

                        parent = projectProxy;
                        parent.IsExpanded = true;
                    }
                }

                var proxy = new ProxyNode();
                proxy.Original = result.Node;
                proxy.Populate(result);
                parent.Children.Add(proxy);
            }

            if (!root.HasChildren)
            {
                root.Children.Add(new Message { Text = "No results found." });
            }

            return root.Children;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var treeViewItem = (TreeViewItem)sender;
            var treeView = (TreeView)typeof(TreeViewItem).GetProperty("ParentTreeView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(treeViewItem);

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
