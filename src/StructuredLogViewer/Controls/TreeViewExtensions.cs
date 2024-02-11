using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StructuredLogViewer.Controls
{
    public static class TreeViewExtensions
    {
        public static readonly DependencyPropertyKey IsSelectionActivePropertyKey =
            (DependencyPropertyKey)typeof(Selector)
            .GetField("IsSelectionActivePropertyKey", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);

        public static void Virtualize(this TreeView treeView)
        {
            VirtualizingPanel.SetIsVirtualizing(treeView, true);
            VirtualizingPanel.SetVirtualizationMode(treeView, VirtualizationMode.Recycling);
        }

        public static Style CreateTreeViewItemStyleWithEvents<T, TTreeViewItem>(
            Action<T> itemDoubleClick = null,
            Action<T, KeyEventArgs> itemKeyUp = null,
            Action<T> itemLeftButtonUp = null,
            ContextMenu contextMenu = null,
            Binding tooltipBinding = null)
            where T : class
        {
            var existingTreeViewItemStyle = (Style)Application.Current.Resources[typeof(TTreeViewItem)];
            var treeViewItemStyle = new Style(typeof(TTreeViewItem), existingTreeViewItemStyle);

            treeViewItemStyle.Setters.Add(new EventSetter(FrameworkElement.ContextMenuOpeningEvent, (ContextMenuEventHandler)OnContextMenuOpening));
            treeViewItemStyle.Setters.Add(new EventSetter(FrameworkElement.ContextMenuClosingEvent, (ContextMenuEventHandler)OnContextMenuClosing));
            treeViewItemStyle.Setters.Add(new EventSetter(UIElement.PreviewMouseRightButtonDownEvent, (MouseButtonEventHandler)OnPreviewMouseRightButtonDown));
            treeViewItemStyle.Setters.Add(new EventSetter(FrameworkElement.RequestBringIntoViewEvent, (RequestBringIntoViewEventHandler)OnRequestBringIntoView));

            if (itemLeftButtonUp != null)
            {
                treeViewItemStyle.Setters.Add(new EventSetter(UIElement.MouseLeftButtonUpEvent, (MouseButtonEventHandler)((s, e) => OnMouseLeftButtonUp(e, itemLeftButtonUp))));
            }

            if (itemKeyUp != null)
            {
                treeViewItemStyle.Setters.Add(new EventSetter(UIElement.KeyUpEvent, (KeyEventHandler)((s, e) => OnItemKeyUp(e, itemKeyUp))));
            }

            if (itemDoubleClick != null)
            {
                treeViewItemStyle.Setters.Add(new EventSetter(Control.MouseDoubleClickEvent, (MouseButtonEventHandler)((s, e) => OnItemDoubleClick(s, e, itemDoubleClick))));
            }

            if (contextMenu != null)
            {
                treeViewItemStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, contextMenu));
            }

            if (tooltipBinding != null)
            {
                treeViewItemStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, tooltipBinding));
            }

            return treeViewItemStyle;
        }

        private static void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var treeViewItem = GetTreeViewItem(sender);
            if (treeViewItem == null || PresentationSource.FromDependencyObject(treeViewItem) == null)
            {
                // the item might have disconnected by the time we run this
                return;
            }

            var treeView = treeViewItem.GetTreeView();
            if (treeView == null)
            {
                return;
            }

            var scrollViewer =
                // sometimes the name is 1_T instead? see #758
                treeView.Template.FindName("_tv_scrollviewer_", treeView) as ScrollViewer ??
                treeView.Template.FindName("1_T", treeView) as ScrollViewer ??
                treeView.FindVisualChild<ScrollViewer>() ??
                // a treeview might not have a scrollviewer, so look for the ambient scrollviewer
                treeView.FindAncestor<ScrollViewer>();

            double viewportHeight = treeView.ActualHeight;

            // scrollViewer can be null sometimes, see issue https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/758
            if (scrollViewer != null)
            {
                viewportHeight = scrollViewer.ViewportHeight;
            }

            Point topLeftInTreeViewCoordinates = treeViewItem.TransformToAncestor(treeView).Transform(new Point(0, 0));
            var itemTop = topLeftInTreeViewCoordinates.Y;
            var itemHeight = treeViewItem.ActualHeight;
            if (itemTop < 0
                || itemTop + itemHeight > viewportHeight
                || itemHeight > viewportHeight)
            {
                // if the item is not visible or too "tall", don't do anything; let them scroll it into view
                return;
            }

            // if the item is already fully within the viewport vertically, disallow horizontal scrolling
            e.Handled = true;
        }

        public static void SubscribeToItemEvents<T, TTreeViewItem>(
            this TreeView treeView,
            Action<T> itemDoubleClick = null,
            Action<T, KeyEventArgs> itemKeyUp = null,
            Action<T> itemLeftButtonUp = null,
            ContextMenu contextMenu = null)
            where T : class
        {
            treeView.ItemContainerStyle = CreateTreeViewItemStyleWithEvents<T, TTreeViewItem>(
                itemDoubleClick,
                itemKeyUp,
                itemLeftButtonUp,
                contextMenu);
            treeView.TextInput += TreeView_TextInput;
        }

        private static void TreeView_TextInput(object sender, TextCompositionEventArgs e)
        {
            ((TreeView)sender).SelectItemByKey(e.Text);
        }

        private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var treeViewItem = GetTreeViewItem(sender);
            if (treeViewItem != null)
            {
                ContextMenuUtilities.SetIsContextMenuOpen(treeViewItem, true);
            }
        }

        private static void OnContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            var treeViewItem = GetTreeViewItem(sender);
            if (treeViewItem != null)
            {
                ContextMenuUtilities.SetIsContextMenuOpen(treeViewItem, false);
            }
        }

        private static void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = GetTreeViewItem(e.OriginalSource);
            if (treeViewItem != null)
            {
                treeViewItem.IsSelected = true;
            }
        }

        private static void OnMouseLeftButtonUp<T>(MouseButtonEventArgs args, Action<T> action)
            where T : class
        {
            var treeNode = GetSelectedTreeNode<T>(args);
            if (treeNode != null)
            {
                try
                {
                    action(treeNode);
                }
                catch
                {
                }

                args.Handled = true;
            }
        }

        private static void OnItemKeyUp<T>(KeyEventArgs args, Action<T, KeyEventArgs> action)
            where T : class
        {
            var treeNode = GetSelectedTreeNode<T>(args);
            if (treeNode != null)
            {
                try
                {
                    if (args.Key == Key.Apps)
                    {
                        var treeViewItem = GetTreeViewItem(args.Source);
                        if (treeViewItem != null)
                        {
                            var contextMenu = treeViewItem.ContextMenu;
                            if (contextMenu != null)
                            {
                                contextMenu.Placement = PlacementMode.Bottom;
                                contextMenu.PlacementTarget = treeViewItem.GetHeaderControl() as UIElement ?? treeViewItem;
                                contextMenu.IsOpen = true;
                                args.Handled = true;
                            }
                        }
                    }

                    action(treeNode, args);
                }
                catch
                {
                }

                args.Handled = true;
            }
        }

        private static void OnItemDoubleClick<T>(object sender, MouseButtonEventArgs args, Action<T> action)
            where T : class
        {
            if (args.Handled || args.ChangedButton != MouseButton.Left)
            {
                return;
            }

            // workaround for http://stackoverflow.com/a/36244243/37899
            var treeViewItem = sender as TreeViewItem;
            if (!treeViewItem.IsSelected)
            {
                return;
            }

            if (args.OriginalSource is FrameworkElement frameworkElement &&
                (frameworkElement is Path || frameworkElement is Border) &&
                frameworkElement.TemplatedParent is ToggleButton)
            {
                // let's not count the double-click on the expander as the double-click on the TreeViewItem
                return;
            }

            var treeNode = GetSelectedTreeNode<T>(args);
            if (treeNode != null)
            {
                try
                {
                    action(treeNode);
                }
                catch
                {
                }

                args.Handled = true;
            }
        }

        private static TreeViewItem GetTreeViewItem(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            return instance as TreeViewItem ??
                (instance as FrameworkElement)?.TemplatedParent as TreeViewItem ??
                VisualTreeUtilities.FindAncestor<TreeViewItem>(instance as UIElement);
        }

        private static T GetSelectedTreeNode<T>(RoutedEventArgs args)
            where T : class
        {
            var treeViewItem = GetTreeViewItem(args.Source);
            if (treeViewItem != null && treeViewItem.IsSelected)
            {
                return treeViewItem.DataContext as T;
            }

            return default;
        }

        /// <summary>
        /// Selects an item in a (possibly virtualized) TreeView using a custom item chain
        /// </summary>
        /// <typeparam name="T">The type of the items present in the control and the chain</typeparam>
        /// <param name="treeView">The TreeView to select an item in</param>
        /// <param name="items">The chain of items to walk. The last item in the chain will be selected</param>
        public static void SelectContainerFromItem<T>(this TreeView treeView, IEnumerable<T> items)
            where T : class
        {
            // Use a default compare method with the '==' operator
            treeView.SelectContainerFromItem(items, (x, y) => x.Equals(y));
        }

        /// <summary>
        /// Selects an item in a TreeView using a custom item chain, an item comparison method,
        /// and an item conversion method.
        /// </summary>
        /// <typeparam name="T">The type of the items present in the control and the chain</typeparam>
        /// <param name="treeView">The TreeView to select an item in</param>
        /// <param name="items">The chain of items to walk. The last item in the chain will be selected</param>
        /// <param name="compareMethod">The method used to compare items in the control with items in the chain</param>
        public static void SelectContainerFromItem<T>(
            this TreeView treeView,
            IEnumerable<T> items,
            Func<T, T, bool> compareMethod)
        {
            // attempt to fix https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/453
            items = items.ToArray();

            SelectContainerFromItem(
                treeView,
                new SelectInfo<T>()
                {
                    Items = items,
                    CompareMethod = compareMethod,
                    SelectItem = delegate (ItemsControl container, SelectInfo<T> info)
                    {
                        var treeItem = (TreeViewItem)container;
                        if (treeItem == null)
                        {
                            return;
                        }

                        treeItem.IsSelected = true;

                        var header = treeItem.GetHeaderControl();
                        if (header == null || !header.IsOnScreen(treeView))
                        {
                            treeItem.BringIntoView();
                        }
                    },
                    NeedMoreItems = delegate (ItemsControl container, SelectInfo<T> info)
                    {
                        if (container is TreeViewItem treeViewItem)
                        {
                            treeViewItem.IsExpanded = true;
                        }
                    }
                },
                treeView
            );
        }

        /// <summary>
        /// Selects the UI element in the specified <i>control</i> based on the corresponding <i>info</i>.
        /// </summary>
        /// <typeparam name="T">The type of item to match.</typeparam>
        /// <param name="container">The <see cref="ItemsControl"/> in which to dumpster dive.</param>
        /// <param name="info">Control parameters used in the search.</param>
        private static void SelectContainerFromItem<T>(this ItemsControl container, SelectInfo<T> selectInfo, TreeView treeView)
        {
            var currentItem = selectInfo.Items.First();
            var itemContainerGenerator = container.ItemContainerGenerator;

            if (itemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                // If the item containers haven't been generated yet, attach an event
                // and wait for the status to change.
                EventHandler selectWhenReadyMethod = null;

                selectWhenReadyMethod = (ds, de) =>
                {
                    if (itemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    {
                        // Stop listening for status changes on this container
                        itemContainerGenerator.StatusChanged -= selectWhenReadyMethod;

                        // Attempt to fix https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/453
                        container.Dispatcher.BeginInvoke((Action)delegate
                        {
                            // Search the container for the item chain
                            SelectContainerFromItem(container, selectInfo, treeView);
                        }, DispatcherPriority.Render);
                    }
                };

                itemContainerGenerator.StatusChanged += selectWhenReadyMethod;

                return;
            }

            Debug.Assert(itemContainerGenerator.Status == GeneratorStatus.ContainersGenerated);

            // Compare each item in the container and look for the next item
            // in the chain.
            foreach (object item in container.Items)
            {
                var convertedItem = default(T);

                // Convert the item if a conversion method exists. Otherwise
                // just cast the item to the desired type.
                if (selectInfo.ConvertMethod != null)
                {
                    convertedItem = selectInfo.ConvertMethod(item);
                }
                else
                {
                    convertedItem = (T)item;
                }

                // Compare the converted item with the item in the chain
                if (selectInfo.CompareMethod != null && selectInfo.CompareMethod(convertedItem, currentItem))
                {
                    // Since the TreeViewItems are in a virtualized panel, the item to be selected may not be realized,
                    // need to ensure it is brought into view, so it can be selected.
                    ItemsPresenter itemsPresenter = container.FindVisualChild<ItemsPresenter>();
                    var containerFromItem = itemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (itemsPresenter != null && (containerFromItem == null || !containerFromItem.IsOnScreen(treeView)))
                    {
                        int index = container.Items.IndexOf(currentItem);

                        var child = VisualTreeHelper.GetChild(itemsPresenter, 0);
                        var virtualizingPanel = child as VirtualizingStackPanel;
                        if (virtualizingPanel != null)
                        {
                            virtualizingPanel.BringIndexIntoViewPublic(index);
                        }
                        else
                        {
                            if (child is StackPanel stackPanel)
                            {
                                if (stackPanel.Children[index] is FrameworkElement frameworkElement)
                                {
                                    frameworkElement.BringIntoView();
                                }
                            }
                        }
                    }

                    var containerParent = (ItemsControl)itemContainerGenerator.ContainerFromItem(item);
                    if (containerParent == null)
                    {
                        return;
                    }

                    // Replace with the remaining items in the chain
                    selectInfo.Items = selectInfo.Items.Skip(1);

                    // If no items are left in the chain, then we're finished
                    if (selectInfo.Items.Count() == 0)
                    {
                        // Select the last item
                        if (selectInfo.SelectItem != null)
                        {
                            Action action = new Action(() =>
                            {
                                selectInfo.SelectItem(containerParent, selectInfo);
                            });

                            // Here we dispatch the select action so the TreeViewItem is focused
                            // and scrolled into view correctly.
                            container.Dispatcher.BeginInvoke(action, DispatcherPriority.Render);
                        }
                    }
                    else
                    {
                        // Request more items and continue the search
                        if (selectInfo.NeedMoreItems != null)
                        {
                            selectInfo.NeedMoreItems(containerParent, selectInfo);
                            SelectContainerFromItem(containerParent, selectInfo, treeView);
                        }
                    }

                    break;
                }
            }
        }

        public static void ExpandAll(this TreeView treeView)
        {
            foreach (object item in treeView.Items)
            {
                TreeViewItem treeItem = treeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeItem != null)
                {
                    treeItem.ExpandAll();
                    treeItem.IsExpanded = true;
                }
            }
        }

        public static TreeViewItem GetTreeViewItemFromChild(this TreeView treeView, DependencyObject child)
        {
            if (child != null)
            {
                if (child is TreeViewItem)
                {
                    return child as TreeViewItem;
                }
                else if (child is TreeView)
                {
                    return null; //we've walked through the tree. The source of the click is not something else
                }
                else
                {
                    return GetTreeViewItemFromChild(treeView, VisualTreeHelper.GetParent(child));
                }
            }

            return null;
        }

        /// <summary>
        /// Clear any selected tree view items within the specified tree view.
        /// </summary>
        public static void ClearTreeViewSelection(this TreeView treeView)
        {
            if (treeView != null)
            {
                ClearTreeViewItemsControlSelection(treeView.Items, treeView.ItemContainerGenerator);
            }
        }

        /// <summary>
        /// Recursively walks through the specified tree and de-selects any selected tree view items.
        /// </summary>
        private static void ClearTreeViewItemsControlSelection(this ItemCollection ic, ItemContainerGenerator icg)
        {
            if ((ic != null) && (icg != null))
            {
                for (int i = 0; i < ic.Count; i++)
                {
                    TreeViewItem tvi = icg.ContainerFromIndex(i) as TreeViewItem;

                    if (tvi != null)
                    {
                        ClearTreeViewItemsControlSelection(tvi.Items, tvi.ItemContainerGenerator);
                        tvi.IsSelected = false;
                    }
                }
            }
        }

        public static TreeViewItem GetTreeViewItemFromParentChain(this TreeView treeView, IReadOnlyList<object> itemChain)
        {
            ItemsControl current = treeView;

            for (int i = 0; i < itemChain.Count; i++)
            {
                var item = itemChain[i];
                var container = current.ItemContainerGenerator.ContainerFromItem(item);
                if (container == null)
                {
                    return null;
                }

                if (container is not TreeViewItem treeViewItem)
                {
                    return null;
                }

                if (i == itemChain.Count - 1)
                {
                    return treeViewItem;
                }

                current = treeViewItem;
            }

            return null;
        }

        private static readonly FieldInfo _selectedContainerField =
            typeof(TreeView).GetField("_selectedContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        // This is marked as an "undocumented API", see issue 1701
        //private static readonly MethodInfo TryGetHeaderElementMethod =
        //    typeof(TreeViewItem).GetMethod("TryGetHeaderElement", BindingFlags.Instance | BindingFlags.NonPublic);

        public static TreeViewItem GetSelectedTreeViewItem(this TreeView treeView)
        {
            var item = _selectedContainerField.GetValue(treeView) as TreeViewItem;
            return item;
        }

        public static TreeViewItem GetTreeViewItem(this TreeView treeView, object item)
        {
            if (item == null)
            {
                return null;
            }

            var container = treeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            return container;
        }

        public static bool FocusSelectedItem(this TreeView treeView, Action<TreeViewItem> focuser)
        {
            var treeViewItem = GetSelectedTreeViewItem(treeView);
            if (treeViewItem == null)
            {
                return false;
            }

            focuser?.Invoke(treeViewItem);
            return true;
        }

        public static bool FocusSelectedItem(this TreeView treeView)
        {
            return FocusSelectedItem(treeView, i => Keyboard.Focus(i));
        }

        public static void ScrollSelectedItemIntoView(this TreeView treeView)
        {
            var selectedItem = treeView.GetSelectedTreeViewItem();
            if (selectedItem == null)
            {
                return;
            }

            selectedItem.ScrollItemIntoView();
        }

        public static void EnableScrollToSelection(this TreeView treeView)
        {
            treeView.SelectedItemChanged += (s, e) =>
            {
                treeView.ScrollSelectedItemIntoView();
            };
        }

        public static bool ScrollPage(this TreeView treeView, bool up)
        {
            var scrollViewer = treeView.FindAncestor<ScrollViewer>();
            if (scrollViewer == null)
            {
                return false;
            }

            var selectedItem = treeView.GetSelectedTreeViewItem();
            if (selectedItem == null)
            {
                return false;
            }

            var oldOffset = scrollViewer.VerticalOffset;

            if (up)
            {
                scrollViewer.PageUp();
            }
            else
            {
                scrollViewer.PageDown();
            }

            scrollViewer.UpdateLayout();

            var newOffset = scrollViewer.VerticalOffset;
            if (AreClose(oldOffset, newOffset))
            {
                TreeViewItem lastItem;
                if (up)
                {
                    lastItem = selectedItem.FindFirstItem();
                }
                else
                {
                    lastItem = selectedItem.FindLastItem();
                }

                if (lastItem != null && lastItem != selectedItem)
                {
                    Keyboard.Focus(lastItem);
                    return true;
                }

                return false;
            }

            TreeViewItem newVisibleItem;
            if (up)
            {
                newVisibleItem = selectedItem.FindFirstVisibleItem(scrollViewer, up: true);
            }
            else
            {
                newVisibleItem = selectedItem.FindFirstVisibleItem(scrollViewer, up: false);
            }

            if (newVisibleItem != null)
            {
                Keyboard.Focus(newVisibleItem);
            }

            return true;
        }

        /// <summary>
        /// AreClose - Returns whether or not two doubles are "close".  That is, whether or 
        /// not they are within epsilon of each other.  Note that this epsilon is proportional
        /// to the numbers themselves to that AreClose survives scalar multiplication.
        /// There are plenty of ways for this to return false even for numbers which
        /// are theoretically identical, so no code calling this should fail to work if this 
        /// returns false.  This is important enough to repeat:
        /// NB: NO CODE CALLING THIS FUNCTION SHOULD DEPEND ON ACCURATE RESULTS - this should be
        /// used for optimizations *only*.
        /// </summary>
        /// <returns>
        /// bool - the result of the AreClose comparision.
        /// </returns>
        /// <param name="value1"> The first double to compare. </param>
        /// <param name="value2"> The second double to compare. </param>
        private static bool AreClose(double value1, double value2)
        {
            //in case they are Infinities (then epsilon check does not work)
            if (value1 == value2)
            {
                return true;
            }

            // This computes (|value1-value2| / (|value1| + |value2| + 10.0)) < DBL_EPSILON
            double eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * TreeViewItemExtensions.DBL_EPSILON;
            double delta = value1 - value2;
            return (-eps < delta) && (eps > delta);
        }

        public static void SelectFirstVisibleItem(this TreeView treeView)
        {
            var firstItem = GetFirstItem(treeView);
            if (firstItem == null)
            {
                return;
            }

            var firstVisible = firstItem.EnumerateAllTreeItemsInALoop().FirstOrDefault(i => i.IsVisible);
            if (firstVisible != null)
            {
                firstVisible.IsSelected = true;
            }
        }

        public static TreeViewItem GetFirstItem(this TreeView treeView)
        {
            var firstItem = treeView.Items.OfType<object>().FirstOrDefault();
            if (firstItem == null)
            {
                return null;
            }

            return treeView.GetTreeViewItem(firstItem);
        }

        public static bool IsFirstItemSelected(this TreeView treeView)
        {
            var selectedItem = treeView.GetSelectedTreeViewItem();
            if (selectedItem == null)
            {
                return false;
            }

            var previous = selectedItem.GetPreviousItemInTraversalOrder();
            if (previous == null)
            {
                return true;
            }

            return false;
        }

        public static bool IsFirstVisibleItemSelected(this TreeView treeView)
        {
            var selectedItem = treeView.GetSelectedTreeViewItem();
            if (selectedItem == null)
            {
                return false;
            }

            var previous = selectedItem.GetPreviousVisibleItemInTraversalOrder();
            if (previous == null)
            {
                return true;
            }

            return false;
        }

        public static void SelectItemByKey(this TreeView treeView, string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return;
            }

            var selectedItem = treeView.GetSelectedTreeViewItem();
            if (selectedItem == null)
            {
                return;
            }

            foreach (var ch in input)
            {
                SelectItemByKey(selectedItem, ch);
            }
        }

        private static readonly DependencyProperty CharacterMatchPrefixLength
            = DependencyProperty.RegisterAttached(
                nameof(CharacterMatchPrefixLength),
                typeof(int),
                typeof(ItemsControl),
                new FrameworkPropertyMetadata(0));

        private static void SelectItemByKey(TreeViewItem selectedTreeViewItem, char ch)
        {
            var propertyOwner = selectedTreeViewItem.GetTreeView();

            int characterMatchPrefixLength = (int)propertyOwner.GetValue(CharacterMatchPrefixLength);

            var selectedText = GetSearchText(selectedTreeViewItem);
            var prefix = selectedText.Substring(0, Math.Min(characterMatchPrefixLength, selectedText.Length));

            var items = selectedTreeViewItem.EnumerateAllTreeItemsInALoop().ToArray();

        search:
            foreach (var item in items)
            {
                var text = GetSearchText(item);
                if (characterMatchPrefixLength < text.Length && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var character = text[characterMatchPrefixLength];
                    if (char.ToLowerInvariant(character) == ch)
                    {
                        characterMatchPrefixLength++;
                        propertyOwner.SetValue(CharacterMatchPrefixLength, characterMatchPrefixLength);
                        Keyboard.Focus(item);
                        return;
                    }
                }
            }

            if (characterMatchPrefixLength > 0)
            {
                characterMatchPrefixLength = 0;
                prefix = "";
                items = items.Skip(1).Concat(items.Take(1)).ToArray();
                goto search;
            }
        }

        private static string GetSearchText(this TreeViewItem treeViewItem)
        {
            if (treeViewItem.DataContext is object dataContext)
            {
                return dataContext.ToString();
            }

            return treeViewItem.Header?.ToString();
        }
    }

    public class SelectInfo<T>
    {
        /// <summary>
        /// Gets or sets the chain of items to search for. The last item in the chain will be selected.
        /// </summary>
        public IEnumerable<T> Items { get; set; }

        /// <summary>
        /// Gets or sets the method used to compare items in the control with items in the chain
        /// </summary>
        public Func<T, T, bool> CompareMethod { get; set; }

        /// <summary>
        /// Gets or sets the method used to convert items in the control to be compare with items in the chain
        /// </summary>
        public Func<object, T> ConvertMethod { get; set; }

        /// <summary>
        /// Gets or sets the method used to select the final item in the chain
        /// </summary>
        public SelectEventHandler<T> SelectItem { get; set; }

        /// <summary>
        /// Gets or sets the method used to request more child items to be generated in the control
        /// </summary>
        public SelectEventHandler<T> NeedMoreItems { get; set; }
    }

    public delegate void SelectEventHandler<T>(ItemsControl container, SelectInfo<T> info);
}