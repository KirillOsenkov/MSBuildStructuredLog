using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace StructuredLogViewer.Controls
{
    public static class TreeViewExtensions
    {
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
            SelectContainerFromItem(treeView,
                new SelectInfo<T>()
                {
                    Items = items,
                    CompareMethod = compareMethod,
                    SelectItem = delegate (ItemsControl container, SelectInfo<T> info)
                    {
                        var treeItem = (TreeViewItem)container;

                        //treeItem.Focus();
                        treeItem.IsSelected = true;
                        //treeItem.IsExpanded = true;
                        treeItem.BringIntoView();
                    },
                    NeedMoreItems = delegate (ItemsControl container, SelectInfo<T> info)
                    {
                        ((TreeViewItem)container).IsExpanded = true;
                    }
                }
            );
        }

        /// <summary>
        /// Selects the UI element in the specified <i>control</i> based on the corresponding <i>info</i>.
        /// </summary>
        /// <typeparam name="T">The type of item to match.</typeparam>
        /// <param name="container">The <see cref="ItemsControl"/> in which to dumpster dive.</param>
        /// <param name="info">Control parameters used in the search.</param>
        private static void SelectContainerFromItem<T>(this ItemsControl container, SelectInfo<T> selectInfo)
        {
            var currentItem = selectInfo.Items.First();

            if (container.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                // If the item containers haven't been generated yet, attach an event
                // and wait for the status to change.
                EventHandler selectWhenReadyMethod = null;

                selectWhenReadyMethod = (ds, de) =>
                {
                    if (container.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    {
                        // Stop listening for status changes on this container
                        container.ItemContainerGenerator.StatusChanged -= selectWhenReadyMethod;

                        // Search the container for the item chain
                        SelectContainerFromItem(container, selectInfo);
                    }
                };

                container.ItemContainerGenerator.StatusChanged += selectWhenReadyMethod;

                return;
            }

            Debug.Assert(container.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated);

            // Compare each item in the container and look for the next item
            // in the chain.
            foreach (object item in container.Items)
            {
                var convertedItem = (T)item;

                // Compare the converted item with the item in the chain
                if ((selectInfo.CompareMethod != null) && selectInfo.CompareMethod(convertedItem, currentItem))
                {
                    // Since the TreeViewItems are in a virtualized panel, the item to be selected may not be realized,
                    // need to ensure it is brought into view, so it can be selected.
                    ItemsPresenter itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                    if (itemsPresenter != null)
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
                            var stackPanel = child as StackPanel;
                            if (stackPanel != null)
                            {
                                var frameworkElement = stackPanel.Children[index] as FrameworkElement;
                                if (frameworkElement != null)
                                {
                                    frameworkElement.BringIntoView();
                                }
                            }
                        }
                    }

                    var containerParent = (ItemsControl)container.ItemContainerGenerator.ContainerFromItem(item);
                    Debug.Assert(containerParent != null, "Failed to find the parent container for the selected item.");

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
                            SelectContainerFromItem(containerParent, selectInfo);
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Finds a visual child of a given type down the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of the queried item.</typeparam>
        /// <param name="element">The element in the visual tree to commence searching below from.</param>
        /// <returns>The first child item that matches the queried type, null is returned otherwise.</returns>
        public static T FindVisualChild<T>(UIElement element) where T : UIElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                UIElement child = (UIElement)VisualTreeHelper.GetChild(element, i);
                if (child != null)
                {
                    T correctlyTyped = child as T;
                    if (correctlyTyped != null)
                    {
                        return correctlyTyped;
                    }

                    T descendent = FindVisualChild<T>(child);
                    if (descendent != null)
                    {
                        return descendent;
                    }
                }
            }

            return null;
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