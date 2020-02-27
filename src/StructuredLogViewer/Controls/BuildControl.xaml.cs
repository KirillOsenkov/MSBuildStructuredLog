using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Language.Xml;

using StructuredLogViewer.Core.ProjectGraph;

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public Build Build { get; set; }
        public TreeViewItem SelectedTreeViewItem { get; private set; }
        public string LogFilePath { get; set; }

        private ScrollViewer scrollViewer;

        private SourceFileResolver sourceFileResolver;
        private ArchiveFileResolver archiveFile => sourceFileResolver.ArchiveFile;
        private PreprocessedFileManager preprocessedFileManager;

        private MenuItem copyItem;
        private MenuItem copySubtreeItem;
        private MenuItem viewSubtreeTextItem;
        private MenuItem searchInSubtreeItem;
        private MenuItem copyChildrenItem;
        private MenuItem sortChildrenItem;
        private MenuItem copyNameItem;
        private MenuItem copyValueItem;
        private MenuItem viewItem;
        private MenuItem openFileItem;
        private MenuItem preprocessItem;
        private MenuItem runItem;
        private MenuItem debugItem;
        private MenuItem hideItem;
        private MenuItem copyAllItem;
        private ContextMenu sharedTreeContextMenu;

        public TreeView ActiveTreeView;

        public BuildControl(Build build, string logFilePath)
        {
            InitializeComponent();

            UpdateWatermark();

            searchLogControl.ExecuteSearch = (searchText, maxResults) =>
            {
                var search = new Search(Build, maxResults);
                var results = search.FindNodes(searchText);
                return results;
            };
            searchLogControl.ResultsTreeBuilder = BuildResultTree;
            searchLogControl.WatermarkDisplayed += () =>
            {
                Search.ClearSearchResults(Build);
                UpdateWatermark();
            };

            VirtualizingPanel.SetIsVirtualizing(treeView, SettingsService.EnableTreeViewVirtualization);

            DataContext = build;
            Build = build;

            LogFilePath = logFilePath;

            if (build.SourceFilesArchive != null)
            {
                // first try to see if the source archive was embedded in the log
                sourceFileResolver = new SourceFileResolver(build.SourceFilesArchive);
            }
            else
            {
                // otherwise try to read from the .zip file on disk if present
                sourceFileResolver = new SourceFileResolver(logFilePath);
            }

            sharedTreeContextMenu = new ContextMenu();
            copyAllItem = new MenuItem() { Header = "Copy All" };
            copyAllItem.Click += (s, a) => CopyAll();
            sharedTreeContextMenu.Items.Add(copyAllItem);

            var contextMenu = new ContextMenu();
            contextMenu.Opened += ContextMenu_Opened;
            copyItem = new MenuItem() { Header = "Copy" };
            copySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            viewSubtreeTextItem = new MenuItem() { Header = "View subtree text" };
            searchInSubtreeItem = new MenuItem() { Header = "Search in subtree" };
            copyChildrenItem = new MenuItem() { Header = "Copy children" };
            sortChildrenItem = new MenuItem() { Header = "Sort children" };
            copyNameItem = new MenuItem() { Header = "Copy name" };
            copyValueItem = new MenuItem() { Header = "Copy value" };
            viewItem = new MenuItem() { Header = "View" };
            openFileItem = new MenuItem() { Header = "Open File" };
            preprocessItem = new MenuItem() { Header = "Preprocess" };
            hideItem = new MenuItem() { Header = "Hide" };
            runItem = new MenuItem() { Header = "Run" };
            debugItem = new MenuItem() { Header = "Debug" };
            copyItem.Click += (s, a) => Copy();
            copySubtreeItem.Click += (s, a) => CopySubtree();
            viewSubtreeTextItem.Click += (s, a) => ViewSubtreeText();
            searchInSubtreeItem.Click += (s, a) => SearchInSubtree();
            copyChildrenItem.Click += (s, a) => CopyChildren();
            sortChildrenItem.Click += (s, a) => SortChildren();
            copyNameItem.Click += (s, a) => CopyName();
            copyValueItem.Click += (s, a) => CopyValue();
            viewItem.Click += (s, a) => Invoke(treeView.SelectedItem as ParentedNode);
            openFileItem.Click += (s, a) => OpenFile();
            preprocessItem.Click += (s, a) => Preprocess(treeView.SelectedItem as IPreprocessable);
            runItem.Click += (s, a) => Run(treeView.SelectedItem as Task, debug: false);
            debugItem.Click += (s, a) => Run(treeView.SelectedItem as Task, debug: true);
            hideItem.Click += (s, a) => Delete();

            contextMenu.Items.Add(runItem);
            contextMenu.Items.Add(debugItem);
            contextMenu.Items.Add(viewItem);
            contextMenu.Items.Add(openFileItem);
            contextMenu.Items.Add(preprocessItem);
            contextMenu.Items.Add(searchInSubtreeItem);
            contextMenu.Items.Add(copyItem);
            contextMenu.Items.Add(copySubtreeItem);
            contextMenu.Items.Add(viewSubtreeTextItem);
            contextMenu.Items.Add(copyChildrenItem);
            contextMenu.Items.Add(sortChildrenItem);
            contextMenu.Items.Add(copyNameItem);
            contextMenu.Items.Add(copyValueItem);
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

                var filesNote = new TextBlock();
                var text =
@"This log contains the full text of projects and imported files used during the build.
You can use the 'Files' tab in the bottom left to view these files and the 'Find in Files' tab for full-text search.
For many nodes in the tree (Targets, Tasks, Errors, Projects, etc) pressing SPACE or ENTER or double-clicking 
on the node will navigate to the corresponding source code associated with the node.

More functionality is available from the right-click context menu for each node.
Right-clicking a project node may show the 'Preprocess' option if the version of MSBuild was at least 15.3.";
                build.Unseal();
                build.AddChild(new Note { Text = text });
                build.Seal();
            }

            breadCrumb.SelectionChanged += BreadCrumb_SelectionChanged;

            Loaded += BuildControl_Loaded;

            preprocessedFileManager = new PreprocessedFileManager(this.Build, sourceFileResolver);
            preprocessedFileManager.DisplayFile += filePath => DisplayFile(filePath);

            PopulateTimeline();
            PopulateProjectGraph();
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
            var timeline = new Timeline(Build);
            this.timeline.BuildControl = this;
            this.timeline.SetTimeline(timeline);
        }

        private void PopulateProjectGraph()
        {
            var graph = new MsaglProjectGraphConstructor().FromBuild(Build);
            projectGraphControl.BuildControl = this;
            projectGraphControl.SetGraph(graph);
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
            "$task $time"
        };

        private static string[] nodeKinds = new[]
        {
            "$project",
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
            "$import",
            "$noimport"
        };

        private void UpdateWatermark()
        {
            string watermarkText1 = @"Type in the search box to search. Press Ctrl+F to focus the search box. Results (up to 1000) will display here.

Search for multiple words separated by space (space means AND). Enclose multiple words in double-quotes """" to search for the exact phrase.

Use syntax like '$property Prop' to narrow results down by item kind. Supported kinds: ";

            string watermarkText2 = @"Use the under(FILTER) clause to filter results to only the nodes where any of the parent nodes in the parent chain matches the FILTER. Examples:
 • $task csc under($project Core)
 • Copying file under(Parent)

Append $time to show durations and sort the results by duration descending (for tasks, targets and projects).

Examples:
";

            Inline MakeLink(string query, string before = " • ", string after = "\r\n")
            {
                var hyperlink = new Hyperlink(new Run(query));
                hyperlink.Click += (s, e) => searchLogControl.SearchText = query;

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
                watermark.Inlines.Add(MakeLink(nodeKind, before: null, after: null));
            }

            watermark.Inlines.Add(new LineBreak());
            watermark.Inlines.Add(new LineBreak());

            watermark.Inlines.Add(watermarkText2);

            foreach (var example in searchExamples)
            {
                watermark.Inlines.Add(MakeLink(example));
            }

            var recentSearches = SettingsService.GetRecentSearchStrings();
            if (recentSearches.Any())
            {
                watermark.Inlines.Add(@"
Recent:
");

                foreach (var recentSearch in recentSearches.Where(s => !searchExamples.Contains(s) && !nodeKinds.Contains(s)))
                {
                    watermark.Inlines.Add(MakeLink(recentSearch));
                }
            }

            searchLogControl.WatermarkContent = watermark;
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

            var taskRunner = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "TaskRunner.exe");
            if (!File.Exists(taskRunner))
            {
                MessageBox.Show("File not found: " + taskRunner);
                return;
            }

            try
            {
                Process.Start(taskRunner.QuoteIfNeeded(), $"{logFilePath} {task.Index} pause{(debug ? " debug" : "")}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var node = treeView.SelectedItem as ParentedNode;
            var visibility = node is NameValueNode ? Visibility.Visible : Visibility.Collapsed;
            copyNameItem.Visibility = visibility;
            copyValueItem.Visibility = visibility;
            viewItem.Visibility = CanView(node) ? Visibility.Visible : Visibility.Collapsed;
            openFileItem.Visibility = CanOpenFile(node) ? Visibility.Visible : Visibility.Collapsed;
            var hasChildren = node is TreeNode t && t.HasChildren;
            copySubtreeItem.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
            viewSubtreeTextItem.Visibility = copySubtreeItem.Visibility;
            searchInSubtreeItem.Visibility = hasChildren && node is TimedNode ? Visibility.Visible : Visibility.Collapsed;
            copyChildrenItem.Visibility = copySubtreeItem.Visibility;
            sortChildrenItem.Visibility = copySubtreeItem.Visibility;
            preprocessItem.Visibility = node is IPreprocessable p && preprocessedFileManager.CanPreprocess(p) ? Visibility.Visible : Visibility.Collapsed;
            Visibility canRun = Build?.LogFilePath != null && node is Task ? Visibility.Visible : Visibility.Collapsed;
            runItem.Visibility = canRun;
            debugItem.Visibility = canRun;
        }

        private object FindInFiles(string searchText, int maxResults = int.MaxValue)
        {
            var results = new List<(string, IEnumerable<(int, string)>)>();

            foreach (var file in archiveFile.Files)
            {
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

        private void PopulateFilesTab()
        {
            var root = new Folder();

            foreach (var file in archiveFile.Files.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                var parts = file.Key.Split('\\');
                AddSourceFile(root, file.Key, parts, 0);
            }

            foreach (var subFolder in root.Children.OfType<Folder>())
            {
                CompressTree(subFolder);
            }

            filesTree.DisplayItems(root.Children);
            filesTree.GotFocus += (s, a) => ActiveTreeView = filesTree.ResultsList;
            filesTree.ContextMenu = sharedTreeContextMenu;
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

                parent.Name = Path.Combine(parent.Name, subfolder.Name);
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

        private void AddSourceFile(Folder folder, string filePath, string[] parts, int index)
        {
            if (index == parts.Length - 1)
            {
                var file = new SourceFile()
                {
                    SourceFilePath = filePath,
                    Name = parts[index]
                };

                foreach (var target in GetTargets(filePath))
                {
                    file.Children.Add(new Target()
                    {
                        Name = target,
                        SourceFilePath = filePath
                    });
                }

                folder.AddChild(file);
            }
            else
            {
                var subfolder = folder.GetOrCreateNodeWithName<Folder>(parts[index]);
                subfolder.IsExpanded = true;
                AddSourceFile(subfolder, filePath, parts, index + 1);
            }
        }

        private IEnumerable<string> GetTargets(string file)
        {
            if (file.EndsWith(".targets") == false)
            {
                yield break;
            }

            var content = sourceFileResolver.GetSourceFileText(file);
            var contentText = content.Text;
            if (string.IsNullOrWhiteSpace(contentText))
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
            }
        }

        private void ResultsList_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var proxy = searchLogControl.ResultsList.SelectedItem as ProxyNode;
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
            IEnumerable<object> chain = parentedNode?.GetParentChainIncludingThis();
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

        public void SelectItem(ParentedNode item)
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

            var selectedItem = treeView.SelectedItem as ParentedNode;
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
                        item.IsSelected = true;
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

            string GetText(ParentedNode node)
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
            var treeNode = treeView.SelectedItem;
            if (treeNode != null)
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(treeNode);
                CopyToClipboard(text);
            }
        }

        public void ViewSubtreeText()
        {
            var treeNode = treeView.SelectedItem;
            if (treeNode != null)
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(treeNode);
                DisplayText(text, treeNode.ToString());
            }
        }

        public void OpenFile()
        {
            if (treeView.SelectedItem is Import import)
            {
                var preprocessContext = preprocessedFileManager.GetProjectEvaluationContext(import);
                DisplayFile(import.ImportedProjectFilePath, preprocessContext: preprocessContext);
            }
        }

        public void SearchInSubtree()
        {
            var treeNode = treeView.SelectedItem as TimedNode;
            if (treeNode != null)
            {
                searchLogControl.SearchText += $" under(${treeNode.Index})";
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

        private void CopyAll()
        {
            var tree = ActiveTreeView;
            if (tree == null)
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in tree.Items)
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(item);
                sb.AppendLine(text);
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

        private void OnItemKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Space || args.Key == Key.Return)
            {
                var treeNode = GetNode(args);
                if (treeNode != null)
                {
                    args.Handled = Invoke(treeNode);
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
                args.Handled = Invoke(node);
            }
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs args)
        {
            var treeViewItem = sender as TreeViewItem;
            if (treeViewItem != null)
            {
                treeViewItem.IsSelected = true;
            }
        }

        private bool CanView(ParentedNode node)
        {
            return node is AbstractDiagnostic
                || node is Project
                || (node is Target t && t.SourceFilePath != null && sourceFileResolver.HasFile(t.SourceFilePath))
                || (node is Task task && task.Parent is Target parentTarget && sourceFileResolver.HasFile(parentTarget.SourceFilePath))
                || (node is IHasSourceFile ihsf && ihsf.SourceFilePath != null && sourceFileResolver.HasFile(ihsf.SourceFilePath))
                || (node is NameValueNode nvn && nvn.IsValueShortened)
                || (node is TextNode tn && tn.IsTextShortened);
        }

        private bool CanOpenFile(ParentedNode node)
        {
            return node is Import i && sourceFileResolver.HasFile(i.ImportedProjectFilePath);
        }

        private bool Invoke(ParentedNode treeNode)
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

                        if (diagnostic.IsTextShortened)
                        {
                            return DisplayText(diagnostic.Text, diagnostic.GetType().Name);
                        }

                        break;
                    case Target target:
                        return DisplayTarget(target.SourceFilePath, target.Name);
                    case Task task:
                        return DisplayTask(task.SourceFilePath, task.Parent, task.Name);
                    case IHasSourceFile hasSourceFile when hasSourceFile.SourceFilePath != null:
                        int line = 0;
                        var hasLine = hasSourceFile as IHasLineNumber;
                        if (hasLine != null)
                        {
                            line = hasLine.LineNumber ?? 0;
                        }

                        return DisplayFile(hasSourceFile.SourceFilePath, line);
                    case SourceFileLine sourceFileLine when sourceFileLine.Parent is SourceFile sourceFile && sourceFile.SourceFilePath != null:
                        return DisplayFile(sourceFile.SourceFilePath, sourceFileLine.LineNumber);
                    case NameValueNode nameValueNode when nameValueNode.IsValueShortened:
                        return DisplayText(nameValueNode.Value, nameValueNode.Name);
                    case TextNode textNode when textNode.IsTextShortened:
                        return DisplayText(textNode.Text, textNode.Name ?? textNode.GetType().Name);
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

        public bool DisplayFile(string sourceFilePath, int lineNumber = 0, int column = 0, string preprocessContext = null)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return false;
            }

            string preprocessableFilePath = Utilities.InsertMissingDriveSeparator(sourceFilePath);

            Action preprocess = null;
            if (preprocessContext != null)
            {
                preprocess = preprocessedFileManager.GetPreprocessAction(preprocessableFilePath, preprocessContext);
            }

            documentWell.DisplaySource(preprocessableFilePath, text.Text, lineNumber, column, preprocess);
            return true;
        }

        public bool DisplayText(string text, string caption = null)
        {
            caption = TextUtilities.SanitizeFileName(caption);
            documentWell.DisplaySource(caption ?? "Text", text, displayPath: false);
            return true;
        }

        private bool DisplayTask(string sourceFilePath, TreeNode parent, string name)
        {
            Target target = parent as Target;
            if (target == null)
            {
                return DisplayFile(sourceFilePath);
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

        private static ParentedNode GetNode(RoutedEventArgs args)
        {
            var treeViewItem = args.Source as TreeViewItem;
            var node = treeViewItem?.DataContext as ParentedNode;
            return node;
        }

        private IEnumerable BuildResultTree(object resultsObject, bool moreAvailable = false)
        {
            var results = resultsObject as ICollection<SearchResult>;
            if (results == null)
            {
                return results;
            }

            var root = new Folder();

            if (moreAvailable)
            {
                root.Children.Add(new ButtonNode
                {
                    Text = $"Showing first {results.Count} results. Show all results instead (slow).",
                    OnClick = () => searchLogControl.TriggerSearch(searchLogControl.SearchText, int.MaxValue)
                });
            }

            root.Children.Add(new Message
            {
                Text = $"{results.Count} results. Search took: {Elapsed.ToString()}"
            });

            bool includeDuration = results.Any(r => r.Duration != default);

            if (includeDuration)
            {
                results = results.OrderByDescending(r => r.Duration).ToArray();
            }

            foreach (var result in results)
            {
                TreeNode parent = root;

                if (!includeDuration && result.Node is ParentedNode parentedNode)
                {
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
    }
}
