using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using System;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class TabItemsControl : TemplatedControl
    {
        public static DirectProperty<TabItemsControl, ObservableCollection<TabItem>> TabsProperty =
            AvaloniaProperty.RegisterDirect(nameof(Tabs), (TabItemsControl o) => o.Tabs);

        public static DirectProperty<TabItemsControl, IEnumerable<object>> HeadersProperty =
            AvaloniaProperty.RegisterDirect(nameof(Headers), (TabItemsControl o) => o.Headers);

        public static readonly DirectProperty<TabItemsControl, int> SelectedIndexProperty =
            AvaloniaProperty.RegisterDirect<TabItemsControl, int>(
                nameof(SelectedIndex),
                o => o.SelectedIndex,
                (o, v) => o.SelectedIndex = v,
                unsetValue: -1);

        public static readonly StyledProperty<Dock> TabStripPlacementProperty =
            TabControl.TabStripPlacementProperty.AddOwner<TabItemsControl>();

        private int _selectedIndex = -1;

        public TabItemsControl()
        {
            Tabs = new ObservableCollection<TabItem>();
            Tabs.CollectionChanged += (o, e) => RaisePropertyChanged(HeadersProperty, null, Headers);
        }

        [Content]
        public ObservableCollection<TabItem> Tabs { get; }

        public IEnumerable<object> Headers => Tabs?.Select(t => t.Header);

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetAndRaise(SelectedIndexProperty, ref _selectedIndex, value);
        }

        public TabItem SelectedItem
        {
            get => Tabs[SelectedIndex];
            set => SelectedIndex = Tabs.IndexOf(value);
        }

        public Dock TabStripPlacement
        {
            get => GetValue(TabStripPlacementProperty);
            set => SetValue(TabStripPlacementProperty, value);
        }

        protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
        {
            base.OnTemplateApplied(e);
        }
    }
}