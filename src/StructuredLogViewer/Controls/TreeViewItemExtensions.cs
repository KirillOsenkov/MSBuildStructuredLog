using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public static class TreeViewItemExtensions
    {
        private static readonly PropertyInfo ParentTreeViewItemProperty =
            typeof(TreeViewItem).GetProperty("ParentTreeViewItem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo ParentTreeViewProperty =
            typeof(TreeViewItem).GetProperty("ParentTreeView", BindingFlags.Instance | BindingFlags.NonPublic);

        public static TreeViewItem GetParent(this TreeViewItem item)
        {
            return ParentTreeViewItemProperty.GetValue(item) as TreeViewItem;
        }

        public static ItemsControl GetParentOrTreeView(this TreeViewItem item)
        {
            return (ItemsControl)item.GetParent() ?? item.GetTreeView();
        }

        public static TreeView GetTreeView(this TreeViewItem item)
        {
            if (item == null)
            {
                return null;
            }

            return ParentTreeViewProperty.GetValue(item) as TreeView;
        }

        public static FrameworkElement GetHeaderControl(this TreeViewItem item)
        {
            if (item == null || item.Template == null)
            {
                return item;
            }

            var result = item.Template.FindName("PART_Header", item) as FrameworkElement;
            if (result != null)
            {
                return result;
            }

            return item;
        }

        public static void ExpandAll(this TreeViewItem treeViewItem)
        {
            if (treeViewItem == null)
            {
                return;
            }

            foreach (object obj in treeViewItem.Items)
            {
                TreeViewItem childControl = treeViewItem.ItemContainerGenerator.ContainerFromItem(obj) as TreeViewItem;
                if (childControl != null)
                {
                    ExpandAll(childControl);
                }

                treeViewItem.IsExpanded = true;
            }
        }

        public static TreeViewItem FindFirstItem(this TreeViewItem item)
        {
            var current = item;
            while (true)
            {
                var next = GetPreviousItemInTraversalOrder(current);
                if (next == null)
                {
                    return current;
                }

                current = next;
            }
        }

        public static TreeViewItem FindLastItem(this TreeViewItem item)
        {
            var current = item;
            while (true)
            {
                var next = GetNextItemInTraversalOrder(current);
                if (next == null)
                {
                    return current;
                }

                current = next;
            }
        }

        public static TreeViewItem GetPreviousVisibleItemInTraversalOrder(this TreeViewItem current)
        {
            return GetPreviousItemInTraversalOrder(current, t => t.IsVisible);
        }

        public static TreeViewItem GetPreviousItemInTraversalOrder(this TreeViewItem current, Func<TreeViewItem, bool> predicate)
        {
            while (true)
            {
                current = current.GetPreviousItemInTraversalOrder();
                if (current == null)
                {
                    return null;
                }

                if (predicate(current))
                {
                    return current;
                }
            }
        }

        public static TreeViewItem GetPreviousItemInTraversalOrder(this TreeViewItem current)
        {
            var previousSibling = FindPreviousSibling(current);
            if (previousSibling != null)
            {
                var lastDescendant = FindLastDescendant(previousSibling);
                return lastDescendant;
            }

            var parent = current.GetParent();
            if (parent != null)
            {
                return parent;
            }

            return null;
        }

        public static TreeViewItem GetNextItemInTraversalOrder(this TreeViewItem current)
        {
            var child = FindFirstChild(current);
            if (child != null)
            {
                return child;
            }

            var parent = current;
            while (parent != null)
            {
                var nextToParent = FindNextSibling(parent);
                if (nextToParent != null)
                {
                    return nextToParent;
                }

                parent = parent.GetParent();
            }

            return null;
        }

        public static TreeViewItem FindFirstChild(this TreeViewItem current)
        {
            if (current.HasItems && current.IsExpanded)
            {
                return current.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
            }

            return null;
        }

        public static TreeViewItem FindLastChild(this TreeViewItem current)
        {
            if (current.HasItems && current.IsExpanded)
            {
                return current.ItemContainerGenerator.ContainerFromIndex(current.Items.Count - 1) as TreeViewItem;
            }

            return null;
        }

        public static TreeViewItem FindLastDescendant(this TreeViewItem current)
        {
            if (current.HasItems && current.IsExpanded)
            {
                var lastChild = FindLastChild(current);
                if (lastChild != null)
                {
                    return FindLastDescendant(lastChild);
                }
            }

            return current;
        }

        public static TreeViewItem FindPreviousSibling(this TreeViewItem current)
        {
            ItemsControl parent = current.GetParentOrTreeView();
            var next = FindPreviousSibling(current, parent);
            return next;
        }

        public static TreeViewItem FindNextSibling(this TreeViewItem current)
        {
            ItemsControl parent = current.GetParentOrTreeView();
            var next = FindNextSibling(current, parent);
            return next;
        }

        public static TreeViewItem FindPreviousSibling(this TreeViewItem current, ItemsControl parent)
        {
            var itemContainerGenerator = parent.ItemContainerGenerator;

            var index = itemContainerGenerator.IndexFromContainer(current);
            if (index > 0)
            {
                index--;
                return itemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
            }

            return null;
        }

        public static TreeViewItem FindNextSibling(this TreeViewItem current, ItemsControl parent)
        {
            var index = parent.ItemContainerGenerator.IndexFromContainer(current);
            if (index < parent.ItemContainerGenerator.Items.Count - 1)
            {
                index++;
                return parent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
            }

            return null;
        }

        public static void ScrollItemIntoView(this TreeViewItem selectedItem)
        {
            var treeView = selectedItem.GetTreeView();
            if (treeView == null)
            {
                return;
            }

            var scrollViewer = treeView.FindAncestor<ScrollViewer>();
            if (scrollViewer == null)
            {
                return;
            }

            var header = selectedItem.GetHeaderControl();
            if (header == null)
            {
                return;
            }

            var positionInScrollViewer = header.TranslatePoint(new Point(), scrollViewer);

            var viewPortHeight = scrollViewer.ViewportHeight;

            double delta = 0;
            double y = positionInScrollViewer.Y;

            if (y < 0)
            {
                delta = y;
            }
            else if (y > viewPortHeight - header.ActualHeight)
            {
                delta = y - (viewPortHeight - header.ActualHeight);
            }

            if (!IsZero(delta))
            {
                var newOffset = scrollViewer.VerticalOffset + delta;
                if (newOffset < 0)
                {
                    newOffset = 0;
                }
                else if (newOffset > scrollViewer.ScrollableHeight)
                {
                    newOffset = scrollViewer.ScrollableHeight;
                }

                scrollViewer.ScrollToVerticalOffset(newOffset);
            }
        }

        // Const values come from sdk\inc\crt\float.h
        internal const double DBL_EPSILON = 2.2204460492503131e-016; /* smallest such that 1.0+DBL_EPSILON != 1.0 */
        internal const float FLT_MIN = 1.175494351e-38F; /* Number close to zero, where float.MinValue is -float.MaxValue */

        private static bool IsZero(double value)
        {
            return Math.Abs(value) < 10.0 * DBL_EPSILON;
        }

        public static TreeViewItem FindFirstVisibleItem(this TreeViewItem item, ScrollViewer scrollViewer, bool up)
        {
            var next = item;
            while (true)
            {
                if (up)
                {
                    next = GetPreviousItemInTraversalOrder(next);
                }
                else
                {
                    next = GetNextItemInTraversalOrder(next);
                }

                if (next == null)
                {
                    return null;
                }

                if (IsVisibleInScrollViewer(next, scrollViewer))
                {
                    return next;
                }
            }
        }

        public static bool IsVisibleInScrollViewer(this TreeViewItem item, ScrollViewer scrollViewer)
        {
            var header = item.GetHeaderControl();
            if (header == null)
            {
                return false;
            }

            var positionInScrollViewer = header.TranslatePoint(new Point(), scrollViewer);

            var viewPortHeight = scrollViewer.ViewportHeight;

            return positionInScrollViewer.Y >= 0 && positionInScrollViewer.Y + header.ActualHeight < viewPortHeight;
        }

        public static IEnumerable<TreeViewItem> EnumerateSiblingsCycle(this TreeViewItem treeViewItem)
        {
            var parent = treeViewItem.GetParentOrTreeView();
            if (parent == null)
            {
                yield return treeViewItem;
                yield break;
            }

            var itemContainerGenerator = parent.ItemContainerGenerator;
            var index = itemContainerGenerator.IndexFromContainer(treeViewItem);
            for (int i = index; i < itemContainerGenerator.Items.Count; i++)
            {
                if (itemContainerGenerator.ContainerFromIndex(i) is TreeViewItem current)
                {
                    yield return current;
                }
            }

            for (int i = 0; i < index; i++)
            {
                if (itemContainerGenerator.ContainerFromIndex(i) is TreeViewItem current)
                {
                    yield return current;
                }
            }
        }

        public static IEnumerable<TreeViewItem> EnumerateAllTreeItemsInALoop(this TreeViewItem treeViewItem)
        {
            var parent = treeViewItem.GetParentOrTreeView();
            if (parent == null)
            {
                yield return treeViewItem;
                yield break;
            }

            var current = treeViewItem;
            while (current != null)
            {
                yield return current;
                current = current.GetNextItemInTraversalOrder();
            }

            var first = (TreeViewItem)treeViewItem.GetTreeView().ItemContainerGenerator.ContainerFromIndex(0);
            current = first;
            while (true)
            {
                if (current == null || current == treeViewItem)
                {
                    yield break;
                }

                yield return current;
                current = current.GetNextItemInTraversalOrder();
            }
        }
    }
}