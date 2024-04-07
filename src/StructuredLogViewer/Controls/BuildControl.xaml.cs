using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Language.Xml;
using Mono.Cecil;
using StructuredLogViewer.Core.ProjectGraph;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public Build Build { get; set; }
        public TreeViewItem SelectedTreeViewItem { get; private set; }
        public string LogFilePath => Build?.LogFilePath;

        private SourceFileResolver sourceFileResolver;
        private ArchiveFileResolver archiveFile => sourceFileResolver.ArchiveFile;
        private PreprocessedFileManager preprocessedFileManager;
        private NavigationHelper navigationHelper;

        private MenuItem searchMenuGroup;
        private MenuItem copyMenuGroup;
        private MenuItem gotoMenuGroup;
        private MenuItem copyItem;
        private MenuItem copySubtreeItem;
        private MenuItem viewSubtreeTextItem;
        private MenuItem searchInSubtreeItem;
        private MenuItem searchInNodeByNameItem;
        private MenuItem searchThisNode;
        private MenuItem excludeSubtreeFromSearchItem;
        private MenuItem excludeNodeByNameFromSearch;
        private MenuItem goToTimeLineItem;
        private MenuItem goToTracingItem;
        private MenuItem copyChildrenItem;
        private MenuItem sortChildrenItem;
        private MenuItem filterChildrenItem;
        private MenuItem copyNameItem;
        private MenuItem copyValueItem;
        private MenuItem viewSourceItem;
        private MenuItem viewFullTextItem;
        private MenuItem openFileItem;
        private MenuItem copyFilePathItem;
        private MenuItem preprocessItem;
        private MenuItem searchNuGetItem;
        private MenuItem runItem;
        private MenuItem debugItem;
        private MenuItem hideItem;
        private MenuItem showTimeItem;
        private MenuItem favoriteItem;
        private MenuItem unfavoriteItem;
        private MenuItem favoriteSharedItem;
        private MenuItem unfavoriteSharedItem;

        private ContextMenu sharedTreeContextMenu;
        private ContextMenu filesTreeContextMenu;

        private TreeView ActiveTreeView;

        private PropertiesAndItemsSearch propertiesAndItemsSearch;

        public BuildControl(Build build, string logFilePath)
        {
            InitializeComponent();

            UpdateWatermark();

            searchLogControl.ExecuteSearch = (searchText, maxResults, cancellationToken) =>
            {
                if (Build.SearchIndex is { } index)
                {
                    index.MaxResults = maxResults;
                    index.MarkResultsInTree = SettingsService.MarkResultsInTree;
                    var indexResults = index.FindNodes(searchText, cancellationToken);
                    PrecalculationDuration = index.PrecalculationDuration;
                    return indexResults;
                }

                var search = new Search(
                    new[] { Build },
                    Build.StringTable.Instances,
                    maxResults,
                    SettingsService.MarkResultsInTree);
                var results = search.FindNodes(searchText, cancellationToken);
                PrecalculationDuration = search.PrecalculationDuration;
                return results;
            };
            searchLogControl.ResultsTreeBuilder = BuildResultTree;
            searchLogControl.WatermarkDisplayed += () =>
            {
                Search.ClearSearchResults(Build, SettingsService.MarkResultsInTree);
                UpdateWatermark();
            };

            propertiesAndItemsSearch = new PropertiesAndItemsSearch();

            propertiesAndItemsControl.ExecuteSearch = (searchText, maxResults, cancellationToken) =>
            {
                var context = GetProjectContext() as TimedNode;
                if (context == null)
                {
                    return null;
                }

                var results = propertiesAndItemsSearch.Search(
                    context,
                    searchText,
                    maxResults,
                    SettingsService.MarkResultsInTree,
                    cancellationToken);

                return results;
            };
            propertiesAndItemsControl.ResultsTreeBuilder = BuildResultTree;

            UpdatePropertiesAndItemsWatermark();
            propertiesAndItemsControl.WatermarkDisplayed += UpdatePropertiesAndItemsWatermark;
            propertiesAndItemsControl.RecentItemsCategory = "PropertiesAndItems";

            SetProjectContext(null);

            VirtualizingPanel.SetIsVirtualizing(treeView, SettingsService.EnableTreeViewVirtualization);

            DataContext = build;
            Build = build;

            // first try to see if the source archive was embedded in the log
            if (build.SourceFiles != null)
            {
                sourceFileResolver = new SourceFileResolver(build.SourceFiles);
            }
            else
            {
                // otherwise try to read from the .zip file on disk if present
                sourceFileResolver = new SourceFileResolver(logFilePath);
            }

            if (Build.Statistics.TimedNodeCount > 1000)
            {
                projectGraphTab.Visibility = Visibility.Collapsed;
            }

            // Search Log | Properties and Items | Find in Files
            sharedTreeContextMenu = new ContextMenu();
            sharedTreeContextMenu.Opened += SharedTreeContextMenu_Opened;
            favoriteSharedItem = new MenuItem { Header = "Add to Favorites" };
            unfavoriteSharedItem = new MenuItem { Header = "Remove from Favorites" };
            var sharedCopyItem = new MenuItem() { Header = "Copy" };
            var sharedCopyAllItem = new MenuItem() { Header = "Copy All" };
            var sharedCopySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            favoriteSharedItem.Click += (s, a) => AddToFavorites();
            unfavoriteSharedItem.Click += (s, a) => RemoveFromFavorites();
            sharedCopyItem.Click += (s, a) => Copy();
            sharedCopyAllItem.Click += (s, a) => CopyAll();
            sharedCopySubtreeItem.Click += (s, a) => CopySubtree();
            sharedTreeContextMenu.AddItem(favoriteSharedItem);
            sharedTreeContextMenu.AddItem(unfavoriteSharedItem);
            sharedTreeContextMenu.AddItem(sharedCopyItem);
            sharedTreeContextMenu.AddItem(sharedCopyAllItem);
            sharedTreeContextMenu.AddItem(sharedCopySubtreeItem);

            // Files
            filesTreeContextMenu = new ContextMenu();
            var filesCopyItem = new MenuItem { Header = "Copy" };
            var filesCopyAllItem = new MenuItem { Header = "Copy All" };
            var filesCopyPathsItem = new MenuItem { Header = "Copy file paths" };
            var filesCopySubtreeItem = new MenuItem { Header = "Copy subtree" };
            filesCopyItem.Click += (s, a) => Copy();
            filesCopyAllItem.Click += (s, a) => CopyAll();
            filesCopyPathsItem.Click += (s, a) => CopyPaths();
            filesCopySubtreeItem.Click += (s, a) => CopySubtree();
            filesTreeContextMenu.AddItem(filesCopyItem);
            filesTreeContextMenu.AddItem(filesCopyAllItem);
            filesTreeContextMenu.AddItem(filesCopyPathsItem);
            filesTreeContextMenu.AddItem(filesCopySubtreeItem);

            // Build Log
            var contextMenu = new ContextMenu();
            contextMenu.Opened += ContextMenu_Opened;
            searchMenuGroup = new() { Header = "Search" };
            copyMenuGroup = new() { Header = "Copy" };
            gotoMenuGroup = new() { Header = "Go to" };

            copyItem = new MenuItem() { Header = "Copy" };
            copySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            viewSubtreeTextItem = new MenuItem() { Header = "View subtree text" };
            searchInSubtreeItem = new MenuItem() { Header = "Search in subtree" };
            excludeSubtreeFromSearchItem = new MenuItem() { Header = "Exclude subtree from search" };
            excludeNodeByNameFromSearch = new MenuItem() { Header = "Exclude node from search" };
            searchInNodeByNameItem = new MenuItem() { Header = "Search in this node." };
            searchThisNode = new MenuItem() { Header = "Search This Node" };
            goToTimeLineItem = new MenuItem() { Header = "Timeline" };
            goToTracingItem = new MenuItem() { Header = "Tracing" };
            copyChildrenItem = new MenuItem() { Header = "Copy children" };
            sortChildrenItem = new MenuItem() { Header = "Sort children" };
            filterChildrenItem = new MenuItem() { Header = "Filter children (Ctrl+F)" };
            copyNameItem = new MenuItem() { Header = "Copy name" };
            copyValueItem = new MenuItem() { Header = "Copy value" };
            viewSourceItem = new MenuItem() { Header = "View source" };
            viewFullTextItem = new MenuItem { Header = "View full text" };
            showTimeItem = new MenuItem() { Header = "Show time and duration" };
            favoriteItem = new MenuItem() { Header = "Add to Favorites" };
            unfavoriteItem = new MenuItem() { Header = "Remove from Favorites" };
            openFileItem = new MenuItem() { Header = "Open File" };
            copyFilePathItem = new MenuItem() { Header = "Copy file path" };
            preprocessItem = new MenuItem() { Header = "Preprocess" };
            var nugetImage = new System.Windows.Shapes.Path
            {
                Data = (Geometry)Application.Current.FindResource("NuGetGeometry"),
                Stroke = (Brush)Application.Current.FindResource("NuGet"),
                Fill = (Brush)Application.Current.FindResource("NuGet"),
                Width = 16,
                Height = 16,
                StrokeThickness = 1
            };
            searchNuGetItem = new MenuItem() { Header = "Search project.assets.json", Icon = nugetImage };
            hideItem = new MenuItem() { Header = "Hide" };
            runItem = new MenuItem() { Header = "Run" };
            debugItem = new MenuItem() { Header = "Debug" };
            copyItem.Click += (s, a) => Copy();
            copySubtreeItem.Click += (s, a) => CopySubtree(ActiveTreeView);
            viewSubtreeTextItem.Click += (s, a) => ViewSubtreeText();
            searchInSubtreeItem.Click += (s, a) => SearchInSubtree();
            excludeSubtreeFromSearchItem.Click += (s, a) => ExcludeSubtreeFromSearch();
            excludeNodeByNameFromSearch.Click += (s, a) => ExcludeNodeByNameFromSearch();
            searchInNodeByNameItem.Click += (s, a) => SearchInNodeByName();
            searchThisNode.Click += (s, a) => SearchThisNode();
            goToTimeLineItem.Click += (s, a) => GoToTimeLine();
            goToTracingItem.Click += (s, a) => GoToTracing();
            copyChildrenItem.Click += (s, a) => CopyChildren();
            sortChildrenItem.Click += (s, a) => SortChildren();
            filterChildrenItem.Click += (s, a) => FilterChildren();
            copyNameItem.Click += (s, a) => CopyName();
            copyValueItem.Click += (s, a) => CopyValue();
            viewSourceItem.Click += (s, a) => Invoke(treeView.SelectedItem as BaseNode);
            viewFullTextItem.Click += (s, a) => ViewFullText(treeView.SelectedItem as BaseNode);
            showTimeItem.Click += (s, a) => ShowTimeAndDuration();
            favoriteItem.Click += (s, a) => AddToFavorites();
            unfavoriteItem.Click += (s, a) => RemoveFromFavorites();
            openFileItem.Click += (s, a) => OpenFile();
            copyFilePathItem.Click += (s, a) => CopyFilePath();
            preprocessItem.Click += (s, a) => Preprocess(treeView.SelectedItem as IPreprocessable);
            searchNuGetItem.Click += (s, a) => SearchNuGet(treeView.SelectedItem as IProjectOrEvaluation);
            runItem.Click += (s, a) => Run(treeView.SelectedItem as Task, debug: false);
            debugItem.Click += (s, a) => Run(treeView.SelectedItem as Task, debug: true);
            hideItem.Click += (s, a) => Delete();

            contextMenu.AddItem(favoriteItem);
            contextMenu.AddItem(unfavoriteItem);
            contextMenu.AddItem(runItem);
            contextMenu.AddItem(debugItem);
            contextMenu.AddItem(viewSourceItem);
            contextMenu.AddItem(viewFullTextItem);
            contextMenu.AddItem(openFileItem);
            gotoMenuGroup.AddItem(preprocessItem);
            contextMenu.AddItem(searchMenuGroup);
            searchMenuGroup.AddItem(searchNuGetItem);
            searchMenuGroup.AddItem(searchInSubtreeItem);
            searchMenuGroup.AddItem(searchInNodeByNameItem);
            searchMenuGroup.AddItem(searchThisNode);
            searchMenuGroup.AddItem(excludeSubtreeFromSearchItem);
            searchMenuGroup.AddItem(excludeNodeByNameFromSearch);
            contextMenu.AddItem(gotoMenuGroup);
            gotoMenuGroup.AddItem(goToTimeLineItem);
            gotoMenuGroup.AddItem(goToTracingItem);
            contextMenu.AddItem(copyMenuGroup);
            copyMenuGroup.AddItem(copyItem);
            copyMenuGroup.AddItem(copySubtreeItem);
            copyMenuGroup.AddItem(copyFilePathItem);
            gotoMenuGroup.AddItem(viewSubtreeTextItem);
            copyMenuGroup.AddItem(copyChildrenItem);
            contextMenu.AddItem(sortChildrenItem);
            contextMenu.AddItem(filterChildrenItem);
            copyMenuGroup.AddItem(copyNameItem);
            copyMenuGroup.AddItem(copyValueItem);
            gotoMenuGroup.AddItem(showTimeItem);
            contextMenu.AddItem(hideItem);

            var treeViewItemStyle = TreeViewExtensions.CreateTreeViewItemStyleWithEvents<BaseNode, TreeViewItem>();

            treeViewItemStyle.Setters.Add(new EventSetter(MouseDoubleClickEvent, (MouseButtonEventHandler)OnItemDoubleClick));
            treeViewItemStyle.Setters.Add(new EventSetter(KeyDownEvent, (KeyEventHandler)OnItemKeyDown));

            treeView.ContextMenu = contextMenu;
            treeView.ItemContainerStyle = treeViewItemStyle;
            treeView.KeyUp += TreeView_KeyDown;
            treeView.SelectedItemChanged += TreeView_SelectedItemChanged;
            treeView.GotFocus += TreeView_GetFocus;
            treeView.AddHandler(TreeViewItem.SelectedEvent, (RoutedEventHandler)TreeViewItem_Selected);

            findTextBox.KeyDown += FindTextBox_KeyDown;
            searchLogControl.searchTextBox.KeyUp += SearchTextBox_KeyUp;

            ActiveTreeView = treeView;

            searchLogControl.ResultsList.ItemContainerStyle = treeViewItemStyle;
            searchLogControl.ResultsList.SelectedItemChanged += ResultsList_SelectionChanged;
            searchLogControl.ResultsList.GotFocus += (s, a) => ActiveTreeView = searchLogControl.ResultsList;
            searchLogControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            propertiesAndItemsControl.ResultsList.ItemContainerStyle = treeViewItemStyle;
            propertiesAndItemsControl.ResultsList.SelectedItemChanged += ResultsList_SelectionChanged;
            propertiesAndItemsControl.ResultsList.GotFocus += (s, a) => ActiveTreeView = propertiesAndItemsControl.ResultsList;
            propertiesAndItemsControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            if (archiveFile != null)
            {
                findInFilesControl.ExecuteSearch = FindInFiles;
                findInFilesControl.ResultsTreeBuilder = BuildFindResults;

                findInFilesControl.GotFocus += (s, a) => ActiveTreeView = findInFilesControl.ResultsList;
                findInFilesControl.ResultsList.ItemContainerStyle = treeViewItemStyle;
                findInFilesControl.ResultsList.GotFocus += (s, a) => ActiveTreeView = findInFilesControl.ResultsList;
                findInFilesControl.ResultsList.ContextMenu = sharedTreeContextMenu;

                filesTab.Visibility = Visibility.Visible;
                findInFilesTab.Visibility = Visibility.Visible;
                PopulateFilesTab();
                filesTree.ResultsList.ItemContainerStyle = treeViewItemStyle;

                filesTree.TextChanged += FilesTree_SearchTextChanged;

                var text =
@"This log contains the full text of projects and imported files used during the build.
You can use the 'Files' tab in the bottom left to view these files and the 'Find in Files' tab for full-text search.
For many nodes in the tree (Targets, Tasks, Errors, Projects, etc) pressing SPACE or ENTER or double-clicking 
on the node will navigate to the corresponding source code associated with the node.

More functionality is available from the right-click context menu for each node.
Right-clicking a project node may show the 'Preprocess' option if the version of MSBuild was at least 15.3.";
#if DEBUG
                text = build.StringTable.Intern(text);
#endif
                var folder = new Folder { Name = "Embedded files" };
                folder.AddChild(new Note { Text = text });
                build.AddChild(folder);
            }

            favoritesTree.TopPanel.Visibility = Visibility.Collapsed;
            favoritesTree.ResultsList.ItemContainerStyle = treeViewItemStyle;
            favoritesTree.ResultsList.SelectedItemChanged += ResultsList_SelectionChanged;
            favoritesTree.ResultsList.ContextMenu = sharedTreeContextMenu;
            favoritesTree.DisplayItems(new[] { new Note { Text = "Right-click any node and Favorite it to add it here" } });
            favoritesTree.ResultsList.GotFocus += (s, a) => ActiveTreeView = favoritesTree.ResultsList;

            breadCrumb.SelectionChanged += BreadCrumb_SelectionChanged;

            Loaded += BuildControl_Loaded;

            preprocessedFileManager = new PreprocessedFileManager(this.Build, sourceFileResolver);
            preprocessedFileManager.DisplayFile += filePath => DisplayFile(filePath);

            navigationHelper = new NavigationHelper(Build, sourceFileResolver);
            navigationHelper.OpenFileRequested += filePath => DisplayFile(filePath);

            centralTabControl.SelectionChanged += CentralTabControl_SelectionChanged;
        }

        public void Dispose()
        {
            // WPF controls
            documentWell.Dispose();
            searchLogControl.Dispose();
            searchLogControl.ResultsList.ItemContainerStyle = null;
            searchLogControl.ResultsList.SelectedItemChanged -= ResultsList_SelectionChanged;
            searchLogControl.WatermarkDisplayed -= UpdatePropertiesAndItemsWatermark;
            searchLogControl.ExecuteSearch = null;
            searchLogControl.WatermarkContent = null;
            propertiesAndItemsControl.ResultsList.ItemContainerStyle = null;
            propertiesAndItemsControl.ResultsList.SelectedItemChanged -= ResultsList_SelectionChanged;
            propertiesAndItemsControl.WatermarkDisplayed -= UpdatePropertiesAndItemsWatermark;
            propertiesAndItemsControl.ExecuteSearch = null;
            propertiesAndItemsControl.WatermarkContent = null;
            propertiesAndItemsContext.Content = null;
            propertiesAndItemsSearch = null;
            breadCrumb.ItemsSource = null;
            filesTree.ResultsList.ItemContainerStyle = null;
            filesTree.ContextMenu = null;
            filesTree.DisplayItems(null);
            favoritesTree.ResultsList.ItemContainerStyle = null;
            findInFilesControl.ResultsList.ItemContainerStyle = null;
            treeView.RemoveHandler(TreeViewItem.SelectedEvent, (RoutedEventHandler)TreeViewItem_Selected);
            treeView.SelectedItemChanged -= TreeView_SelectedItemChanged;
            treeView.KeyUp -= TreeView_KeyDown;
            treeView.GotFocus -= TreeView_GetFocus;
            treeView.ItemsSource = null;
            treeView.ItemContainerStyle = null;
            treeView.ContextMenu = null;
            centralTabControl.SelectionChanged -= CentralTabControl_SelectionChanged;

            findTextBox.KeyDown -= FindTextBox_KeyDown;
            searchLogControl.searchTextBox.KeyUp -= SearchTextBox_KeyUp;

            if (this.tracing.Timeline != null)
            {
                this.tracing.Dispose();
            }

            if (this.timeline.Timeline != null)
            {
                this.timeline.Dispose();
            }

            if (this.graph != null)
            {
                graph = null;
                projectGraphControl.Dispose();
            }

            // member variables
            copyItem = null;
            copySubtreeItem = null;
            viewSubtreeTextItem = null;
            searchInSubtreeItem = null;
            excludeSubtreeFromSearchItem = null;
            excludeNodeByNameFromSearch = null;
            searchInNodeByNameItem = null;
            searchThisNode = null;
            goToTimeLineItem = null;
            goToTracingItem = null;
            copyChildrenItem = null;
            sortChildrenItem = null;
            filterChildrenItem = null;
            copyNameItem = null;
            copyValueItem = null;
            viewSourceItem = null;
            viewFullTextItem = null;
            openFileItem = null;
            copyFilePathItem = null;
            preprocessItem = null;
            searchNuGetItem = null;
            runItem = null;
            debugItem = null;
            hideItem = null;
            showTimeItem = null;
            favoriteItem = null;
            unfavoriteItem = null;
            favoriteSharedItem = null;
            unfavoriteSharedItem = null;

            sharedTreeContextMenu = null;
            filesTreeContextMenu = null;
            ActiveTreeView = null;
            DataContext = null;
            preprocessedFileManager = null;
            navigationHelper = null;
            projectContext = null;
            SelectedTreeViewItem = null;
            sourceFileResolver = null;
            BaseNode.ClearSelectedNode();
            this.Build = null;
        }

        private void CentralTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = centralTabControl.SelectedItem as TabItem;
            if (selectedItem == null)
            {
                return;
            }
            else if (selectedItem.Name == nameof(timelineTab))
            {
                PopulateTimeline();
            }
            else if (selectedItem.Name == nameof(projectGraphTab))
            {
                PopulateProjectGraph();
            }
            else if (selectedItem.Name == nameof(tracingTab))
            {
                PopulateTrace();
            }
        }

        private void FilesTree_SearchTextChanged(string text)
        {
            var list = filesTree.ResultsList.ItemsSource as IEnumerable<object>;
            if (list != null)
            {
                UpdateFileVisibility(list.OfType<NamedNode>(), text);
            }
        }

        private bool UpdateFileVisibility(IEnumerable<NamedNode> items, string text)
        {
            bool visible = false;

            if (items == null)
            {
                return false;
            }

            foreach (var item in items)
            {
                if (item is Folder folder)
                {
                    var subItems = folder.Children.OfType<NamedNode>();
                    var folderVisibility = UpdateFileVisibility(subItems, text);
                    folder.IsVisible = folderVisibility;
                    visible |= folderVisibility;
                }
                else if (item is SourceFile file)
                {
                    if (string.IsNullOrEmpty(text) || file.SourceFilePath.IndexOf(text, StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        visible = true;
                        file.IsVisible = true;
                    }
                    else
                    {
                        file.IsVisible = false;
                    }

                    var subItems = file.Children.OfType<NamedNode>();
                    var fileVisibility = UpdateFileVisibility(subItems, text);
                    file.IsVisible |= fileVisibility;
                    visible |= fileVisibility;
                }
                else if (item is Target || item is Task)
                {
                    if (string.IsNullOrEmpty(text) ||
                        item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) > -1 ||
                        (text == "$target" && item is Target) ||
                        (text == "$task" && item is Task))
                    {
                        visible = true;
                        item.IsVisible = true;
                    }
                    else
                    {
                        item.IsVisible = false;
                    }
                }
            }

            return visible;
        }

        public void SelectTree()
        {
            centralTabControl.SelectedIndex = 0;
        }

        private void PopulateTimeline()
        {
            if (this.timeline.Timeline == null)
            {
                var timeline = new Timeline(Build, analyzeCpp: false);
                this.timeline.BuildControl = this;
                this.timeline.SetTimeline(timeline, Build.StartTime.Ticks);
                this.timelineWatermark.Visibility = Visibility.Hidden;
                this.timeline.Visibility = Visibility.Visible;
            }
        }

        private void PopulateTrace()
        {
            if (this.tracing.Timeline == null)
            {
                var start = DateTime.UtcNow;
                var timeline = new Timeline(Build, analyzeCpp: true);
                var timelineTime = DateTime.UtcNow - start;
                this.tracing.TimelineTime = timelineTime;
                this.tracing.BuildControl = this;
                this.tracing.SetTimeline(timeline, Build.StartTime.Ticks, Build.EndTime.Ticks);
                this.tracingWatermark.Visibility = Visibility.Hidden;
                this.tracing.Visibility = Visibility.Visible;
            }
        }

        private Microsoft.Msagl.Drawing.Graph graph;

        private void PopulateProjectGraph()
        {
            if (graph != null)
            {
                return;
            }

            graph = new MsaglProjectGraphConstructor().FromBuild(Build);
            projectGraphControl.BuildControl = this;

            if (graph.NodeCount > 1000 || graph.EdgeCount > 10000)
            {
                centralTabControl.SelectedIndex = 0;
                projectGraphTab.Visibility = Visibility.Collapsed;
                return;
            }

            projectGraphControl.SetGraph(graph);
            projectGraphControl.Visibility = Visibility.Visible;
            projectGraphWatermark.Visibility = Visibility.Collapsed;
        }

        private static string[] searchExamples = new[]
        {
            "Copying file from ",
            "Resolved file path is ",
            "There was a conflict",
            "Encountered conflict between",
            "Building target completely ",
            "is newer than output ",
            "Property reassignment: $(",
            "out-of-date",
            "$task $time",
            "$message CompilerServer failed",
            "will be compiled because",
        };

        private static string[] nodeKinds = new[]
        {
            "$project",
            "$projectevaluation",
            "$target",
            "$task",
            "$error",
            "$warning",
            "$message",
            "$property",
            "$item",
            "$additem",
            "$removeitem",
            "$metadata",
            "$csc",
            "$rar",
            "$import",
            "$noimport"
        };

        private static Inline MakeLink(string query, SearchAndResultsControl searchControl, string before = " \u2022 ", string after = "\r\n")
        {
            var hyperlink = new Hyperlink(new Run(query.Trim()));
            hyperlink.Click += (s, e) => searchControl.SearchText = query;

            var span = new System.Windows.Documents.Span();
            if (before != null)
            {
                span.Inlines.Add(new Run(before));
            }

            span.Inlines.Add(hyperlink);

            if (after != null)
            {
                if (after == "\r\n")
                {
                    span.Inlines.Add(new LineBreak());
                }
                else
                {
                    span.Inlines.Add(new Run(after));
                }
            }

            return span;
        }

        private void UpdateWatermark()
        {
            string watermarkText0 = @"Type in the search box to search. Press Ctrl+F to focus the search box. Results (up to 1000) will display here.
";

            string watermarkText1 = @"
Search for multiple words separated by space (space means AND). Enclose multiple words in double-quotes """" to search for the exact phrase. A single word in quotes means exact match (turns off substring search).

Use syntax like '$property Prop' to narrow results down by item kind. Supported kinds: ";

            string watermarkText2 = @"Use the under(FILTER) clause to only include results where any of the nodes in the parent chain matches the FILTER. Use project(...) to filter by parent project. Examples:
 • $csc under($project Core)
 • Copying file project(ProjectA)

Append [[$time]], [[$start]] and/or [[$end]] to show times and/or durations and sort the results by start time or duration descending (for tasks, targets and projects).

Use start<""2023-11-23 14:30:54.579"", start>, end< or end> to filter events that start or end before or after a given timestamp. Timestamp needs to be in quotes.

Use '$copy path' where path is a file or directory to find file copy operations involving the file or directory. `$copy substring` will search for copied files containing the substring.

Use '$nuget project(MyProject.csproj) Package.Name' to search for NuGet packages (by name or version), dependencies (direct and transitive) and files coming from NuGet packages.

Examples:
";

            var watermark = new TextBlock();
            watermark.Inlines.Add(watermarkText0);

            var recentSearches = SettingsService.GetRecentSearchStrings();
            if (recentSearches.Any())
            {
                watermark.Inlines.Add(@"
Recent (");
                var clearRecentHyperlink = new Hyperlink(new Run("clear"));
                clearRecentHyperlink.Click += (s, e) => { SettingsService.RemoveAllRecentSearchText(); UpdateWatermark(); };
                watermark.Inlines.Add(clearRecentHyperlink);
                watermark.Inlines.Add(@"):
");

                foreach (var recentSearch in recentSearches.Where(s => !searchExamples.Contains(s) && !nodeKinds.Contains(s)))
                {
                    watermark.Inlines.Add(MakeLink(recentSearch, searchLogControl));
                }
            }

            watermark.Inlines.Add(watermarkText1);

            bool isFirst = true;
            foreach (var nodeKind in nodeKinds)
            {
                if (!isFirst)
                {
                    watermark.Inlines.Add(", ");
                }

                isFirst = false;
                watermark.Inlines.Add(MakeLink(nodeKind + " ", searchLogControl, before: null, after: null));
            }

            watermark.Inlines.Add(new LineBreak());
            watermark.Inlines.Add(new LineBreak());

            AddTextWithHyperlinks(watermarkText2, watermark.Inlines, searchLogControl);

            foreach (var example in searchExamples)
            {
                watermark.Inlines.Add(MakeLink(example, searchLogControl));
            }

            searchLogControl.WatermarkContent = watermark;
        }

        private void UpdatePropertiesAndItemsWatermark()
        {
            string watermarkText1 = $@"Look up properties or items for the selected project " +
                "or a node under a project or evaluation. " +
                "Properties and items might not be available for some projects.\n\n" +
                "Surround the search term in quotes to find an exact match " +
                "(turns off substring search). Prefix the search term with " +
                "[[name=]] or [[value=]] to only search property and metadata names " +
                "or values. Add [[$property ]], [[$item ]] or [[$metadata ]] to limit search " +
                "to a specific node type.";

            var watermark = new TextBlock();
            AddTextWithHyperlinks(watermarkText1, watermark.Inlines, propertiesAndItemsControl);

            watermark.Inlines.Add(new LineBreak());
            watermark.Inlines.Add(new LineBreak());

            var recentSearches = SettingsService.GetRecentSearchStrings("PropertiesAndItems");
            if (recentSearches.Any())
            {
                watermark.Inlines.Add(@"
Recent (");
                var clearRecentHyperlink = new Hyperlink(new Run("clear"));
                clearRecentHyperlink.Click += (s, e) => { SettingsService.RemoveAllRecentSearchText("PropertiesAndItems"); UpdatePropertiesAndItemsWatermark(); };
                watermark.Inlines.Add(clearRecentHyperlink);
                watermark.Inlines.Add(@"):
");

                foreach (var recentSearch in recentSearches)
                {
                    watermark.Inlines.Add(MakeLink(recentSearch, propertiesAndItemsControl));
                }
            }

            propertiesAndItemsControl.WatermarkContent = watermark;
        }

        public void AddTextWithHyperlinks(string text, InlineCollection result, SearchAndResultsControl searchControl)
        {
            const string openParen = "[[";
            const string closeParen = "]]";
            var chunks = TextUtilities.SplitIntoParenthesizedSpans(text, openParen, closeParen);
            foreach (var chunk in chunks)
            {
                if (chunk.StartsWith(openParen) && chunk.EndsWith(closeParen))
                {
                    var link = chunk.Substring(openParen.Length, chunk.Length - openParen.Length - closeParen.Length);
                    result.Add(MakeLink(link, searchControl, before: null, after: null));
                }
                else
                {
                    result.Add(chunk);
                }
            }
        }

        private void SearchNuGet(IProjectOrEvaluation node)
        {
            string projectName = Path.GetFileName(node.ProjectFile);
            searchLogControl.SearchText = $"$nuget project({projectName})";
            SelectSearchTab();
        }

        private void Preprocess(IPreprocessable project) => preprocessedFileManager.ShowPreprocessed(project);

        private void Run(Task task, bool debug = false)
        {
            var logFilePath = Build.LogFilePath;
            if (!File.Exists(logFilePath))
            {
                MessageBox.Show($"The log file {logFilePath} doesn't exist on disk. Please save the log to disk and reopen it from disk.");
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var arguments = $"{logFilePath.QuoteIfNeeded()} {task.Index} pause{(debug ? " debug" : "")}";

                var targetFramework = GetTaskTargetFramework(task);
                if (targetFramework == null || targetFramework.StartsWith(".NETFramework"))
                {
                    var taskRunnerExe = Path.Combine(directory, "TaskRunner.exe");
                    Process.Start(taskRunnerExe.QuoteIfNeeded(), arguments);
                }
                else
                {
                    var taskRunnerDll = Path.Combine(directory, "TaskRunner.dll");
                    Process.Start("dotnet", $"{taskRunnerDll.QuoteIfNeeded()} {arguments}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private string GetTaskTargetFramework(Task task)
        {
            try
            {
                var taskDllPath = task.FromAssembly;
                if (!File.Exists(taskDllPath))
                {
                    // `FromAssembly` might be an assembly name instead of a file path, e.g. "Microsoft.Build.Tasks.Core, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
                    // Assuming that this assembly is an MSBuild assembly which is in the same directory as the MSBuild executable
                    var msbuildDirectory = Path.GetDirectoryName(task.GetNearestParent<Build>()?.MSBuildExecutablePath);
                    if (msbuildDirectory is not null)
                    {
                        var assemblyName = new AssemblyName(task.FromAssembly);
                        taskDllPath = Path.Combine(msbuildDirectory, $"{assemblyName.Name}.dll");
                        if (!File.Exists(taskDllPath))
                        {
                            return null;
                        }
                    }
                }

                var module = AssemblyDefinition.ReadAssembly(taskDllPath);
                var attribute = module.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "TargetFrameworkAttribute");
                if (attribute == null || attribute.ConstructorArguments.Count != 1)
                {
                    return null;
                }

                var targetFramework = attribute.ConstructorArguments[0].Value as string;
                return targetFramework;
            }
            catch
            {
                return null;
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var node = treeView.SelectedItem as BaseNode;
            var nameValueVisibility = node is NameValueNode ? Visibility.Visible : Visibility.Collapsed;
            copyNameItem.Visibility = nameValueVisibility;
            copyValueItem.Visibility = nameValueVisibility;
            viewSourceItem.Visibility = CanView(node) ? Visibility.Visible : Visibility.Collapsed;
            viewFullTextItem.Visibility = HasFullText(node) ? Visibility.Visible : Visibility.Collapsed;
            openFileItem.Visibility = CanOpenFile(node) ? Visibility.Visible : Visibility.Collapsed;
            copyFilePathItem.Visibility = node is Import || (node is IHasSourceFile file && !string.IsNullOrEmpty(file.SourceFilePath))
                ? Visibility.Visible
                : Visibility.Collapsed;
            var hasChildren = node is TreeNode t && t.HasChildren;
            var hasChildrenVisibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
            copySubtreeItem.Visibility = hasChildrenVisibility;
            viewSubtreeTextItem.Visibility = hasChildrenVisibility;
            copyChildrenItem.Visibility = hasChildrenVisibility;
            sortChildrenItem.Visibility = hasChildrenVisibility;
            filterChildrenItem.Visibility = hasChildrenVisibility;
            preprocessItem.Visibility = node is IPreprocessable p && preprocessedFileManager.CanPreprocess(p) ? Visibility.Visible : Visibility.Collapsed;
            searchNuGetItem.Visibility = node is IProjectOrEvaluation ? Visibility.Visible : Visibility.Collapsed;
            Visibility canRun = Build?.LogFilePath != null && node is Task ? Visibility.Visible : Visibility.Collapsed;
            runItem.Visibility = canRun;
            debugItem.Visibility = canRun;
            hideItem.Visibility = node is TreeNode ? Visibility.Visible : Visibility.Collapsed;

            if (node is SearchableItem searchItem)
            {
                searchThisNode.Visibility = Visibility.Visible;
                searchThisNode.Header = $"Search {searchItem.SearchText}";
            }
            else
            {
                searchThisNode.Visibility = Visibility.Collapsed;
            }

            bool isFavorite = IsFavorite(node);
            favoriteItem.Visibility = !isFavorite ? Visibility.Visible : Visibility.Collapsed;
            unfavoriteItem.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;

            if (node is TimedNode timedNode)
            {
                showTimeItem.Visibility = Visibility.Visible;
                searchInSubtreeItem.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
                excludeSubtreeFromSearchItem.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
                goToTimeLineItem.Visibility = Visibility.Visible;
                goToTracingItem.Visibility = Visibility.Visible;
                excludeNodeByNameFromSearch.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
                searchInNodeByNameItem.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;

                if (excludeNodeByNameFromSearch.Visibility == Visibility.Visible)
                {
                    excludeNodeByNameFromSearch.Header = $"Exclude '{timedNode.Name}' from search";
                }

                if (searchInNodeByNameItem.Visibility == Visibility.Visible)
                {
                    searchInNodeByNameItem.Header = $"Search in '{timedNode.Name}'";
                }
            }
            else
            {
                showTimeItem.Visibility = Visibility.Collapsed;
                searchInSubtreeItem.Visibility = Visibility.Collapsed;
                excludeSubtreeFromSearchItem.Visibility = Visibility.Collapsed;
                goToTimeLineItem.Visibility = Visibility.Collapsed;
                goToTracingItem.Visibility = Visibility.Collapsed;
                excludeNodeByNameFromSearch.Visibility = Visibility.Collapsed;
                searchInNodeByNameItem.Visibility = Visibility.Collapsed;
            }

            searchMenuGroup.Visibility = searchMenuGroup.Items.Cast<MenuItem>().Any(p => p.Visibility != Visibility.Collapsed) ?
                Visibility.Visible : Visibility.Collapsed;

            copyMenuGroup.Visibility = copyMenuGroup.Items.Cast<MenuItem>().Any(p => p.Visibility != Visibility.Collapsed) ?
                Visibility.Visible : Visibility.Collapsed;

            gotoMenuGroup.Visibility = gotoMenuGroup.Items.Cast<MenuItem>().Any(p => p.Visibility != Visibility.Collapsed) ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void SharedTreeContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var node = ActiveTreeView.SelectedItem as BaseNode;
            if (node == null)
            {
                return;
            }

            bool isFavorite = IsFavorite(node);
            favoriteSharedItem.Visibility = !isFavorite ? Visibility.Visible : Visibility.Collapsed;
            unfavoriteSharedItem.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
        }

        private object FindInFiles(string searchText, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<(string, IEnumerable<(int, string)>)>();

            foreach (var file in archiveFile.Files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var haystack = file.Value;
                var resultsInFile = haystack.Find(searchText);
                if (resultsInFile.Count > 0)
                {
                    results.Add((file.Key, resultsInFile.Select(lineNumber => (lineNumber, haystack.GetLineText(lineNumber)))));
                }
            }

            return results;
        }

        private IEnumerable BuildFindResults(object resultsObject, bool moreAvailable = false)
        {
            if (resultsObject == null)
            {
                return null;
            }

            var results = resultsObject as IEnumerable<(string, IEnumerable<(int, string)>)>;

            var root = new Folder();

            // root.Children.Add(new Message { Text = "Elapsed " + Elapsed.ToString() });

            if (results != null)
            {
                foreach (var file in results)
                {
                    var folder = new SourceFile()
                    {
                        Name = Path.GetFileName(file.Item1),
                        SourceFilePath = file.Item1,
                        IsExpanded = true
                    };
                    root.AddChild(folder);
                    foreach (var line in file.Item2)
                    {
                        var sourceFileLine = new SourceFileLine()
                        {
                            LineNumber = line.Item1 + 1,
                            LineText = line.Item2
                        };
                        folder.AddChild(sourceFileLine);
                    }
                }
            }

            if (!root.HasChildren && !string.IsNullOrEmpty(findInFilesControl.SearchText))
            {
                root.Children.Add(new Message
                {
                    Text = "No results found."
                });
            }

            return root.Children;
        }

        private string filePathSeparator;

        private void PopulateFilesTab()
        {
            var root = new Folder();

            foreach (var file in archiveFile.Files.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                AddSourceFile(root, file.Key);
            }

            foreach (var taskAssembly in Build.TaskAssemblies)
            {
                var filePath = ArchiveFile.CalculateArchivePath(taskAssembly.Key);
                var sourceFile = AddSourceFile(root, filePath);
                foreach (var taskName in taskAssembly.Value.OrderBy(s => s))
                {
                    var task = new Task
                    {
                        Name = taskName
                    };
                    sourceFile.AddChild(task);
                }

                sourceFile.SortChildren();
            }

            foreach (var subFolder in root.Children.OfType<Folder>())
            {
                CompressTree(subFolder);
            }

            filesTree.DisplayItems(root.Children);
            filesTree.GotFocus += (s, a) => ActiveTreeView = filesTree.ResultsList;
            filesTree.ContextMenu = filesTreeContextMenu;
        }

        private SourceFile AddSourceFile(Folder folder, string filePath)
        {
            if (filePathSeparator == null)
            {
                if (filePath.Contains(":") || (!filePath.StartsWith("\\") && !filePath.StartsWith("/")))
                {
                    filePathSeparator = "\\";
                }
                else
                {
                    filePathSeparator = "/";
                }
            }

            var parts = filePath.Split('\\', '/');
            return AddSourceFile(folder, filePath, parts, 0);
        }

        private void CompressTree(Folder parent)
        {
            if (parent.Children.Count == 1 && parent.Children[0] is Folder subfolder)
            {
                parent.Children.Clear();
                var grandchildren = subfolder.Children.ToArray();
                subfolder.Children.Clear();
                foreach (var grandChild in grandchildren)
                {
                    parent.Children.Add(grandChild);
                }

                if (filePathSeparator == null)
                {
                    filePathSeparator = "\\";
                }

                parent.Name = parent.Name + filePathSeparator + subfolder.Name;
                CompressTree(parent);
            }
            else
            {
                foreach (var subFolder in parent.Children.OfType<Folder>())
                {
                    CompressTree(subFolder);
                }
            }
        }

        private SourceFile AddSourceFile(Folder folder, string filePath, string[] parts, int index)
        {
            if (index == parts.Length - 1)
            {
                var file = new SourceFile
                {
                    SourceFilePath = filePath,
                    Name = parts[index]
                };

                if (PlatformUtilities.HasThreads)
                {
                    TPLTask.Run(() => AddTargetsAsync(filePath, file));
                }
                else
                {
                    AddTargets(filePath, file);
                }

                folder.AddChild(file);
                return file;
            }
            else
            {
                var folderName = parts[index];

                // root of the Mac file system
                if (string.IsNullOrEmpty(folderName) && index == 0)
                {
                    folderName = "/";
                }

                var subfolder = folder.GetOrCreateNodeWithName<Folder>(folderName);
                subfolder.IsExpanded = true;
                return AddSourceFile(subfolder, filePath, parts, index + 1);
            }
        }

        private async TPLTask AddTargetsAsync(string filePath, SourceFile file)
        {
            var targets = GetTargets(filePath).OrderBy(t => t).ToArray();
            if (targets.Length == 0)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                foreach (var target in targets)
                {
                    file.AddChild(new Target
                    {
                        Name = target,
                        SourceFilePath = filePath
                    });
                }
            });
        }

        private void AddTargets(string filePath, SourceFile file)
        {
            var targets = GetTargets(filePath).OrderBy(t => t).ToArray();
            if (targets.Length == 0)
            {
                return;
            }

            foreach (var target in targets)
            {
                file.AddChild(new Target
                {
                    Name = target,
                    SourceFilePath = filePath
                });
            }
        }

        private static HashSet<string> nonMSBuildExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dll",
            ".json",
            ".rsp",
            ".sln",
            ".tmp",
            ".txt",
            ".user"
        };

        private IEnumerable<string> GetTargets(string file)
        {
            var extension = Path.GetExtension(file);
            if (nonMSBuildExtensions.Contains(extension))
            {
                yield break;
            }

            var content = sourceFileResolver.GetSourceFileText(file);
            if (content == null)
            {
                yield break;
            }

            var contentText = content.Text;

            if (!Utilities.LooksLikeXml(contentText))
            {
                yield break;
            }

            if (contentText.IndexOf("<Target", StringComparison.Ordinal) == -1)
            {
                yield break;
            }

            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(contentText);
            }
            catch (Exception)
            {
                yield break;
            }

            if (doc.DocumentElement == null)
            {
                yield break;
            }

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("x", doc.DocumentElement.NamespaceURI);
            var xmlNodeList = doc.SelectNodes(@"//x:Project/x:Target[@Name]", nsmgr);
            if (xmlNodeList == null)
            {
                yield break;
            }

            foreach (XmlNode selectNode in xmlNodeList)
            {
                yield return selectNode.Attributes["Name"].Value;
            }
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
        internal static TimeSpan PrecalculationDuration;

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
                if (this.centralTabControl.SelectedIndex == 0)
                {
                    SelectItem(node);
                    treeView.Focus();
                }
                else if (this.centralTabControl.SelectedIndex == 2)
                {
                    if (node is TimedNode tnode)
                    {
                        tracing.GoToTimedNode(tnode);
                    }
                }

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
                UpdateProjectContext(item);
                UpdateFindContent();
            }
        }

        private void TreeView_GetFocus(object sender, RoutedEventArgs e)
        {
            ActiveTreeView = treeView;
        }

        private void ResultsList_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var treeView = sender as TreeView;
            if (treeView != null && treeView.SelectedItem is ProxyNode proxy)
            {
                var item = proxy.Original;
                if (item != null)
                {
                    SelectItem(item);
                }
            }
        }

        public void UpdateProjectContext(object item)
        {
            if (item is not BaseNode node)
            {
                return;
            }

            var project = node.GetNearestParentOrSelf<Project>();
            if (project != null)
            {
                //projectEvaluation = Build.FindEvaluation(project.EvaluationId);
                //if (projectEvaluation != null && (projectEvaluation.FindChild<Folder>(Strings.Items) != null || projectEvaluation.FindChild<Folder>(Strings.Properties) != null))
                //{
                //    SetProjectContext(projectEvaluation);
                //    return;
                //}

                //if (project.FindChild<Folder>(Strings.Items) != null || project.FindChild<Folder>(Strings.Properties) != null)
                //{
                //    SetProjectContext(project);
                //    return;
                //}

                SetProjectContext(project);
                return;
            }

            var projectEvaluation = node.GetNearestParentOrSelf<ProjectEvaluation>();
            if (projectEvaluation != null && (projectEvaluation.FindChild<Folder>(Strings.Items) != null || projectEvaluation.FindChild<Folder>(Strings.Properties) != null))
            {
                SetProjectContext(projectEvaluation);
                return;
            }

            SetProjectContext(null);
        }

        private object projectContext;

        public void SetProjectContext(object contents)
        {
            projectContext = contents;
            propertiesAndItemsContext.Content = contents;
            var visibility = contents != null ? Visibility.Visible : Visibility.Collapsed;
            projectContextBorder.Visibility = visibility;
            propertiesAndItemsControl.TopPanel.Visibility = visibility;
        }

        public IProjectOrEvaluation GetProjectContext()
        {
            return projectContext as IProjectOrEvaluation;
        }

        public void UpdateBreadcrumb(object item)
        {
            var node = item as BaseNode;
            IEnumerable<object> chain = node?.GetParentChainIncludingThis();
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
            if (!Build.Succeeded)
            {
                var firstError = Build.FirstError;
                if (firstError != null)
                {
                    SelectItem(firstError);
                    treeView.Focus();
                }

                if (InitialSearchText == null)
                {
                    InitialSearchText = "$error";
                }
            }

            if (InitialSearchText != null)
            {
                searchLogControl.SearchText = InitialSearchText;
            }

            FocusSearch();
        }

        public string InitialSearchText { get; set; }

        public void SelectItem(BaseNode item)
        {
            var parentChain = item.GetParentChainIncludingThis();
            if (!parentChain.Any())
            {
                return;
            }

            SelectTree();
            treeView.SelectContainerFromItem<object>(parentChain);
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key >= Key.A && args.Key <= Key.Z && args.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                SelectItemByKey((char)('A' + args.Key - Key.A));
                args.Handled = true;
            }
        }

        public bool IsFindVisible
        {
            get => findControl.Visibility == Visibility.Visible;
            set
            {
                findControl.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    findTextBox.Focus();
                    UpdateFindContent();
                }
                else
                {
                    ActiveTreeView.Focus();
                }
            }
        }

        private TreeNode TryGetTreeNodeForFind()
        {
            BaseNode node = treeView.SelectedItem as BaseNode;
            if (node is Property or Metadata)
            {
                node = node.Parent;
            }
            else if (node is Item item && !item.HasChildren)
            {
                node = node.Parent;
            }

            var treeNode = node as TreeNode;
            if (treeNode != null && treeNode.HasChildren)
            {
                return treeNode;
            }

            return null;
        }

        private void UpdateFindContent()
        {
            if (!IsFindVisible)
            {
                return;
            }

            var treeNode = TryGetTreeNodeForFind();
            if (treeNode != null)
            {
                findLabel.Content = $"Filter children of: {TextUtilities.ShortenValue(GetText(treeNode), trimPrompt: "", maxChars: 100)}";
                if (nodeFilters.TryGetValue(treeNode, out var filter))
                {
                    findTextBox.Text = filter;
                }
                else
                {
                    findTextBox.Clear();
                }
            }
            else
            {
                IsFindVisible = false;
            }
        }

        private void SearchTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (string.IsNullOrEmpty(searchLogControl.SearchText))
                {
                    ActiveTreeView.Focus();
                    e.Handled = true;
                }
                else
                {
                    searchLogControl.searchTextBox.Clear();
                    e.Handled = true;
                }
            }
        }

        private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled == true)
            {
                return;
            }

            if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (e.Key == Key.Escape)
                {
                    if (!string.IsNullOrEmpty(findTextBox.Text))
                    {
                        findTextBox.Clear();
                    }
                    else
                    {
                        IsFindVisible = false;
                    }

                    e.Handled = true;
                }

                if (e.Key == Key.Return)
                {
                    IsFindVisible = false;
                    e.Handled = true;
                }
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.F)
                {
                    IsFindVisible = false;
                    FocusSearch();
                    e.Handled = true;
                }
            }
        }

        private void findTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = findTextBox.Text.Trim();

            var node = TryGetTreeNodeForFind();
            if (node == null)
            {
                return;
            }

            ApplyFilter(node, searchText);
        }

        private readonly Dictionary<TreeNode, string> nodeFilters = new Dictionary<TreeNode, string>();

        private void ApplyFilter(TreeNode node, string text)
        {
            if (nodeFilters.TryGetValue(node, out var existing))
            {
                if (existing == text)
                {
                    return;
                }
            }

            var children = node.Children;
            var view = CollectionViewSource.GetDefaultView(children);
            if (string.IsNullOrEmpty(text))
            {
                view.Filter = null;
                nodeFilters.Remove(node);
            }
            else
            {
                nodeFilters[node] = text;
                view.Filter = o =>
                {
                    if (o is not BaseNode childNode)
                    {
                        return false;
                    }

                    var nodeText = GetText(childNode);
                    if (nodeText != null && nodeText.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    return false;
                };
            }
        }

        private int characterMatchPrefixLength = 0;

        private void SelectItemByKey(char ch)
        {
            ch = char.ToLowerInvariant(ch);

            var selectedItem = treeView.SelectedItem as BaseNode;
            if (selectedItem == null)
            {
                return;
            }

            var parent = selectedItem.Parent;
            if (parent == null)
            {
                return;
            }

            var selectedText = GetText(selectedItem);
            var prefix = selectedText.Substring(0, Math.Min(characterMatchPrefixLength, selectedText.Length));

            var items = selectedItem.EnumerateSiblingsCycle();

        search:
            foreach (var item in items)
            {
                var text = GetText(item);
                if (text == null)
                {
                    continue;
                }

                if (characterMatchPrefixLength < text.Length && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var character = text[characterMatchPrefixLength];
                    if (char.ToLowerInvariant(character) == ch)
                    {
                        characterMatchPrefixLength++;
                        SelectItem(item);
                        return;
                    }
                }
            }

            if (characterMatchPrefixLength > 0)
            {
                characterMatchPrefixLength = 0;
                prefix = "";
                items = items.Skip(1).Concat(items.Take(1));
                goto search;
            }
        }

        private string GetText(BaseNode node)
        {
            return node.Title ?? node.ToString();
        }

        public void FocusSearch()
        {
            if (leftPaneTabControl.SelectedItem == searchLogTab)
            {
                searchLogControl.searchTextBox.Focus();
            }
            else if (leftPaneTabControl.SelectedItem == findInFilesTab)
            {
                findInFilesControl.searchTextBox.Focus();
            }
            else if (leftPaneTabControl.SelectedItem == propertiesAndItemsTab)
            {
                propertiesAndItemsControl.searchTextBox.Focus();
            }
        }

        public void SelectSearchTab()
        {
            leftPaneTabControl.SelectedItem = searchLogTab;
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
            var tree = ActiveTreeView;
            var treeNode = tree?.SelectedItem;
            if (treeNode != null)
            {
                var text = treeNode.ToString();
                CopyToClipboard(text);
            }
        }

        public void CopySubtree(TreeView tree = null)
        {
            tree ??= ActiveTreeView;
            if (tree == null)
            {
                return;
            }

            if (tree.SelectedItem is BaseNode treeNode)
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(treeNode);
                CopyToClipboard(text);
            }
        }

        public void ViewSubtreeText()
        {
            if (treeView.SelectedItem is BaseNode treeNode)
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(treeNode);
                DisplayText(text, treeNode.ToString());
            }
        }

        public void ShowTimeAndDuration()
        {
            if (treeView.SelectedItem is TimedNode timedNode)
            {
                var text = timedNode.GetTimeAndDurationText(fullPrecision: true);
                DisplayText(text, timedNode.ToString());
            }
        }

        private readonly HashSet<BaseNode> favorites = new HashSet<BaseNode>();

        public void AddToFavorites()
        {
            var node = ActiveTreeView?.SelectedItem as BaseNode;
            if (node != null)
            {
                if (node is ProxyNode proxy)
                {
                    node = proxy.Original ?? node;
                }

                if (favorites.Add(node))
                {
                    RefreshFavorites();
                }
            }
        }

        public void RemoveFromFavorites()
        {
            var node = ActiveTreeView?.SelectedItem as BaseNode;
            if (node != null)
            {
                if (node is ProxyNode proxy)
                {
                    node = proxy.Original ?? node;
                }

                if (favorites.Remove(node))
                {
                    RefreshFavorites();
                }
            }
        }

        public bool IsFavorite(BaseNode node)
        {
            if (node is ProxyNode proxy)
            {
                node = proxy.Original ?? node;
            }

            return favorites.Contains(node);
        }

        public void RefreshFavorites()
        {
            var list = favorites.OrderBy(f =>
            {
                if (f is TimedNode timed)
                {
                    return timed.Index;
                }

                return 0;
            }).Select(f =>
            {
                var searchResult = new SearchResult(f);
                return searchResult;
            }).ToArray();

            var tree = ResultTree.BuildResultTree(
                list,
                addDuration: false,
                addWhenNoResults: () => new Note { Text = "Right-click any node and Favorite it to add it here" });

            SortByIndex(tree);

            favoritesTree.DisplayItems(tree.Children);
        }

        private static int CompareByIndex(BaseNode l, BaseNode r)
        {
            if (l == r)
            {
                return 0;
            }

            if (l is null || r is null)
            {
                return -1;
            }

            if (l is TimedNode timedLeft && r is TimedNode timedRight)
            {
                return timedLeft.Index - timedRight.Index;
            }

            return 0;
        }

        private void SortByIndex(TreeNode node)
        {
            node.SortChildren(CompareByIndex);
            SortByIndex(node.Children);
        }

        private void SortByIndex(IList<BaseNode> list)
        {
            foreach (var child in list)
            {
                if (child is TreeNode childNode)
                {
                    SortByIndex(childNode);
                }
            }
        }

        public void OpenFile()
        {
            if (treeView.SelectedItem is Import import)
            {
                DisplayFile(import.ImportedProjectFilePath, evaluation: import.GetNearestParent<ProjectEvaluation>());
            }
        }

        public void CopyFilePath()
        {
            string toCopy = null;
            if (treeView.SelectedItem is Import import)
            {
                toCopy = import.ImportedProjectFilePath;
            }
            else if (treeView.SelectedItem is IHasSourceFile file)
            {
                toCopy = file.SourceFilePath;
            }

            if (toCopy != null)
            {
                CopyToClipboard(toCopy);
            }
        }

        public void SearchInSubtree()
        {
            if (treeView.SelectedItem is TimedNode treeNode)
            {
                searchLogControl.SearchText += $" under(${treeNode.Index})";
                SelectSearchTab();
            }
        }

        public void SearchInNodeByName()
        {
            if (treeView.SelectedItem is TimedNode treeNode)
            {
                searchLogControl.SearchText += $" under(${treeNode.TypeName} {treeNode.Name})";
                SelectSearchTab();
            }
        }

        public void SearchThisNode()
        {
            if (treeView.SelectedItem is SearchableItem searchNode)
            {
                searchLogControl.SearchText = searchNode.SearchText;
                SelectSearchTab();
            }
        }

        public void ExcludeSubtreeFromSearch()
        {
            if (treeView.SelectedItem is TimedNode treeNode)
            {
                searchLogControl.SearchText += $" notunder(${treeNode.Index})";
                SelectSearchTab();
            }
        }

        public void ExcludeNodeByNameFromSearch()
        {
            if (treeView.SelectedItem is TimedNode treeNode)
            {
                searchLogControl.SearchText += $" notunder(${treeNode.TypeName} {treeNode.Name})";
                SelectSearchTab();
            }
        }

        public void GoToTimeLine()
        {
            var treeNode = treeView.SelectedItem as TimedNode;
            if (treeNode != null)
            {
                centralTabControl.SelectedIndex = 1;
                this.timeline.GoToTimedNode(treeNode);
            }
        }

        public void GoToTracing()
        {
            if (treeView.SelectedItem is TimedNode treeNode)
            {
                centralTabControl.SelectedIndex = 2;

                // need to dispatch because at the time this is called the visual tree isn't laid out yet,
                // so all the sizes are 0 and we don't know what's the real (X,Y) position to navigate to.
                // Dispatching will run it after the layout when all the sizes have been set.
                Dispatcher.InvokeAsync(() =>
                {
                    this.tracing.GoToTimedNode(treeNode);
                }, DispatcherPriority.Background);
            }
        }

        public void CopyChildren()
        {
            if (treeView.SelectedItem is TreeNode node && node.HasChildren)
            {
                // the texts have \n for line breaks, expand to \r\n
                var children = node.Children.Select(c => c.ToString().Replace("\n", "\r\n"));
                var text = string.Join(Environment.NewLine, children);
                CopyToClipboard(text);
            }
        }

        public void SortChildren()
        {
            var selectedItem = treeView.SelectedItem;
            if (selectedItem is TreeNode treeNode)
            {
                treeNode.SortChildren();
            }
        }

        public void FilterChildren()
        {
            IsFindVisible = !IsFindVisible;
        }

        private void CopyAll(TreeView tree = null)
        {
            tree ??= ActiveTreeView;
            if (tree == null)
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in tree.Items.OfType<BaseNode>())
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(item);
                sb.Append(text);
                if (!text.Contains("\n"))
                {
                    sb.AppendLine();
                }

                if (sb.Length > Microsoft.Build.Logging.StructuredLogger.StringWriter.MaxStringLength)
                {
                    break;
                }
            }

            CopyToClipboard(sb.ToString());
        }

        private void CopyPaths(TreeView tree = null)
        {
            tree = tree ?? ActiveTreeView;
            if (tree == null)
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in tree.Items.OfType<TreeNode>())
            {
                item.VisitAllChildren<BaseNode>(s =>
                {
                    if (sb.Length > Microsoft.Build.Logging.StructuredLogger.StringWriter.MaxStringLength)
                    {
                        return;
                    }

                    if (s is SourceFile file && !string.IsNullOrEmpty(file.SourceFilePath))
                    {
                        sb.AppendLine(file.SourceFilePath);
                    }
                });
            }

            CopyToClipboard(sb.ToString());
        }

        private static void CopyToClipboard(string text)
        {
            try
            {
                text = text.Replace("\0", "");
                Clipboard.SetText(text);
            }
            catch (Exception)
            {
                // clipboard API is notoriously flaky
            }
        }

        public void CopyName()
        {
            var nameValueNode = treeView.SelectedItem as NameValueNode;
            if (nameValueNode != null)
            {
                CopyToClipboard(nameValueNode.Name);
            }
        }

        public void CopyValue()
        {
            var nameValueNode = treeView.SelectedItem as NameValueNode;
            if (nameValueNode != null)
            {
                CopyToClipboard(nameValueNode.Value);
            }
        }

        private void MoveSelectionOut(BaseNode node)
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

        private void OnItemKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }

            if (args.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (args.Key == Key.Delete)
                {
                    Delete();
                    args.Handled = true;
                }

                if (args.Key == Key.Space || args.Key == Key.Return)
                {
                    var treeNode = GetNode(args);
                    if (treeNode != null)
                    {
                        args.Handled = Invoke(treeNode) || ViewFullText(treeNode);
                    }
                }

                if (args.Key == Key.Escape)
                {
                    if (IsFindVisible)
                    {
                        IsFindVisible = false;
                        args.Handled = true;
                    }
                    else if (documentWell.IsVisible)
                    {
                        documentWell.Hide();
                        args.Handled = true;
                    }
                }
            }

            if (args.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (args.Key == Key.C)
                {
                    Copy();
                    args.Handled = true;
                }

                if (args.Key == Key.F)
                {
                    if (IsFindVisible)
                    {
                        IsFindVisible = false;
                        args.Handled = true;
                    }
                    else if (TryGetTreeNodeForFind() != null)
                    {
                        IsFindVisible = true;
                        args.Handled = true;
                    }
                }
            }
        }

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs args)
        {
            // workaround for http://stackoverflow.com/a/36244243/37899
            var treeViewItem = sender as TreeViewItem;
            if (!treeViewItem.IsSelected)
            {
                return;
            }

            var node = GetNode(args);
            if (node != null)
            {
                args.Handled = Invoke(node) || ViewFullText(node);
            }
        }

        private bool CanView(BaseNode node)
        {
            return node is AbstractDiagnostic
                || node is Project
                || (node is Target t && t.SourceFilePath != null && sourceFileResolver.HasFile(t.SourceFilePath))
                || (node is Task task && task.Parent is Target parentTarget && sourceFileResolver.HasFile(parentTarget.SourceFilePath))
                || (node is IHasSourceFile ihsf && ihsf.SourceFilePath != null && sourceFileResolver.HasFile(ihsf.SourceFilePath));
        }

        private bool HasFullText(BaseNode node)
        {
            return (node is NameValueNode nvn && nvn.IsValueShortened)
                || (node is NamedNode nn && nn.IsNameShortened)
                || (node is TextNode tn && tn.IsTextShortened);
        }

        private bool CanOpenFile(BaseNode node)
        {
            return node is Import i && sourceFileResolver.HasFile(i.ImportedProjectFilePath);
        }

        private bool ViewFullText(BaseNode treeNode)
        {
            if (treeNode == null)
            {
                return false;
            }

            switch (treeNode)
            {
                case NameValueNode nameValueNode when nameValueNode.IsValueShortened:
                    return DisplayText(nameValueNode.Value, nameValueNode.Name);
                case TextNode textNode when textNode.IsTextShortened:
                    return DisplayText(textNode.Text, textNode.ShortenedText ?? textNode.TypeName);
                case NamedNode namedNode when namedNode.IsNameShortened:
                    return DisplayText(namedNode.Name, namedNode.ShortenedName ?? namedNode.TypeName);
                default:
                    return false;
            }
        }

        private bool Invoke(BaseNode treeNode)
        {
            if (treeNode == null)
            {
                return false;
            }

            try
            {
                switch (treeNode)
                {
                    case AbstractDiagnostic diagnostic:
                        var path = diagnostic.File;
                        if (!DisplayFile(path, diagnostic.LineNumber) &&
                            path != null &&
                            !Path.IsPathRooted(path) &&
                            diagnostic.ProjectFile != null)
                        {
                            // path must be relative, try to normalize:
                            path = Path.Combine(Path.GetDirectoryName(diagnostic.ProjectFile), path);
                            return DisplayFile(path, diagnostic.LineNumber, diagnostic.ColumnNumber);
                        }

                        break;
                    case Target target:
                        return DisplayTarget(target.SourceFilePath, target.Name);
                    case Task task:
                        return DisplayTask(task);
                    case AddItem addItem:
                        return DisplayAddRemoveItem(addItem.Parent, addItem.LineNumber ?? 0);
                    case RemoveItem removeItem:
                        return DisplayAddRemoveItem(removeItem.Parent, removeItem.LineNumber ?? 0);
                    case Item item when item.Parent is AddItem parentAddItem && parentAddItem.Name == "EmbedInBinlog":
                        return DisplayEmbeddedFile(item);
                    case Item item when
                        item.Parent == null &&
                        searchLogControl.SearchText.Contains("$copy") &&
                        searchLogControl.ResultsList.ItemsSource is IEnumerable<BaseNode> results &&
                        results.Contains(item):
                        return SearchForFullPath(item.Text);
                    case IHasSourceFile hasSourceFile when hasSourceFile.SourceFilePath != null:
                        int line = 0;
                        var hasLine = hasSourceFile as IHasLineNumber;
                        if (hasLine != null)
                        {
                            line = hasLine.LineNumber ?? 0;
                        }

                        ProjectEvaluation evaluation = null;
                        if (hasSourceFile is TreeNode node)
                        {
                            // TODO: https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/392
                            evaluation = node.GetNearestParentOrSelf<ProjectEvaluation>();

                            if (evaluation == null && node is Project project)
                            {
                                evaluation = Build.FindEvaluation(project.EvaluationId);
                            }
                        }

                        return DisplayFile(hasSourceFile.SourceFilePath, line, evaluation: evaluation);
                    case SourceFileLine sourceFileLine when sourceFileLine.Parent is SourceFile sourceFile && sourceFile.SourceFilePath != null:
                        return DisplayFile(sourceFile.SourceFilePath, sourceFileLine.LineNumber);
                    default:
                        return false;
                }
            }
            catch
            {
                // in case our guessing of file path goes awry
            }

            return false;
        }

        private bool SearchForFullPath(string filePath)
        {
            var text = searchLogControl.SearchText;
            var matcher = new NodeQueryMatcher(text);
            if (matcher.Terms.Count == 1 &&
                matcher.Terms[0].Word is string substring &&
                filePath.IndexOf(substring, StringComparison.OrdinalIgnoreCase) != -1)
            {
                text = text.Replace(substring, filePath);
                searchLogControl.SearchText = text;
                return true;
            }

            return false;
        }

        private bool DisplayEmbeddedFile(Item item)
        {
            string path = item.Text;

            var candidates = sourceFileResolver.ArchiveFile.FindFileNames(path).ToArray();
            if (candidates.Length == 1)
            {
                return DisplayFile(candidates[0]);
            }

            return false;
        }

        public bool DisplayFile(string sourceFilePath, int lineNumber = 0, int column = 0, ProjectEvaluation evaluation = null)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return false;
            }

            string preprocessableFilePath = sourceFilePath;

            Action preprocess = null;
            if (evaluation != null)
            {
                preprocess = preprocessedFileManager.GetPreprocessAction(preprocessableFilePath, PreprocessedFileManager.GetEvaluationKey(evaluation));
            }

            documentWell.DisplaySource(preprocessableFilePath, text.Text, lineNumber, column, preprocess, navigationHelper);
            return true;
        }

        public bool DisplayText(string text, string caption = null)
        {
            caption = TextUtilities.SanitizeFileName(caption);
            documentWell.DisplaySource(caption ?? "Text", text, displayPath: false);
            return true;
        }

        private bool DisplayAddRemoveItem(TreeNode parent, int line)
        {
            if (parent is not Target target)
            {
                return false;
            }

            string sourceFilePath = target.SourceFilePath;
            return DisplayFile(sourceFilePath, line);
        }

        private bool DisplayTask(Task task)
        {
            var sourceFilePath = task.SourceFilePath;
            var parent = task.Parent;
            var name = task.Name;
            if (parent is not Target target)
            {
                return DisplayFile(sourceFilePath);
            }

            if (task.LineNumber.HasValue && task.LineNumber.Value > 0)
            {
                return DisplayFile(sourceFilePath, task.LineNumber.Value);
            }

            return DisplayTarget(sourceFilePath, target.Name, name);
        }

        public bool DisplayTarget(string sourceFilePath, string targetName, string taskName = null)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return false;
            }

            var xml = text.XmlRoot.Root;
            IXmlElement root = xml;
            int startPosition = 0;
            int line = 0;

            // work around a bug in Xml Parser where a virtual parent is created around the root element
            // when the root element is preceded by trivia (comment)
            if (root.Name == null && root.Elements.FirstOrDefault() is IXmlElement firstElement && firstElement.Name == "Project")
            {
                root = firstElement;
            }

            foreach (var element in root.Elements)
            {
                if (element.Name == "Target" && element.Attributes != null)
                {
                    var nameAttribute = element.AsSyntaxElement.Attributes.FirstOrDefault(a => a.Name == "Name" && a.Value == targetName);
                    if (nameAttribute != null)
                    {
                        startPosition = nameAttribute.ValueNode.Start;

                        if (taskName != null)
                        {
                            var tasks = element.Elements.Where(e => e.Name == taskName).ToArray();
                            if (tasks.Length == 1)
                            {
                                startPosition = tasks[0].AsSyntaxElement.NameNode.Start;
                            }
                        }

                        break;
                    }
                }
            }

            if (startPosition > 0)
            {
                line = text.GetLineNumberFromPosition(startPosition);
            }

            return DisplayFile(sourceFilePath, line + 1);
        }

        private static BaseNode GetNode(RoutedEventArgs args)
        {
            var treeViewItem = args.Source as TreeViewItem;
            var node = treeViewItem?.DataContext as BaseNode;
            return node;
        }

        public IEnumerable BuildResultTree(object resultsObject, bool moreAvailable = false)
        {
            return BuildResultTree(resultsObject, moreAvailable, addDuration: true);
        }

        public IEnumerable BuildResultTree(object resultsObject, bool moreAvailable = false, bool addDuration = true)
        {
            var folder = ResultTree.BuildResultTree(
                resultsObject,
                Elapsed,
                addDuration: addDuration,
                addWhenNoResults: () => new Message { Text = "No results found." });

            if (moreAvailable)
            {
                var count = resultsObject is ICollection<SearchResult> results
                    ? results.Count
                    : folder.Children.Count;

                var showAllButton = new ButtonNode
                {
                    Text = $"Showing first {count} results. Show all results instead (slow)."
                };

                showAllButton.OnClick = () =>
                {
                    showAllButton.IsEnabled = false;
                    searchLogControl.TriggerSearch(searchLogControl.SearchText, int.MaxValue);
                };

                folder.AddChildAtBeginning(showAllButton);
            }

            return folder.Children;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            SelectedTreeViewItem = e.OriginalSource as TreeViewItem;
        }

        public void DisplayStats()
        {
            if (!File.Exists(LogFilePath))
            {
                return;
            }

            var statsRoot = Build.FindChild<Folder>(f => f.Name.StartsWith(Strings.Statistics));
            if (statsRoot != null)
            {
                return;
            }

            var recordStats = BinlogStats.Calculate(this.LogFilePath);
            var records = recordStats.CategorizedRecords;

            statsRoot = DisplayRecordStats(records, Build);

            var treeStats = Build.Statistics;
            DisplayTreeStats(statsRoot, treeStats, recordStats);

            //var histogram = GetHistogram(recordStats.StringSizes);
            //var histogramNode = new CustomContentNode { Content = histogram };
            //statsRoot.AddChild(histogramNode);

            statsRoot.AddChild(new Property { Name = "BinlogFileFormatVersion", Value = Build.FileFormatVersion.ToString() });
            statsRoot.AddChild(new Property { Name = "FileSize", Value = recordStats.FileSize.ToString("N0") });
            statsRoot.AddChild(new Property { Name = "UncompressedStreamSize", Value = recordStats.UncompressedStreamSize.ToString("N0") });
            statsRoot.AddChild(new Property { Name = "RecordCount", Value = recordStats.RecordCount.ToString("N0") });

            // This is interesting. Technically WPF needs the Build.Children collection to be observable
            // to properly refresh the list when we add a new node. However it suffices to replace the Children
            // collection with something else (and I assume it gets a new collection view and that is
            // equivalent to a Reset.
            // We could literally just do children = children.ToArray() and that would be sufficient here.
            // Note that there's no need to actually change it to observable collection this late.
            // Since the children have already mutated by the time we're setting this. Ideally we should be
            // setting this at the beginning.
            // It also doesn't seem like raising PropertyChanged for Children is necessary.
            // See https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/487 for details.
            Build.MakeChildrenObservable();
        }

        private UIElement GetHistogram(List<int> values)
        {
            double width = 800;
            double height = 200;
            var fill = Brushes.AliceBlue;
            var border = Brushes.LightBlue;

            var canvas = new Canvas()
            {
                Width = width,
                Height = height,
                Background = Brushes.Azure
            };

            double max = values.Max();
            int count = values.Count;

            for (double x = 0; x < width; x++)
            {
                int startIndex = (int)(x / width * count);
                int endIndex = (int)((x + 1) / width * count);
                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                if (endIndex >= count)
                {
                    endIndex = count;
                }

                if (startIndex >= endIndex)
                {
                    continue;
                }

                int sum = 0;
                int maxInBucket = 0;
                for (int i = startIndex; i < endIndex; i++)
                {
                    int value = values[i];
                    sum += value;
                    if (maxInBucket < value)
                    {
                        maxInBucket = value;
                    }
                }

                if (sum == 0)
                {
                    continue;
                }

                double y = height * maxInBucket / max;
                if (y < height / 2)
                {
                    y += 5;
                }

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = 1,
                    Height = y,
                    Fill = fill,
                    Stroke = border
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, height - y);

                canvas.Children.Add(rect);
            }

            return canvas;
        }

        private void DisplayTreeStats(Folder statsRoot, BuildStatistics treeStats, BinlogStats recordStats)
        {
            var buildMessageNode = statsRoot.FindChild<Folder>(n => n.Name.StartsWith("BuildMessage", StringComparison.Ordinal));
            var taskInputsNode = buildMessageNode.FindChild<Folder>(n => n.Name.StartsWith("Task Input", StringComparison.Ordinal));
            var taskOutputsNode = buildMessageNode.FindChild<Folder>(n => n.Name.StartsWith("Task Output", StringComparison.Ordinal));

            AddTopTasks(treeStats.TaskParameterMessagesByTask, taskInputsNode);
            AddTopTasks(treeStats.OutputItemMessagesByTask, taskOutputsNode);

            if (recordStats.StringTotalSize > 0)
            {
                var strings = new Item
                {
                    Text = BinlogStats.GetString("Strings", recordStats.StringTotalSize, recordStats.StringCount, recordStats.StringLargest)
                };
                var allStringText = string.Join("\n", recordStats.AllStrings);
                var allStrings = new Message { Text = allStringText };

                statsRoot.AddChild(strings);
                strings.AddChild(allStrings);
            }

            if (recordStats.NameValueListTotalSize > 0)
            {
                statsRoot.AddChild(new Message
                {
                    Text = BinlogStats.GetString(
                        "NameValueLists",
                        recordStats.NameValueListTotalSize,
                        recordStats.NameValueListCount,
                        recordStats.NameValueListLargest)
                });
            }

            if (recordStats.BlobTotalSize > 0)
            {
                statsRoot.AddChild(new Message
                {
                    Text = BinlogStats.GetString("Blobs", recordStats.BlobTotalSize, recordStats.BlobCount, recordStats.BlobLargest)
                });
            }
        }

        private static void AddTopTasks(Dictionary<string, List<string>> messagesByTask, Folder node)
        {
            var topTaskParameters = messagesByTask
                .Select(kvp => (taskName: kvp.Key, count: kvp.Value.Count, totalSize: kvp.Value.Sum(s => s.Length * 2), largest: kvp.Value.Max(s => s.Length) * 2))
                .OrderByDescending(kvp => kvp.totalSize)
                .Take(20);
            foreach (var task in topTaskParameters)
            {
                var name = BinlogStats.GetString(task.taskName, task.totalSize, task.count, task.largest);
                node.AddChild(new Folder { Name = name });
            }
        }

        private Folder DisplayRecordStats(BinlogStats.RecordsByType stats, TreeNode parent, string titlePrefix = "")
        {
            var node = parent.GetOrCreateNodeWithName<Folder>(titlePrefix + stats.ToString());

            if (stats.CategorizedRecords != null)
            {
                foreach (var records in stats.CategorizedRecords)
                {
                    DisplayRecordStats(records, node);
                }
            }

            var top = stats.Records.Take(300).ToArray();
            foreach (var item in top)
            {
                if (item.Args is EnvironmentVariableReadEventArgs env)
                {
                    node.AddChild(new Property { Name = env.EnvironmentVariableName, Value = env.Message });
                }
                else if (item.Args is BuildMessageEventArgs buildMessage)
                {
                    node.AddChild(new Message { Text = buildMessage.Message });
                }
            }

            return node;
        }

        public override string ToString()
        {
            return Build?.ToString();
        }
    }
}
