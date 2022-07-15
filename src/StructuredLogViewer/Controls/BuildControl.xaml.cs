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

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public Build Build { get; set; }
        public TreeViewItem SelectedTreeViewItem { get; private set; }
        public string LogFilePath => Build?.LogFilePath;

        private ScrollViewer scrollViewer;

        private SourceFileResolver sourceFileResolver;
        private ArchiveFileResolver archiveFile => sourceFileResolver.ArchiveFile;
        private PreprocessedFileManager preprocessedFileManager;
        private NavigationHelper navigationHelper;

        private MenuItem copyItem;
        private MenuItem copySubtreeItem;
        private MenuItem viewSubtreeTextItem;
        private MenuItem searchInSubtreeItem;
        private MenuItem excludeSubtreeFromSearchItem;
        private MenuItem goToTimeLineItem;
        private MenuItem goToTracingItem;
        private MenuItem copyChildrenItem;
        private MenuItem sortChildrenItem;
        private MenuItem copyNameItem;
        private MenuItem copyValueItem;
        private MenuItem viewSourceItem;
        private MenuItem viewFullTextItem;
        private MenuItem openFileItem;
        private MenuItem copyFilePathItem;
        private MenuItem preprocessItem;
        private MenuItem runItem;
        private MenuItem debugItem;
        private MenuItem hideItem;
        private MenuItem copyAllItem;
        private MenuItem showTimeItem;
        private ContextMenu sharedTreeContextMenu;
        private ContextMenu filesTreeContextMenu;

        public TreeView ActiveTreeView;

        private PropertiesAndItemsSearch propertiesAndItemsSearch;

        public BuildControl(Build build, string logFilePath)
        {
            InitializeComponent();

            UpdateWatermark();

            searchLogControl.ExecuteSearch = (searchText, maxResults, cancellationToken) =>
            {
                var search = new Search(
                    new[] { Build },
                    Build.StringTable.Instances,
                    maxResults,
                    SettingsService.MarkResultsInTree
                    //, Build.StringTable // disable validation in production
                    );
                var results = search.FindNodes(searchText, cancellationToken);
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
            propertiesAndItemsControl.WatermarkDisplayed += () =>
            {
                UpdatePropertiesAndItemsWatermark();
            };
            propertiesAndItemsControl.RecentItemsCategory = "PropertiesAndItems";

            SetProjectContext(null);

            VirtualizingPanel.SetIsVirtualizing(treeView, SettingsService.EnableTreeViewVirtualization);

            DataContext = build;
            Build = build;

            if (build.SourceFilesArchive != null)
            {
                // first try to see if the source archive was embedded in the log
                sourceFileResolver = new SourceFileResolver(build.SourceFiles.Values);
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

            sharedTreeContextMenu = new ContextMenu();
            copyAllItem = new MenuItem() { Header = "Copy All" };
            copyAllItem.Click += (s, a) => CopyAll();
            sharedTreeContextMenu.Items.Add(copyAllItem);

            filesTreeContextMenu = new ContextMenu();
            var filesCopyAll = new MenuItem { Header = "Copy All" };
            filesCopyAll.Click += (s, a) => CopyAll(filesTree.ResultsList);
            var filesCopyPaths = new MenuItem { Header = "Copy file paths" };
            filesCopyPaths.Click += (s, a) => CopyPaths(filesTree.ResultsList);
            filesTreeContextMenu.Items.Add(filesCopyAll);
            filesTreeContextMenu.Items.Add(filesCopyPaths);

            var contextMenu = new ContextMenu();
            contextMenu.Opened += ContextMenu_Opened;
            copyItem = new MenuItem() { Header = "Copy" };
            copySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            viewSubtreeTextItem = new MenuItem() { Header = "View subtree text" };
            searchInSubtreeItem = new MenuItem() { Header = "Search in subtree" };
            excludeSubtreeFromSearchItem = new MenuItem() { Header = "Exclude subtree from search" };
            goToTimeLineItem = new MenuItem() { Header = "Go to timeline" };
            goToTracingItem = new MenuItem() { Header = "Go to tracing" };
            copyChildrenItem = new MenuItem() { Header = "Copy children" };
            sortChildrenItem = new MenuItem() { Header = "Sort children" };
            copyNameItem = new MenuItem() { Header = "Copy name" };
            copyValueItem = new MenuItem() { Header = "Copy value" };
            viewSourceItem = new MenuItem() { Header = "View source" };
            viewFullTextItem = new MenuItem { Header = "View full text" };
            showTimeItem = new MenuItem() { Header = "Show time and duration" };
            openFileItem = new MenuItem() { Header = "Open File" };
            copyFilePathItem = new MenuItem() { Header = "Copy file path" };
            preprocessItem = new MenuItem() { Header = "Preprocess" };
            hideItem = new MenuItem() { Header = "Hide" };
            runItem = new MenuItem() { Header = "Run" };
            debugItem = new MenuItem() { Header = "Debug" };
            copyItem.Click += (s, a) => Copy();
            copySubtreeItem.Click += (s, a) => CopySubtree();
            viewSubtreeTextItem.Click += (s, a) => ViewSubtreeText();
            searchInSubtreeItem.Click += (s, a) => SearchInSubtree();
            excludeSubtreeFromSearchItem.Click += (s, a) => ExcludeSubtreeFromSearch();
            goToTimeLineItem.Click += (s, a) => GoToTimeLine();
            goToTracingItem.Click += (s, a) => GoToTracing();
            copyChildrenItem.Click += (s, a) => CopyChildren();
            sortChildrenItem.Click += (s, a) => SortChildren();
            copyNameItem.Click += (s, a) => CopyName();
            copyValueItem.Click += (s, a) => CopyValue();
            viewSourceItem.Click += (s, a) => Invoke(treeView.SelectedItem as BaseNode);
            viewFullTextItem.Click += (s, a) => ViewFullText(treeView.SelectedItem as BaseNode);
            showTimeItem.Click += (s, a) => ShowTimeAndDuration();
            openFileItem.Click += (s, a) => OpenFile();
            copyFilePathItem.Click += (s, a) => CopyFilePath();
            preprocessItem.Click += (s, a) => Preprocess(treeView.SelectedItem as IPreprocessable);
            runItem.Click += (s, a) => Run(treeView.SelectedItem as Task, debug: false);
            debugItem.Click += (s, a) => Run(treeView.SelectedItem as Task, debug: true);
            hideItem.Click += (s, a) => Delete();

            contextMenu.Items.Add(runItem);
            contextMenu.Items.Add(debugItem);
            contextMenu.Items.Add(viewSourceItem);
            contextMenu.Items.Add(viewFullTextItem);
            contextMenu.Items.Add(openFileItem);
            contextMenu.Items.Add(preprocessItem);
            contextMenu.Items.Add(searchInSubtreeItem);
            contextMenu.Items.Add(excludeSubtreeFromSearchItem);
            contextMenu.Items.Add(goToTimeLineItem);
            contextMenu.Items.Add(goToTracingItem);
            contextMenu.Items.Add(copyItem);
            contextMenu.Items.Add(copySubtreeItem);
            contextMenu.Items.Add(copyFilePathItem);
            contextMenu.Items.Add(viewSubtreeTextItem);
            contextMenu.Items.Add(copyChildrenItem);
            contextMenu.Items.Add(sortChildrenItem);
            contextMenu.Items.Add(copyNameItem);
            contextMenu.Items.Add(copyValueItem);
            contextMenu.Items.Add(showTimeItem);
            contextMenu.Items.Add(hideItem);

            var existingTreeViewItemStyle = (Style)Application.Current.Resources[typeof(TreeViewItem)];
            var treeViewItemStyle = new Style(typeof(TreeViewItem), existingTreeViewItemStyle);
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.VisibilityProperty, new Binding("IsVisible") { Mode = BindingMode.TwoWay, Converter = new BooleanToVisibilityConverter() }));

            treeViewItemStyle.Setters.Add(new EventSetter(MouseDoubleClickEvent, (MouseButtonEventHandler)OnItemDoubleClick));
            treeViewItemStyle.Setters.Add(new EventSetter(PreviewMouseRightButtonDownEvent, (MouseButtonEventHandler)OnPreviewMouseRightButtonDown));
            treeViewItemStyle.Setters.Add(new EventSetter(RequestBringIntoViewEvent, (RequestBringIntoViewEventHandler)TreeViewItem_RequestBringIntoView));
            treeViewItemStyle.Setters.Add(new EventSetter(KeyDownEvent, (KeyEventHandler)OnItemKeyDown));

            treeView.ContextMenu = contextMenu;
            treeView.ItemContainerStyle = treeViewItemStyle;
            treeView.KeyDown += TreeView_KeyDown;
            treeView.SelectedItemChanged += TreeView_SelectedItemChanged;
            treeView.GotFocus += (s, a) => ActiveTreeView = treeView;

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
                build.Unseal();
#if DEBUG
                text = build.StringTable.Intern(text);
#endif
                build.AddChild(new Note { Text = text });
                build.Seal();
            }

            breadCrumb.SelectionChanged += BreadCrumb_SelectionChanged;

            Loaded += BuildControl_Loaded;

            preprocessedFileManager = new PreprocessedFileManager(this.Build, sourceFileResolver);
            preprocessedFileManager.DisplayFile += filePath => DisplayFile(filePath);

            navigationHelper = new NavigationHelper(Build, sourceFileResolver);
            navigationHelper.OpenFileRequested += filePath => DisplayFile(filePath);

            centralTabControl.SelectionChanged += CentralTabControl_SelectionChanged;
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
                var timeline = new Timeline(Build, analyzeCpp: true);
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
            "csc $task",
            "ResolveAssemblyReference $task",
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
            "$copytask",
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
            string watermarkText1 = @"Type in the search box to search. Press Ctrl+F to focus the search box. Results (up to 1000) will display here.

Search for multiple words separated by space (space means AND). Enclose multiple words in double-quotes """" to search for the exact phrase. A single word in quotes means exact match (turns off substring search).

Use syntax like '$property Prop' to narrow results down by item kind. Supported kinds: ";

            string watermarkText2 = @"Use the under(FILTER) clause to only include results where any of the nodes in the parent chain matches the FILTER. Use project(...) to filter by parent project. Examples:
 • $task csc under($project Core)
 • Copying file project(ProjectA)

Append [[$time]], [[$start]] and/or [[$end]] to show times and/or durations and sort the results by start time or duration descending (for tasks, targets and projects).

Examples:
";

            var watermark = new TextBlock();
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

            var recentSearches = SettingsService.GetRecentSearchStrings();
            if (recentSearches.Any())
            {
                watermark.Inlines.Add(@"
Recent:
");

                foreach (var recentSearch in recentSearches.Where(s => !searchExamples.Contains(s) && !nodeKinds.Contains(s)))
                {
                    watermark.Inlines.Add(MakeLink(recentSearch, searchLogControl));
                }
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
Recent:
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
            var visibility = node is NameValueNode ? Visibility.Visible : Visibility.Collapsed;
            copyNameItem.Visibility = visibility;
            copyValueItem.Visibility = visibility;
            viewSourceItem.Visibility = CanView(node) ? Visibility.Visible : Visibility.Collapsed;
            viewFullTextItem.Visibility = HasFullText(node) ? Visibility.Visible : Visibility.Collapsed;
            openFileItem.Visibility = CanOpenFile(node) ? Visibility.Visible : Visibility.Collapsed;
            copyFilePathItem.Visibility = node is Import || (node is IHasSourceFile file && !string.IsNullOrEmpty(file.SourceFilePath))
                ? Visibility.Visible
                : Visibility.Collapsed;
            var hasChildren = node is TreeNode t && t.HasChildren;
            copySubtreeItem.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
            viewSubtreeTextItem.Visibility = copySubtreeItem.Visibility;
            showTimeItem.Visibility = node is TimedNode ? Visibility.Visible : Visibility.Collapsed;
            searchInSubtreeItem.Visibility = hasChildren && node is TimedNode ? Visibility.Visible : Visibility.Collapsed;
            excludeSubtreeFromSearchItem.Visibility = hasChildren && node is TimedNode ? Visibility.Visible : Visibility.Collapsed;
            goToTimeLineItem.Visibility = node is TimedNode ? Visibility.Visible : Visibility.Collapsed;
            goToTracingItem.Visibility = node is TimedNode ? Visibility.Visible : Visibility.Collapsed;
            copyChildrenItem.Visibility = copySubtreeItem.Visibility;
            sortChildrenItem.Visibility = copySubtreeItem.Visibility;
            preprocessItem.Visibility = node is IPreprocessable p && preprocessedFileManager.CanPreprocess(p) ? Visibility.Visible : Visibility.Collapsed;
            Visibility canRun = Build?.LogFilePath != null && node is Task ? Visibility.Visible : Visibility.Collapsed;
            runItem.Visibility = canRun;
            debugItem.Visibility = canRun;
            hideItem.Visibility = node is TreeNode ? Visibility.Visible : Visibility.Collapsed;
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

                foreach (var target in GetTargets(filePath))
                {
                    file.AddChild(new Target
                    {
                        Name = target,
                        SourceFilePath = filePath
                    });
                }

                file.SortChildren();

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

        private IEnumerable<string> GetTargets(string file)
        {
            if (file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
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
                UpdateProjectContext(item);
            }
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
            scrollViewer = treeView.Template.FindName("_tv_scrollviewer_", treeView) as ScrollViewer;

            if (!Build.Succeeded)
            {
                var firstError = Build.FindFirstInSubtreeIncludingSelf<Error>();
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
            if (args.Key == Key.Delete)
            {
                Delete();
                args.Handled = true;
            }
            else if (args.Key == Key.C && args.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                CopySubtree();
                args.Handled = true;
            }
            else if (args.Key >= Key.A && args.Key <= Key.Z && args.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                SelectItemByKey((char)('A' + args.Key - Key.A));
                args.Handled = true;
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

            string GetText(BaseNode node)
            {
                if (node is IHasTitle hasTitle)
                {
                    return hasTitle.Title;
                }
                else
                {
                    return node.ToString();
                }
            }
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
            var treeNode = treeView.SelectedItem;
            if (treeNode != null)
            {
                var text = treeNode.ToString();
                CopyToClipboard(text);
            }
        }

        public void CopySubtree()
        {
            if (treeView.SelectedItem is BaseNode treeNode)
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

        public void ExcludeSubtreeFromSearch()
        {
            if (treeView.SelectedItem is TimedNode treeNode)
            {
                searchLogControl.SearchText += $" notunder(${treeNode.Index})";
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

        private void CopyAll(TreeView tree = null)
        {
            tree = tree ?? ActiveTreeView;
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
                if (documentWell.IsVisible)
                {
                    documentWell.Hide();
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

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs args)
        {
            if (sender is TreeViewItem treeViewItem)
            {
                treeViewItem.IsSelected = true;
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
                    return DisplayText(textNode.Text, textNode.Name ?? textNode.GetType().Name);
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

        public bool DisplayFile(string sourceFilePath, int lineNumber = 0, int column = 0, ProjectEvaluation evaluation = null)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return false;
            }

            string preprocessableFilePath = Utilities.InsertMissingDriveSeparator(sourceFilePath);

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
            var folder = ResultTree.BuildResultTree(resultsObject, moreAvailable, Elapsed);

            if (moreAvailable)
            {
                var showAllButton = new ButtonNode
                {
                    Text = $"Showing first {folder.Children.Count} results. Show all results instead (slow)."
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

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (scrollViewer == null)
            {
                return;
            }

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

            Build.Unseal();

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
            foreach (var records in stats.CategorizedRecords)
            {
                DisplayRecordStats(records, node);
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
