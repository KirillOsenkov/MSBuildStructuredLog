using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Language.Xml;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Data;
using Avalonia.Layout;

namespace StructuredLogViewer.Avalonia.Controls
{
    public partial class BuildControl : UserControl
    {
        public Build Build { get; set; }
        public TreeViewItem SelectedTreeViewItem { get; private set; }
        public string LogFilePath { get; private set; }

        private ScrollViewer scrollViewer;

        private SourceFileResolver sourceFileResolver;
        private ArchiveFileResolver archiveFile => sourceFileResolver.ArchiveFile;
        private PreprocessedFileManager preprocessedFileManager;
        private NavigationHelper navigationHelper;

        private MenuItem copyItem;
        private MenuItem copySubtreeItem;
        private MenuItem sortChildrenItem;
        private MenuItem copyNameItem;
        private MenuItem copyValueItem;
        private MenuItem viewItem;
        private MenuItem preprocessItem;
        private MenuItem hideItem;
        private MenuItem copyAllItem;
        private ContextMenu sharedTreeContextMenu;
        private TreeView treeView;
        private SearchAndResultsControl searchLogControl;
        private SearchAndResultsControl findInFilesControl;
        private TabItem filesTab;
        private TabItem findInFilesTab;
        private TreeView filesTree;
        private TabControl centralTabControl;
        private ListBox breadCrumb;
        private TabControl leftPaneTabControl;
        private TabItem searchLogTab;
        private DocumentWell documentWell;

        public TreeView ActiveTreeView;

        public BuildControl()
        {
        }

        public BuildControl(Build build, string logFilePath)
        {
            DataContext = build;

            InitializeComponent();

            UpdateWatermark();

            searchLogControl.ExecuteSearch = (searchText, maxResults, cancellationToken) =>
            {
                var search = new Search(new[] { Build }, Build.StringTable.Instances, maxResults, SettingsService.MarkResultsInTree);
                var results = search.FindNodes(searchText, cancellationToken);
                return results;
            };
            searchLogControl.ResultsTreeBuilder = BuildResultTree;
            searchLogControl.WatermarkDisplayed += () =>
            {
                Search.ClearSearchResults(Build, SettingsService.MarkResultsInTree);
                UpdateWatermark();
            };

            findInFilesControl.ExecuteSearch = FindInFiles;
            findInFilesControl.ResultsTreeBuilder = BuildFindResults;

            Build = build;

            LogFilePath = logFilePath;

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

            sharedTreeContextMenu = new ContextMenu();
            copyAllItem = new MenuItem() { Header = "Copy All" };
            copyAllItem.Click += (s, a) => CopyAll();
            sharedTreeContextMenu.AddItem(copyAllItem);

            var contextMenu = new ContextMenu();
            // TODO
            //contextMenu.Opened += ContextMenu_Opened;
            copyItem = new MenuItem() { Header = "Copy" };
            copySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            sortChildrenItem = new MenuItem() { Header = "Sort children" };
            copyNameItem = new MenuItem() { Header = "Copy name" };
            copyValueItem = new MenuItem() { Header = "Copy value" };
            viewItem = new MenuItem() { Header = "View" };
            preprocessItem = new MenuItem() { Header = "Preprocess" };
            hideItem = new MenuItem() { Header = "Hide" };
            copyItem.Click += (s, a) => Copy();
            copySubtreeItem.Click += (s, a) => CopySubtree();
            sortChildrenItem.Click += (s, a) => SortChildren();
            copyNameItem.Click += (s, a) => CopyName();
            copyValueItem.Click += (s, a) => CopyValue();
            viewItem.Click += (s, a) => Invoke(treeView.SelectedItem as BaseNode);
            preprocessItem.Click += (s, a) => Preprocess(treeView.SelectedItem as IPreprocessable);
            hideItem.Click += (s, a) => Delete();
            contextMenu.AddItem(viewItem);
            contextMenu.AddItem(preprocessItem);
            contextMenu.AddItem(copyItem);
            contextMenu.AddItem(copySubtreeItem);
            contextMenu.AddItem(sortChildrenItem);
            contextMenu.AddItem(copyNameItem);
            contextMenu.AddItem(copyValueItem);
            contextMenu.AddItem(hideItem);

            Style GetTreeViewItemStyle()
            {
                var treeViewItemStyle = new Style(s => s.OfType<TreeViewItem>());
                treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") {Mode = BindingMode.TwoWay}));
                treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding("IsSelected") {Mode = BindingMode.TwoWay}));
                treeViewItemStyle.Setters.Add(new Setter(IsVisibleProperty, new Binding("IsVisible") {Mode = BindingMode.TwoWay}));
                return treeViewItemStyle;
            }

            treeView.ContextMenu = contextMenu;
            treeView.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(treeView);
            treeView.KeyDown += TreeView_KeyDown;
            treeView.PropertyChanged += TreeView_SelectedItemChanged;
            treeView.GotFocus += (s, a) => ActiveTreeView = treeView;

            ActiveTreeView = treeView;

            searchLogControl.ResultsList.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(searchLogControl.ResultsList);
            searchLogControl.ResultsList.SelectionChanged += ResultsList_SelectionChanged;
            searchLogControl.ResultsList.GotFocus += (s, a) => ActiveTreeView = searchLogControl.ResultsList;
            searchLogControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            findInFilesControl.GotFocus += (s, a) => ActiveTreeView = findInFilesControl.ResultsList;
            findInFilesControl.ResultsList.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(findInFilesControl.ResultsList);
            findInFilesControl.ResultsList.GotFocus += (s, a) => ActiveTreeView = findInFilesControl.ResultsList;
            findInFilesControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            if (archiveFile != null)
            {
                filesTab.IsVisible = true;
                findInFilesTab.IsVisible = true;
                PopulateFilesTab();
                filesTree.Styles.Add(GetTreeViewItemStyle());
                RegisterTreeViewHandlers(filesTree);

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

            TemplateApplied += BuildControl_Loaded;

            preprocessedFileManager = new PreprocessedFileManager(Build, sourceFileResolver);
            preprocessedFileManager.DisplayFile += path => DisplayFile(path);

            navigationHelper = new NavigationHelper(Build, sourceFileResolver);
            navigationHelper.OpenFileRequested += path => DisplayFile(path);
        }

        private void RegisterTreeViewHandlers(TreeView treeView)
        {
            treeView.DoubleTapped += (o, e) =>
            {
                if (treeView.SelectedItem is BaseNode node)
                {
                    e.Handled = Invoke(node);
                }
            };

            treeView.KeyDown += (o, e) =>
            {
                if (treeView.SelectedItem is BaseNode node)
                {
                    if (e.Key == Key.Space || e.Key == Key.Return)
                    {
                        e.Handled = Invoke(node);
                    }
                }

                if (e.Key == Key.Escape)
                {
                    if (documentWell.IsVisible)
                    {
                        documentWell.Hide();
                    }
                }
            };

            // TODO:
            //treeViewItemStyle.Setters.Add(new EventSetter(PreviewMouseRightButtonDownEvent, (MouseButtonEventHandler)OnPreviewMouseRightButtonDown));
            //treeViewItemStyle.Setters.Add(new EventSetter(RequestBringIntoViewEvent, (RequestBringIntoViewEventHandler)TreeViewItem_RequestBringIntoView));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out treeView, nameof(treeView));
            this.RegisterControl(out filesTab, nameof(filesTab));
            this.RegisterControl(out findInFilesTab, nameof(findInFilesTab));
            this.RegisterControl(out filesTree, nameof(filesTree));
            this.RegisterControl(out centralTabControl, nameof(centralTabControl));
            this.RegisterControl(out breadCrumb, nameof(breadCrumb));
            this.RegisterControl(out leftPaneTabControl, nameof(leftPaneTabControl));
            this.RegisterControl(out searchLogTab, nameof(searchLogTab));

            this.RegisterControl(out SplitterPanel tabs, nameof(tabs));
            documentWell = tabs.SecondChild as DocumentWell;
            searchLogControl = searchLogTab.Content as SearchAndResultsControl;
            findInFilesControl = findInFilesTab.Content as SearchAndResultsControl;
        }

        public void SelectTree()
        {
            centralTabControl.SelectedIndex = 0;
        }

        private static string[] searchExamples = new[]
        {
            "Copying file from ",
            "Resolved file path is ",
            "There was a conflict",
            "Building target completely ",
            "is newer than output ",
            "Property reassignment: $(",
            "Importing project ",
            "was not imported by ",
            "out-of-date",
            "csc $task",
            "ResolveAssemblyReference $task",
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
            "$csctask",
            "$vbctask",
            "$fsctask"
        };

        private void UpdateWatermark()
        {
            string watermarkText1 = @"Type in the search box to search. Press Ctrl+F to focus the search box. Results (up to 1000) will display here.

Search for multiple words separated by space (space means AND). Enclose multiple words in double-quotes """" to search for the exact phrase. Enclose a single word in quotes to search for exact match (turns off substring search).

Use syntax like '$property Prop' to narrow results down by item kind. Supported kinds: ";

            string watermarkText2 = @"Use the under(FILTER) clause to filter results to only the nodes where any of the parent nodes in the parent chain matches the FILTER. Examples:
 • $task csc under($project Core)
 • Copying file under(Parent)

Examples:
";

            //Inline MakeLink(string query, string before = " \u2022 ", string after = "\r\n")
            //{
            //    var hyperlink = new Hyperlink(new Run(query));
            //    hyperlink.Click += (s, e) => searchLogControl.SearchText = query;

            //    var span = new System.Windows.Documents.Span();
            //    if (before != null)
            //    {
            //        span.Inlines.Add(new Run(before));
            //    }

            //    span.Inlines.Add(hyperlink);

            //    if (after != null)
            //    {
            //        if (after == "\r\n")
            //        {
            //            span.Inlines.Add(new LineBreak());
            //        }
            //        else
            //        {
            //            span.Inlines.Add(new Run(after));
            //        }
            //    }

            //    return span;
            //}

            var text = watermarkText1;
            text += watermarkText1;

            bool isFirst = true;
            foreach (var nodeKind in nodeKinds)
            {
                if (!isFirst)
                {
                    text += ", ";
                }

                isFirst = false;
                //text += (MakeLink(nodeKind, before: null, after: null));
                text += nodeKind;
            }

            text += Environment.NewLine + Environment.NewLine;
            text += watermarkText2;

            foreach (var example in searchExamples)
            {
                //text += (MakeLink(example));
                text += " \u2022 " + example + Environment.NewLine;
            }

            var recentSearches = SettingsService.GetRecentSearchStrings();
            if (recentSearches.Any())
            {
                text += @"
Recent:
";

                foreach (var recentSearch in recentSearches.Where(s => !searchExamples.Contains(s) && !nodeKinds.Contains(s)))
                {
                    //text += MakeLink(recentSearch));
                    text += " \u2022 " + recentSearch + Environment.NewLine;
                }
            }

            searchLogControl.WatermarkContent = new TextBlock { Text = text };
        }

        private void Preprocess(IPreprocessable preprocessable)
        {
            preprocessedFileManager.ShowPreprocessed(preprocessable);
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var node = treeView.SelectedItem as BaseNode;
            var visibility = node is NameValueNode;
            copyNameItem.IsVisible = visibility;
            copyValueItem.IsVisible = visibility;
            viewItem.IsVisible = CanView(node);
            var hasChildren = node is TreeNode t && t.HasChildren;
            copySubtreeItem.IsVisible = hasChildren;
            sortChildrenItem.IsVisible = hasChildren;
            preprocessItem.IsVisible = node is IPreprocessable p && preprocessedFileManager.CanPreprocess(p);
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

        private IEnumerable BuildFindResults(object resultsObject, bool moreAvailable)
        {
            if (resultsObject == null)
            {
                return null;
            }

            var results = resultsObject as IEnumerable<(string, IEnumerable<(int, string)>)>;

            var root = new Folder();

            if (results != null)
            {
                foreach (var file in results)
                {
                    var folder = new SourceFile
                    {
                        Name = Path.GetFileName(file.Item1),
                        SourceFilePath = file.Item1,
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
                root.Children.Add(new Message { Text = "No results found." });
            }

            return root.Children;
        }

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
            }

            foreach (var subFolder in root.Children.OfType<Folder>())
            {
                CompressTree(subFolder);
            }

            filesTree.Items = root.Children;
            filesTree.GotFocus += (s, a) => ActiveTreeView = filesTree;
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

        private SourceFile AddSourceFile(Folder folder, string filePath)
        {
            var parts = filePath.Split('\\', '/');
            return AddSourceFile(folder, filePath, parts, 0);
        }

        private SourceFile AddSourceFile(Folder folder, string filePath, string[] parts, int index)
        {
            if (index == parts.Length - 1)
            {
                var file = new SourceFile()
                {
                    SourceFilePath = filePath,
                    Name = parts[index]
                };
                folder.AddChild(file);
                return file;
            }
            else
            {
                var subfolder = folder.GetOrCreateNodeWithName<Folder>(parts[index]);
                subfolder.IsExpanded = true;
                return AddSourceFile(subfolder, filePath, parts, index + 1);
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
            Dispatcher.UIThread.InvokeAsync(() => { isProcessingBreadcrumbClick = false; }, DispatcherPriority.Background);
        }

        private void TreeView_SelectedItemChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != TreeView.SelectedItemProperty) return;

            var item = treeView.SelectedItem;
            if (item != null)
            {
                UpdateBreadcrumb(item);
            }
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var proxy = searchLogControl.ResultsList.SelectedItem as ProxyNode;
            if (proxy != null)
            {
                var item = proxy.Original as BaseNode;
                if (item != null)
                {
                    SelectItem(item);
                }
            }
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

            breadCrumb.Items = chain;
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
            //scrollViewer = treeView.Template.FindName("_tv_scrollviewer_", treeView) as ScrollViewer;

            if (!Build.Succeeded)
            {
                var firstError = Build.FindFirstInSubtreeIncludingSelf<Error>();
                if (firstError != null)
                {
                    SelectItem(firstError);
                    treeView.Focus();
                }

                searchLogControl.SearchText = "$error";
            }
        }

        public void SelectItem(BaseNode item)
        {
            var parentChain = item.GetParentChainExcludingThis();
            
            foreach (var node in parentChain)
            {
                if (node is TreeNode treeNode)
                    treeNode.IsExpanded = true;
            }

            SelectTree();
            treeView.SelectedItem = item;
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Delete)
            {
                Delete();
                args.Handled = true;
            }
            else if (args.Key == Key.C && args.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                CopySubtree();
                args.Handled = true;
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
            if (treeView.SelectedItem is TreeNode node)
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
            foreach (var item in tree.Items.OfType<BaseNode>())
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(item);
                sb.AppendLine(text);
            }

            CopyToClipboard(sb.ToString());
        }

        private static void CopyToClipboard(string text)
        {
            Application.Current.Clipboard.SetTextAsync(text);
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

        private void OnPreviewMouseRightButtonDown(object sender, PointerEventArgs args)
        {
            var treeViewItem = sender as TreeViewItem;
            if (treeViewItem != null)
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
                || (node is IHasSourceFile ihsf && ihsf.SourceFilePath != null && sourceFileResolver.HasFile(ihsf.SourceFilePath))
                || (node is NameValueNode nvn && nvn.IsValueShortened)
                || (node is TextNode tn && tn.IsTextShortened);
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

                        ProjectEvaluation evaluation = null;
                        if (hasSourceFile is TreeNode node)
                        {
                            // TODO: https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/392
                            evaluation = node.GetNearestParentOrSelf<ProjectEvaluation>();
                        }

                        return DisplayFile(hasSourceFile.SourceFilePath, line, evaluation: evaluation);
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

        public bool DisplayFile(string sourceFilePath, int lineNumber = 0, int column = 0, ProjectEvaluation evaluation = null)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return false;
            }

            Action preprocess = preprocessedFileManager.GetPreprocessAction(sourceFilePath, PreprocessedFileManager.GetEvaluationKey(evaluation));
            documentWell.DisplaySource(sourceFilePath, text.Text, lineNumber, column, preprocess, navigationHelper);
            return true;
        }

        public bool DisplayText(string text, string caption = null)
        {
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

            var xml = text.XmlRoot;
            IXmlElement root = xml.Root;
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

            //if (PresentationSource.FromDependencyObject(treeViewItem) == null)
            //{
            //    // the item might have disconnected by the time we run this
            //    return;
            //}

            Point? topLeftInTreeViewCoordinates = treeViewItem.TranslatePoint(new Point(), treeView);
            var treeViewItemTop = topLeftInTreeViewCoordinates?.Y ?? 0;
            if (treeViewItemTop < 0
                || treeViewItemTop + treeViewItem.Bounds.Height > scrollViewer.Viewport.Height
                || treeViewItem.Bounds.Height > scrollViewer.Viewport.Height)
            {
                // if the item is not visible or too "tall", don't do anything; let them scroll it into view
                return;
            }

            // if the item is already fully within the viewport vertically, disallow horizontal scrolling
            e.Handled = true;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            SelectedTreeViewItem = e.Source as TreeViewItem;
        }
    }
}
