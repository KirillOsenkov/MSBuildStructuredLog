using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Language.Xml;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class VSCodeInstallation
    {
        public string Name { get; }
        public string ExePath { get; }
        public string UriScheme { get; }
        public string CliName { get; }

        public VSCodeInstallation(string name, string exePath, string uriScheme, string cliName)
        {
            Name = name;
            ExePath = exePath;
            UriScheme = uriScheme;
            CliName = cliName;
        }
    }

    public partial class BuildControl : UserControl
    {
        public Build Build { get; set; }
        public TreeViewItem SelectedTreeViewItem { get; private set; }
        public string LogFilePath => Build?.LogFilePath;

        private readonly List<string> attachedBinlogs = new List<string>();
        public int AttachedBinlogCount => attachedBinlogs.Count;

        public void AttachBinlog(string path)
        {
            if (!string.IsNullOrEmpty(path) && !attachedBinlogs.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                attachedBinlogs.Add(path);
            }
        }

        private SourceFileResolver sourceFileResolver;
        private ArchiveFileResolver archiveFile => sourceFileResolver.ArchiveFile;
        private PreprocessedFileManager preprocessedFileManager;
        private NavigationHelper navigationHelper;
        private MenuItem searchMenuGroup;

        private MenuItem copyItem;
        private MenuItem copySubtreeItem;
        private MenuItem copyVisibleSubtreeItem;
        private MenuItem viewSubtreeTextItem;
        private MenuItem searchInSubtreeItem;
        private MenuItem searchInNodeByNameItem;
        private MenuItem searchThisNode;
        private MenuItem viewPropertyItem;
        private MenuItem excludeSubtreeFromSearchItem;
        private MenuItem excludeNodeByNameFromSearch;
        private MenuItem searchInclusiveWithinThisTimespan;
        private MenuItem searchExclusiveWithinThisTimespan;
        private MenuItem copyChildrenItem;
        private MenuItem sortChildrenByNameItem;
        private MenuItem sortChildrenByDurationItem;
        private MenuItem filterChildrenItem;
        private MenuItem copyNameItem;
        private MenuItem copyValueItem;
        private MenuItem viewSourceItem;
        private MenuItem viewFullTextItem;
        private MenuItem openFileItem;
        private MenuItem copyFilePathItem;
        private MenuItem showFileInExplorerItem;
        private MenuItem preprocessItem;
        private MenuItem searchNuGetItem;
        private MenuItem hideItem;
        private MenuItem showTimeItem;
        private MenuItem favoriteItem;
        private MenuItem unfavoriteItem;
        private MenuItem favoriteSharedItem;
        private MenuItem unfavoriteSharedItem;
        private Separator separator1;
        private Separator separator2;
        private ContextMenu sharedTreeContextMenu;
        private ContextMenu filesTreeContextMenu;
        private TreeView treeView;
        internal SearchAndResultsControl searchLogControl;
        private SearchAndResultsControl findInFilesControl;
        private SearchAndResultsControl propertiesAndItemsControl;
        private TabItem filesTab;
        private TabItem propertiesAndItemsTab;
        private TabItem findInFilesTab;
        private SearchAndResultsControl filesTree;
        private SearchAndResultsControl favoritesTree;
        private TabControl centralTabControl;
        private ListBox breadCrumb;
        private TabControl leftPaneTabControl;
        private TabItem searchLogTab;
        private DocumentWell documentWell;
        private Border projectContextBorder;
        private TextBlock projectContextLabel;
        private ContentControl propertiesAndItemsContext;
        private Grid findControl;
        private TextBlock findLabel;
        private TextBox findTextBox;

        public TreeView ActiveTreeView;

        private PropertiesAndItemsSearch propertiesAndItemsSearch;
        private SecretsSearch secretsSearch;

        static BuildControl()
        {
            PreprocessedFileManager.GetPreprocessedFilePath = SettingsService.GetPreprocessedFilePath;
            PreprocessedFileManager.WriteContentToTempFileAndGetPath = SettingsService.WriteContentToTempFileAndGetPath;

            // ScrollContentPresenter handles RequestBringIntoView before any handler on the
            // TreeView can (it's closer to the item in the bubble route) and scrolls
            // horizontally too, shifting the whole tree left when a wide item is expanded
            // or selected. A class handler on TreeViewItem runs at the event source, before
            // the presenter, so we can take over and scroll vertically only.
            RequestBringIntoViewEvent.AddClassHandler<TreeViewItem>(TreeViewItem_RequestBringIntoView);
        }

        // required by the Avalonia XAML loader (AVLN3001); not used directly
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
                if (Build.SearchIndex is { } index)
                {
                    index.MaxResults = maxResults;
                    index.MarkResultsInTree = SettingsService.MarkResultsInTree;
                    var indexResults = index.FindNodes(searchText, cancellationToken);
                    return indexResults;
                }

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
            searchLogControl.WatermarkDisplayed += SearchLogControl_WatermarkDisplayed;

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

            SetProjectContext(null, force: true);

            Build = build;

            secretsSearch = (SecretsSearch)build.SearchExtensions.FirstOrDefault(se => se is SecretsSearch);

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

            // Search Log | Properties and Items | Find in Files
            sharedTreeContextMenu = new ContextMenu();
            sharedTreeContextMenu.Opened += SharedTreeContextMenu_Opened;
            favoriteSharedItem = new MenuItem { Header = "Add to Favorites" };
            unfavoriteSharedItem = new MenuItem { Header = "Remove from Favorites" };
            var sharedCopyItem = new MenuItem() { Header = "Copy" };
            var sharedCopyAllItem = new MenuItem() { Header = "Copy All" };
            var sharedCopySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            var sharedCopyVisibleSubtreeItem = new MenuItem() { Header = "Copy visible subtree" };
            favoriteSharedItem.Click += (s, a) => AddToFavorites();
            unfavoriteSharedItem.Click += (s, a) => RemoveFromFavorites();
            sharedCopyItem.Click += (s, a) => Copy();
            sharedCopyAllItem.Click += (s, a) => CopyAll();
            sharedCopySubtreeItem.Click += (s, a) => CopySubtree();
            sharedCopyVisibleSubtreeItem.Click += (s, a) => CopySubtree(visibleOnly: true);
            sharedTreeContextMenu.AddItem(favoriteSharedItem);
            sharedTreeContextMenu.AddItem(unfavoriteSharedItem);
            sharedTreeContextMenu.AddItem(sharedCopyItem);
            sharedTreeContextMenu.AddItem(sharedCopyAllItem);
            sharedTreeContextMenu.AddItem(sharedCopySubtreeItem);
            sharedTreeContextMenu.AddItem(sharedCopyVisibleSubtreeItem);

            // Files
            filesTreeContextMenu = new ContextMenu();
            var filesCopyItem = new MenuItem { Header = "Copy" };
            var filesCopyAllItem = new MenuItem { Header = "Copy All" };
            var filesCopyPathsItem = new MenuItem { Header = "Copy file paths" };
            var filesCopySubtreeItem = new MenuItem { Header = "Copy subtree" };
            var filesCopyVisibleSubtreeItem = new MenuItem { Header = "Copy visible subtree" };
            filesCopyItem.Click += (s, a) => Copy();
            filesCopyAllItem.Click += (s, a) => CopyAll();
            filesCopyPathsItem.Click += (s, a) => CopyPaths();
            filesCopySubtreeItem.Click += (s, a) => CopySubtree();
            filesCopyVisibleSubtreeItem.Click += (s, a) => CopySubtree(visibleOnly: true);
            filesTreeContextMenu.AddItem(filesCopyItem);
            filesTreeContextMenu.AddItem(filesCopyAllItem);
            filesTreeContextMenu.AddItem(filesCopyPathsItem);
            filesTreeContextMenu.AddItem(filesCopySubtreeItem);
            filesTreeContextMenu.AddItem(filesCopyVisibleSubtreeItem);

            // Build Log
            var contextMenu = new ContextMenu();
            contextMenu.Opened += ContextMenu_Opened;
            searchMenuGroup = new MenuItem() { Header = "Search" };
            copyItem = new MenuItem() { Header = "Copy" };
            copySubtreeItem = new MenuItem() { Header = "Copy subtree" };
            copyVisibleSubtreeItem = new MenuItem() { Header = "Copy visible subtree" };
            sortChildrenByNameItem = new MenuItem() { Header = "Sort children by name" };
            sortChildrenByDurationItem = new MenuItem() { Header = "Sort children by duration" };
            filterChildrenItem = new MenuItem() { Header = "Filter children (Ctrl+F)" };
            copyNameItem = new MenuItem() { Header = "Copy name" };
            copyValueItem = new MenuItem() { Header = "Copy value" };
            viewSourceItem = new MenuItem() { Header = "View source" };
            viewFullTextItem = new MenuItem() { Header = "View full text" };
            searchNuGetItem = new MenuItem() { Header = "Search project.assets.json" };
            showFileInExplorerItem = new MenuItem() { Header = "Show in Explorer" };
            preprocessItem = new MenuItem() { Header = "Preprocess" };
            hideItem = new MenuItem() { Header = "Hide" };
            copyChildrenItem = new MenuItem() { Header = "Copy children" };
            viewSubtreeTextItem = new MenuItem() { Header = "View subtree text" };
            showTimeItem = new MenuItem() { Header = "Show time and duration" };
            openFileItem = new MenuItem() { Header = "Open File" };
            copyFilePathItem = new MenuItem() { Header = "Copy file path" };
            viewPropertyItem = new MenuItem() { Header = "View property" };
            searchInSubtreeItem = new MenuItem() { Header = "Search in subtree" };
            searchInNodeByNameItem = new MenuItem() { Header = "Search in this node." };
            searchThisNode = new MenuItem() { Header = "Search this node" };
            excludeSubtreeFromSearchItem = new MenuItem() { Header = "Exclude subtree from search" };
            excludeNodeByNameFromSearch = new MenuItem() { Header = "Exclude node from search" };
            searchInclusiveWithinThisTimespan = new MenuItem() { Header = "Search overlapping this duration" };
            searchExclusiveWithinThisTimespan = new MenuItem() { Header = "Search within this duration" };
            favoriteItem = new MenuItem() { Header = "Add to Favorites" };
            unfavoriteItem = new MenuItem() { Header = "Remove from Favorites" };
            copyChildrenItem.Click += (s, a) => CopyChildren();
            viewSubtreeTextItem.Click += (s, a) => ViewSubtreeText();
            showTimeItem.Click += (s, a) => ShowTimeAndDuration();
            openFileItem.Click += (s, a) => OpenFile();
            copyFilePathItem.Click += (s, a) => CopyFilePath();
            viewPropertyItem.Click += (s, a) => ViewProperty();
            searchInSubtreeItem.Click += (s, a) => SearchInSubtree();
            searchInNodeByNameItem.Click += (s, a) => SearchInNodeByName();
            searchThisNode.Click += (s, a) => SearchThisNode();
            excludeSubtreeFromSearchItem.Click += (s, a) => ExcludeSubtreeFromSearch();
            excludeNodeByNameFromSearch.Click += (s, a) => ExcludeNodeByNameFromSearch();
            searchInclusiveWithinThisTimespan.Click += (s, a) => SearchInclusiveWithinThisTimespan();
            searchExclusiveWithinThisTimespan.Click += (s, a) => SearchExclusiveWithinThisTimespan();
            favoriteItem.Click += (s, a) => AddToFavorites();
            unfavoriteItem.Click += (s, a) => RemoveFromFavorites();
            copyItem.Click += (s, a) => Copy();
            copySubtreeItem.Click += (s, a) => CopySubtree(treeView);
            copyVisibleSubtreeItem.Click += (s, a) => CopySubtree(treeView, visibleOnly: true);
            sortChildrenByNameItem.Click += (s, a) => SortChildrenByName();
            sortChildrenByDurationItem.Click += (s, a) => SortChildrenByDuration();
            filterChildrenItem.Click += (s, a) => FilterChildren();
            copyNameItem.Click += (s, a) => CopyName();
            copyValueItem.Click += (s, a) => CopyValue();
            viewSourceItem.Click += (s, a) => Invoke(treeView.SelectedItem as BaseNode);
            viewFullTextItem.Click += (s, a) => ViewFullText(treeView.SelectedItem as BaseNode);
            searchNuGetItem.Click += (s, a) => SearchNuGet(treeView.SelectedItem as IProjectOrEvaluation);
            showFileInExplorerItem.Click += (s, a) => ShowFileInExplorer();
            preprocessItem.Click += (s, a) => Preprocess(treeView.SelectedItem as IPreprocessable);
            hideItem.Click += (s, a) => Delete();
            separator1 = new Separator();
            separator2 = new Separator();

            contextMenu.AddItem(favoriteItem);
            contextMenu.AddItem(unfavoriteItem);
            contextMenu.AddItem(viewSourceItem);
            contextMenu.AddItem(viewFullTextItem);
            contextMenu.AddItem(viewPropertyItem);
            contextMenu.AddItem(openFileItem);
            contextMenu.AddItem(preprocessItem);

            contextMenu.AddItem(searchMenuGroup);
            searchMenuGroup.AddItem(searchNuGetItem);
            searchMenuGroup.AddItem(searchInSubtreeItem);
            searchMenuGroup.AddItem(searchInNodeByNameItem);
            searchMenuGroup.AddItem(searchThisNode);
            searchMenuGroup.AddItem(excludeSubtreeFromSearchItem);
            searchMenuGroup.AddItem(excludeNodeByNameFromSearch);
            searchMenuGroup.AddItem(searchInclusiveWithinThisTimespan);
            searchMenuGroup.AddItem(searchExclusiveWithinThisTimespan);

            contextMenu.AddItem(new Separator());

            contextMenu.AddItem(copyItem);
            contextMenu.AddItem(copySubtreeItem);
            contextMenu.AddItem(copyVisibleSubtreeItem);
            contextMenu.AddItem(copyFilePathItem);
            contextMenu.AddItem(copyChildrenItem);
            contextMenu.AddItem(copyNameItem);
            contextMenu.AddItem(copyValueItem);

            contextMenu.AddItem(new Separator());
            contextMenu.AddItem(showFileInExplorerItem);

            contextMenu.AddItem(separator2);

            contextMenu.AddItem(viewSubtreeTextItem);
            contextMenu.AddItem(showTimeItem);

            contextMenu.AddItem(separator1);

            contextMenu.AddItem(sortChildrenByNameItem);
            contextMenu.AddItem(sortChildrenByDurationItem);
            contextMenu.AddItem(filterChildrenItem);
            contextMenu.AddItem(hideItem);

            Style GetTreeViewItemStyle()
            {
                var treeViewItemStyle = new Style(s => s.OfType<TreeViewItem>());
                treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty,
                    CompiledBinding.Create<IExpandable, bool>(i => i.IsExpanded, mode: BindingMode.TwoWay)));
                treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty,
                    CompiledBinding.Create<Item, bool>(i => i.IsSelected, mode: BindingMode.TwoWay)));
                treeViewItemStyle.Setters.Add(new Setter(IsVisibleProperty,
                    CompiledBinding.Create<IExpandable, bool>(i => i.IsVisible, mode: BindingMode.TwoWay)));
                return treeViewItemStyle;
            }

            treeView.ContextMenu = contextMenu;
            treeView.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(treeView);
            treeView.KeyDown += TreeView_KeyDown;
            treeView.PropertyChanged += TreeView_SelectedItemChanged;
            treeView.GotFocus += TreeView_GotFocus;

            ActiveTreeView = treeView;

            findTextBox.KeyDown += FindTextBox_KeyDown;
            findTextBox.TextChanged += FindTextBox_TextChanged;

            searchLogControl.searchTextBox.KeyUp += SearchTextBox_KeyUp;

            searchLogControl.ResultsList.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(searchLogControl.ResultsList);
            searchLogControl.ResultsList.SelectionChanged += ResultsList_SelectionChanged;
            searchLogControl.ResultsList.GotFocus += TreeView_GotFocus;
            searchLogControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            propertiesAndItemsControl.ResultsList.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(propertiesAndItemsControl.ResultsList);
            propertiesAndItemsControl.ResultsList.SelectionChanged += ResultsList_SelectionChanged;
            propertiesAndItemsControl.ResultsList.GotFocus += TreeView_GotFocus;
            propertiesAndItemsControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            findInFilesControl.GotFocus += FindInFilesControl_GotFocus;
            findInFilesControl.ResultsList.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(findInFilesControl.ResultsList);
            findInFilesControl.ResultsList.GotFocus += TreeView_GotFocus;
            findInFilesControl.ResultsList.ContextMenu = sharedTreeContextMenu;

            if (archiveFile != null)
            {

                findInFilesControl.ExecuteSearch = FindInFiles;
                findInFilesControl.ResultsTreeBuilder = BuildFindResults;

                filesTab.IsVisible = true;
                findInFilesTab.IsVisible = true;
                PopulateFilesTab();
                filesTree.ResultsList.Styles.Add(GetTreeViewItemStyle());
                RegisterTreeViewHandlers(filesTree.ResultsList);
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

            favoritesTree.TopPanel.IsVisible = false;
            favoritesTree.ResultsList.Styles.Add(GetTreeViewItemStyle());
            RegisterTreeViewHandlers(favoritesTree.ResultsList);
            favoritesTree.ResultsList.SelectionChanged += ResultsList_SelectionChanged;
            favoritesTree.ResultsList.ContextMenu = sharedTreeContextMenu;
            favoritesTree.DisplayItems(new[] { new Note { Text = "Right-click any node and Favorite it to add it here" } });
            favoritesTree.ResultsList.GotFocus += TreeView_GotFocus;

            breadCrumb.SelectionChanged += BreadCrumb_SelectionChanged;

            TemplateApplied += BuildControl_Loaded;

            preprocessedFileManager = new PreprocessedFileManager(this.Build, sourceFileResolver);
            preprocessedFileManager.DisplayFile += filePath => DisplayFile(filePath);
            Build.TextProvider = evaluation => preprocessedFileManager.GetPreprocessedText(evaluation);

            navigationHelper = new NavigationHelper(Build, sourceFileResolver);
            navigationHelper.OpenFileRequested += filePath => DisplayFile(filePath);
        }

        /// <summary>
        /// Returns the workspace directory for VS Code.
        /// Uses the binlog file's directory, or null if the path doesn't exist locally.
        /// </summary>
        public string GetWorkspacePath()
        {
            if (Build == null) return null;

            var binlogDir = Path.GetDirectoryName(Build.LogFilePath);
            if (!string.IsNullOrEmpty(binlogDir) && Directory.Exists(binlogDir))
            {
                return FindRepoRoot(binlogDir) ?? binlogDir;
            }

            return null;
        }

        /// <summary>
        /// Walks up from a directory to find a repository root (contains .git or .sln).
        /// </summary>
        private static string FindRepoRoot(string startDir)
        {
            var dir = startDir;
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                    return dir;
                if (Directory.GetFiles(dir, "*.sln", SearchOption.TopDirectoryOnly).Length > 0)
                    return dir;
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
            return null;
        }

        /// <summary>
        /// Launches VS Code with the workspace folder and binlog URI handler.
        /// Auto-installs the binlog-analyzer extension if not already installed.
        /// </summary>
        public void OpenInVSCode(VSCodeInstallation installation = null)
        {
            var binlogPath = Build?.LogFilePath;
            if (string.IsNullOrEmpty(binlogPath))
            {
                return;
            }

            if (installation == null)
            {
                var installations = FindVSCodeInstallations();
                installation = installations.FirstOrDefault();
            }

            if (installation == null)
            {
                return;
            }

            try
            {
                TPLTask.Run(() => EnsureExtensionInstalled(installation));

                var folder = GetWorkspacePath();

                // Build the URI using the correct scheme for this variant
                var uri = $"{installation.UriScheme}://ms-dotnettools.msbuild-binlog-analyzer/open?path=" + Uri.EscapeDataString(binlogPath);
                foreach (var attached in attachedBinlogs)
                {
                    uri += "&path=" + Uri.EscapeDataString(attached);
                }

                // Launch VS Code with folder, then send URI after a short delay.
                // Combining --new-window + --open-url in one call can cause VS Code to ignore the folder.
                var codeExe = installation.ExePath;
                var folderArg = !string.IsNullOrEmpty(folder) ? $"\"{folder}\"" : "";
                Process.Start(new ProcessStartInfo { FileName = codeExe, Arguments = $"--new-window {folderArg}".Trim(), UseShellExecute = true });

                var capturedUri = uri;
                TPLTask.Run(async () =>
                {
                    try
                    {
                        await TPLTask.Delay(1000);
                        Process.Start(new ProcessStartInfo { FileName = codeExe, Arguments = $"--open-url \"{capturedUri}\"", UseShellExecute = true });
                    }
                    catch { }
                });
            }
            catch
            {
            }
        }

        private static readonly string ExtensionId = "ms-dotnettools.msbuild-binlog-analyzer";

        private static void EnsureExtensionInstalled(VSCodeInstallation installation)
        {
            try
            {
                var codeDir = Path.GetDirectoryName(installation.ExePath);
                var codeCli = Path.Combine(codeDir, "bin", installation.CliName + ".cmd");
                if (!File.Exists(codeCli))
                {
                    codeCli = Path.Combine(codeDir, "bin", installation.CliName);
                }

                // Check if extension is already installed
                var checkPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{codeCli}\" --list-extensions",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };

                using var checkProc = Process.Start(checkPsi);
                var output = checkProc?.StandardOutput.ReadToEnd() ?? "";
                checkProc?.WaitForExit(10000);

                if (output.IndexOf(ExtensionId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                // Install from VS Code Marketplace
                var installPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{codeCli}\" --install-extension {ExtensionId} --force",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var installProc = Process.Start(installPsi);
                installProc?.WaitForExit(60000);
            }
            catch
            {
                // Non-fatal — user can install manually
            }
        }

        public static List<VSCodeInstallation> FindVSCodeInstallations()
        {
            var installations = new List<VSCodeInstallation>();

            var variants = new[]
            {
                new { Name = "VS Code", FolderName = "Microsoft VS Code", ExeName = "Code.exe", UriScheme = "vscode", CliName = "code" },
                new { Name = "VS Code Insiders", FolderName = "Microsoft VS Code Insiders", ExeName = "Code - Insiders.exe", UriScheme = "vscode-insiders", CliName = "code-insiders" },
            };

            foreach (var variant in variants)
            {
                string[] candidates =
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", variant.FolderName, variant.ExeName),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), variant.FolderName, variant.ExeName),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), variant.FolderName, variant.ExeName),
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        installations.Add(new VSCodeInstallation(variant.Name, candidate, variant.UriScheme, variant.CliName));
                        break;
                    }
                }
            }

            // Fallback: resolve from code.cmd / code-insiders.cmd in PATH
            try
            {
                var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
                foreach (var variant in variants)
                {
                    if (installations.Any(i => i.CliName == variant.CliName))
                        continue;

                    var cmdName = variant.CliName + ".cmd";
                    foreach (var dir in pathDirs)
                    {
                        var codeCmdPath = Path.Combine(dir, cmdName);
                        if (File.Exists(codeCmdPath))
                        {
                            // code.cmd is in <install>/bin/, Code.exe is in <install>/
                            var codeExe = Path.Combine(Path.GetDirectoryName(dir) ?? dir, variant.ExeName);
                            if (File.Exists(codeExe))
                            {
                                installations.Add(new VSCodeInstallation(variant.Name, codeExe, variant.UriScheme, variant.CliName));
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            return installations;
        }

        public void Dispose()
        {
            documentWell.Dispose();

            searchLogControl.ResultsList.SelectionChanged -= ResultsList_SelectionChanged;
            searchLogControl.ResultsList.GotFocus -= TreeView_GotFocus;
            searchLogControl.searchTextBox.KeyUp -= SearchTextBox_KeyUp;
            searchLogControl.WatermarkDisplayed -= SearchLogControl_WatermarkDisplayed;
            searchLogControl.ExecuteSearch = null;
            searchLogControl.ResultsTreeBuilder = null;
            searchLogControl.WatermarkContent = null;
            searchLogControl.Dispose();

            propertiesAndItemsControl.ResultsList.SelectionChanged -= ResultsList_SelectionChanged;
            propertiesAndItemsControl.ResultsList.GotFocus -= TreeView_GotFocus;
            propertiesAndItemsControl.WatermarkDisplayed -= UpdatePropertiesAndItemsWatermark;
            propertiesAndItemsControl.ExecuteSearch = null;
            propertiesAndItemsControl.ResultsTreeBuilder = null;
            propertiesAndItemsControl.WatermarkContent = null;
            propertiesAndItemsControl.Dispose();
            propertiesAndItemsContext.Content = null;
            propertiesAndItemsSearch = null;

            findInFilesControl.GotFocus -= FindInFilesControl_GotFocus;
            findInFilesControl.ResultsList.GotFocus -= TreeView_GotFocus;
            findInFilesControl.ExecuteSearch = null;
            findInFilesControl.ResultsTreeBuilder = null;
            findInFilesControl.Dispose();

            favoritesTree.ResultsList.SelectionChanged -= ResultsList_SelectionChanged;
            favoritesTree.ResultsList.GotFocus -= TreeView_GotFocus;
            favoritesTree.Dispose();

            filesTree.ResultsList.GotFocus -= TreeView_GotFocus;
            filesTree.ContextMenu = null;
            filesTree.DisplayItems(null);
            filesTree.Dispose();

            breadCrumb.SelectionChanged -= BreadCrumb_SelectionChanged;
            breadCrumb.ItemsSource = null;

            findTextBox.KeyDown -= FindTextBox_KeyDown;
            findTextBox.TextChanged -= FindTextBox_TextChanged;

            treeView.PropertyChanged -= TreeView_SelectedItemChanged;
            treeView.KeyDown -= TreeView_KeyDown;
            treeView.GotFocus -= TreeView_GotFocus;
            treeView.ItemsSource = null;
            treeView.ContextMenu = null;

            TemplateApplied -= BuildControl_Loaded;

            // member variables
            searchMenuGroup = null;
            copyItem = null;
            copySubtreeItem = null;
            copyVisibleSubtreeItem = null;
            viewSubtreeTextItem = null;
            searchInSubtreeItem = null;
            searchInNodeByNameItem = null;
            searchThisNode = null;
            viewPropertyItem = null;
            excludeSubtreeFromSearchItem = null;
            excludeNodeByNameFromSearch = null;
            searchInclusiveWithinThisTimespan = null;
            searchExclusiveWithinThisTimespan = null;
            copyChildrenItem = null;
            sortChildrenByNameItem = null;
            sortChildrenByDurationItem = null;
            filterChildrenItem = null;
            copyNameItem = null;
            copyValueItem = null;
            viewSourceItem = null;
            viewFullTextItem = null;
            openFileItem = null;
            copyFilePathItem = null;
            showFileInExplorerItem = null;
            preprocessItem = null;
            searchNuGetItem = null;
            hideItem = null;
            showTimeItem = null;
            favoriteItem = null;
            unfavoriteItem = null;
            favoriteSharedItem = null;
            unfavoriteSharedItem = null;
            separator1 = null;
            separator2 = null;

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
            Build = null;
        }

        private void SearchLogControl_WatermarkDisplayed()
        {
            Search.ClearSearchResults(Build, SettingsService.MarkResultsInTree);
            UpdateWatermark();
        }

        private void TreeView_GotFocus(object sender, FocusChangedEventArgs e)
        {
            if (sender is TreeView focusedTree)
            {
                ActiveTreeView = focusedTree;
            }
        }

        private void FindInFilesControl_GotFocus(object sender, FocusChangedEventArgs e)
        {
            ActiveTreeView = findInFilesControl.ResultsList;
        }

        private void RegisterTreeViewHandlers(TreeView treeView)
        {
            // select the node under the cursor on right-click, so the context menu
            // acts on the node being clicked rather than the previously selected one.
            // Setting TreeViewItem.IsSelected alone only changes the visual state -
            // TreeView.SelectedItem (what the menu handlers read) must be set explicitly.
            treeView.AddHandler(PointerPressedEvent, (o, e) =>
            {
                if (e.GetCurrentPoint(treeView).Properties.IsRightButtonPressed)
                {
                    // right-click doesn't move focus, so mark this tree active for the
                    // shared context menu commands as well
                    ActiveTreeView = treeView;

                    var item = (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true);
                    if (item?.DataContext != null)
                    {
                        treeView.SelectedItem = item.DataContext;
                    }
                }
            }, RoutingStrategies.Tunnel);

            treeView.DoubleTapped += (o, e) =>
            {
                if (treeView.SelectedItem is BaseNode node)
                {
                    e.Handled = Invoke(node) || ViewFullText(node);
                }
            };

            treeView.KeyDown += (o, e) =>
            {
                if (e.Handled)
                {
                    return;
                }

                if (e.KeyModifiers == KeyModifiers.None)
                {
                    if (e.Key == Key.Delete)
                    {
                        Delete();
                        e.Handled = true;
                    }

                    if (e.Key == Key.Space || e.Key == Key.Return)
                    {
                        if (treeView.SelectedItem is BaseNode node)
                        {
                            e.Handled = Invoke(node) || ViewFullText(node);
                        }
                    }

                    if (e.Key == Key.Escape)
                    {
                        if (IsFindVisible)
                        {
                            IsFindVisible = false;
                            e.Handled = true;
                        }
                        else if (documentWell.IsVisible)
                        {
                            documentWell.Hide();
                            e.Handled = true;
                        }
                    }
                }

                if (e.KeyModifiers == KeyModifiers.Control)
                {
                    if (e.Key == Key.C)
                    {
                        Copy();
                        e.Handled = true;
                    }

                    if (e.Key == Key.F)
                    {
                        if (IsFindVisible)
                        {
                            IsFindVisible = false;
                            e.Handled = true;
                        }
                        else if (TryGetTreeNodeForFind() != null)
                        {
                            IsFindVisible = true;
                            e.Handled = true;
                        }
                    }
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out treeView, nameof(treeView));
            this.RegisterControl(out findControl, nameof(findControl));
            this.RegisterControl(out findLabel, nameof(findLabel));
            this.RegisterControl(out findTextBox, nameof(findTextBox));
            this.RegisterControl(out filesTab, nameof(filesTab));
            this.RegisterControl(out findInFilesTab, nameof(findInFilesTab));
            this.RegisterControl(out propertiesAndItemsTab, nameof(propertiesAndItemsTab));
            this.RegisterControl(out projectContextBorder, nameof(projectContextBorder));
            this.RegisterControl(out projectContextLabel, nameof(projectContextLabel));
            this.RegisterControl(out propertiesAndItemsContext, nameof(propertiesAndItemsContext));
            this.RegisterControl(out filesTree, nameof(filesTree));
            this.RegisterControl(out favoritesTree, nameof(favoritesTree));
            this.RegisterControl(out centralTabControl, nameof(centralTabControl));
            this.RegisterControl(out breadCrumb, nameof(breadCrumb));
            this.RegisterControl(out leftPaneTabControl, nameof(leftPaneTabControl));
            this.RegisterControl(out searchLogTab, nameof(searchLogTab));
            this.RegisterControl(out propertiesAndItemsControl, nameof(propertiesAndItemsControl));

            this.RegisterControl(out SplitterPanel tabs, nameof(tabs));
            documentWell = tabs.SecondChild as DocumentWell;
            searchLogControl = searchLogTab.Content as SearchAndResultsControl;
            findInFilesControl = findInFilesTab.Content as SearchAndResultsControl;
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
            "$secret",
            "$secret not(username)"
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
            "$noimport",
            "$secret"
        };

        private static Inline MakeLink(string query, SearchAndResultsControl searchControl, string before = " • ", string after = "\r\n")
        {
            var linkText = new TextBlock
            {
                Text = query.Trim(),
                Foreground = Brushes.RoyalBlue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            linkText.PointerPressed += (s, e) =>
            {
                searchControl.SearchText = query;
                e.Handled = true;
            };

            var span = new global::Avalonia.Controls.Documents.Span();
            if (before != null)
            {
                span.Inlines.Add(new Run(before));
            }

            span.Inlines.Add(new InlineUIContainer(linkText) { BaselineAlignment = BaselineAlignment.TextBottom });

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

        private static Inline MakeActionLink(string text, Action action)
        {
            var linkText = new TextBlock
            {
                Text = text,
                Foreground = Brushes.RoyalBlue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            linkText.PointerPressed += (s, e) =>
            {
                action();
                e.Handled = true;
            };

            return new InlineUIContainer(linkText) { BaselineAlignment = BaselineAlignment.TextBottom };
        }

        private void UpdateWatermark()
        {
            string watermarkText0 = @"Type in the search box to search. Press Ctrl+F to focus the search box. Results (up to 1000) will display here.
";

            string watermarkText1 = @"
Search for multiple words separated by space (space means AND). Enclose multiple words in double-quotes """" to search for the exact phrase. A single word in quotes means exact match (turns off substring search).

Use syntax like '$property Prop' to narrow results down by item kind. Supported kinds: ";

            string watermarkText2 = @" • Use under(FILTER) clause to only include results where any of the nodes in the parent chain matches the FILTER.
 • Use notunder(...) as the opposite of under(...).
 • Use project(...) to filter by parent project.
 • Use not(...) to exclude subqueries.

Examples:
 • $csc under($project Core)
 • Copying file project(ProjectA.csproj)

Use '$target skipped=false' to exclude skipped targets (use true to only include skipped).

Append [[$time]], [[$start]] and/or [[$end]] to show times and/or durations and sort the results by start time or duration descending (for tasks, targets and projects).

Use start<""2023-11-23 14:30:54.579"", start>, end<, or end> to filter events that start or end before or after a given timestamp. Timestamp needs to be in quotes.

Use '$copy path' where path is a file or directory to find file copy operations involving the file or directory. `$copy substring` will search for copied files containing the substring.

Use '$nuget project(MyProject.csproj) Package.Name' to search for NuGet packages (by name or version), dependencies (direct and transitive) and files coming from NuGet packages.

Use '$projectreference project(MyProject.csproj) RefProj' to search for projects referenced by MyProject.csproj directly or indirectly. For a single matching project all referencing projects will be shown as well.

Examples:
";

            var watermark = new TextBlock { TextWrapping = TextWrapping.Wrap };
            watermark.Inlines.Add(new Run(watermarkText0));

            var recentSearches = SettingsService.GetRecentSearchStrings();
            if (recentSearches.Any())
            {
                watermark.Inlines.Add(new Run(@"
Recent ("));
                watermark.Inlines.Add(MakeActionLink("clear", () =>
                {
                    SettingsService.RemoveAllRecentSearchText();
                    UpdateWatermark();
                }));
                watermark.Inlines.Add(new Run(@"):
"));

                foreach (var recentSearch in recentSearches.Where(s => !searchExamples.Contains(s) && !nodeKinds.Contains(s)))
                {
                    watermark.Inlines.Add(MakeLink(recentSearch, searchLogControl));
                }
            }

            watermark.Inlines.Add(new Run(watermarkText1));

            bool isFirst = true;
            foreach (var nodeKind in nodeKinds)
            {
                if (!isFirst)
                {
                    watermark.Inlines.Add(new Run(", "));
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

            var watermark = new TextBlock { TextWrapping = TextWrapping.Wrap };
            AddTextWithHyperlinks(watermarkText1, watermark.Inlines, propertiesAndItemsControl);

            watermark.Inlines.Add(new LineBreak());
            watermark.Inlines.Add(new LineBreak());

            var recentSearches = SettingsService.GetRecentSearchStrings("PropertiesAndItems");
            if (recentSearches.Any())
            {
                watermark.Inlines.Add(new Run(@"
Recent ("));
                watermark.Inlines.Add(MakeActionLink("clear", () =>
                {
                    SettingsService.RemoveAllRecentSearchText("PropertiesAndItems");
                    UpdatePropertiesAndItemsWatermark();
                }));
                watermark.Inlines.Add(new Run(@"):
"));

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
                    result.Add(new Run(chunk));
                }
            }
        }

        private void SearchNuGet(IProjectOrEvaluation node)
        {
            if (node == null)
            {
                return;
            }

            string projectName = Path.GetFileName(node.ProjectFile);
            SelectSearchTab($"$nuget project({projectName})");
        }

        private void Preprocess(IPreprocessable project) => preprocessedFileManager.ShowPreprocessed(project);


        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var node = treeView.SelectedItem as BaseNode;
            var nameValueVisibility = node is NameValueNode;
            copyNameItem.IsVisible = nameValueVisibility;
            copyValueItem.IsVisible = nameValueVisibility;
            viewSourceItem.IsVisible = CanView(node);
            viewFullTextItem.IsVisible = HasFullText(node);
            searchNuGetItem.IsVisible = node is IProjectOrEvaluation;
            openFileItem.IsVisible = CanOpenFile(node);
            copyFilePathItem.IsVisible = node is Import || (node is IHasSourceFile file && !string.IsNullOrEmpty(file.SourceFilePath));
            showFileInExplorerItem.IsVisible = CanShowInExplorer();
            var hasChildren = node is TreeNode t && t.HasChildren;
            copySubtreeItem.IsVisible = hasChildren;
            copyVisibleSubtreeItem.IsVisible = hasChildren;
            viewSubtreeTextItem.IsVisible = hasChildren;
            copyChildrenItem.IsVisible = hasChildren;
            sortChildrenByNameItem.IsVisible = hasChildren;
            sortChildrenByDurationItem.IsVisible = hasChildren;
            filterChildrenItem.IsVisible = hasChildren;
            preprocessItem.IsVisible = node is IPreprocessable p && preprocessedFileManager.CanPreprocess(p);
            hideItem.IsVisible = node is TreeNode;
            separator2.IsVisible = true;

            if (node is SearchableItem searchItem)
            {
                searchThisNode.IsVisible = true;
                searchThisNode.Header = $"Search {searchItem.SearchText}";
            }
            else
            {
                searchThisNode.IsVisible = false;
            }

            if (node is Property ||
                (node?.Parent is { } parent &&
                (parent.Title == Strings.PropertyReassignmentFolder ||
                parent?.Parent?.Title == Strings.PropertyReassignmentFolder ||
                parent.Title == Strings.PropertyAssignmentFolder ||
                parent?.Parent?.Title == Strings.PropertyAssignmentFolder)))
            {
                viewPropertyItem.IsVisible = true;
            }
            else
            {
                viewPropertyItem.IsVisible = false;
            }

            bool isFavorite = IsFavorite(node);
            favoriteItem.IsVisible = !isFavorite;
            unfavoriteItem.IsVisible = isFavorite;

            if (node is TimedNode timedNode)
            {
                showTimeItem.IsVisible = true;
                separator1.IsVisible = true;
                searchInSubtreeItem.IsVisible = hasChildren;
                excludeSubtreeFromSearchItem.IsVisible = hasChildren;
                excludeNodeByNameFromSearch.IsVisible = hasChildren;
                searchInclusiveWithinThisTimespan.IsVisible = true;
                searchExclusiveWithinThisTimespan.IsVisible = true;
                searchInNodeByNameItem.IsVisible = hasChildren;

                if (excludeNodeByNameFromSearch.IsVisible)
                {
                    excludeNodeByNameFromSearch.Header = $"Exclude '{timedNode.Name}' from search";
                }

                if (searchInNodeByNameItem.IsVisible)
                {
                    searchInNodeByNameItem.Header = $"Search in '{timedNode.Name}'";
                }
            }
            else
            {
                separator1.IsVisible = false;
                showTimeItem.IsVisible = false;
                searchInSubtreeItem.IsVisible = false;
                excludeSubtreeFromSearchItem.IsVisible = false;
                excludeNodeByNameFromSearch.IsVisible = false;
                searchInclusiveWithinThisTimespan.IsVisible = false;
                searchExclusiveWithinThisTimespan.IsVisible = false;
                searchInNodeByNameItem.IsVisible = false;
                if (!hasChildren)
                {
                    separator2.IsVisible = false;
                }
            }

            searchMenuGroup.IsVisible = searchMenuGroup.Items.OfType<MenuItem>().Any(p => p.IsVisible);
        }

        private void SharedTreeContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var node = ActiveTreeView?.SelectedItem as BaseNode;
            bool isFavorite = node != null && IsFavorite(node);
            favoriteSharedItem.IsVisible = !isFavorite;
            unfavoriteSharedItem.IsVisible = isFavorite;
        }

        private object FindInFiles(string searchText, int maxResults, CancellationToken cancellationToken)
        {
            var results = new List<(string, IEnumerable<(int, string)>)>();

            NodeQueryMatcher nodeQueryMatcher = new NodeQueryMatcher(searchText);
            bool isSecretsSearch = !string.IsNullOrEmpty(searchText) && searchText.StartsWith("$secret");

            List<System.Threading.Tasks.Task<(string filePath, SourceText sourceText, IReadOnlyList<int> resultLines)>> tasks = new();

            foreach (var file in archiveFile.Files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                if (isSecretsSearch)
                {
                    if (secretsSearch != null)
                    {
                        var searchResults = secretsSearch.SearchSecrets(file.Value.Text, nodeQueryMatcher.NotMatchers, maxResults);
                        if (searchResults.Count > 0)
                        {
                            results.Add((file.Key, searchResults.Select(sr => (sr.Line - 1, sr.Secret))));
                        }
                    }
                }
                else
                {
                    var task = TPLTask.Run(() =>
                    {
                        var resultsInFile = file.Value.Find(searchText);
                        return (file.Key, file.Value, resultsInFile);
                    });
                    tasks.Add(task);
                }
            }

            foreach (var task in tasks)
            {
                var result = task.Result;
                if (result.resultLines.Count > 0)
                {
                    results.Add((result.filePath, result.resultLines.Select(lineNumber => (lineNumber, result.sourceText.GetLineText(lineNumber)))));
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
                        IsExpanded = true
                    };
                    root.AddChild(folder);
                    foreach (var line in file.Item2)
                    {
                        var sourceFileLine = new SourceFileLine()
                        {
                            SourceFilePath = file.Item1,
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
            filesTree.ResultsList.GotFocus += TreeView_GotFocus;
            filesTree.ResultsList.ContextMenu = filesTreeContextMenu;
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
                var file = new SourceFile()
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

            await Dispatcher.UIThread.InvokeAsync(() =>
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

        public string TryFindDanglingTarget(Project project, string targetName)
        {
            if (project.GetEvaluation(Build) is ProjectEvaluation evaluation)
            {
                var graph = Build.TargetGraphManager.GetTargetGraph(evaluation);

                var roots = project.EntryTargets != null && project.EntryTargets.Any()
                    ? project.EntryTargets
                    : graph.RootTargets;

                var path = graph.FindPathFromEntryTargets(targetName, roots);
                var result = string.Join(" → ", path.Reverse().Select(t => $"[{t.relationship}] {t.targetName}"));
                return result;
            }

            return null;
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
                SelectedTreeViewItem = treeView.TreeContainerFromItem(item) as TreeViewItem;
                UpdateBreadcrumb(item);
                UpdateProjectContext(item);
                UpdateFindContent();
            }
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var proxy = (sender as TreeView)?.SelectedItem as ProxyNode;
            if (proxy != null)
            {
                var item = proxy.Original as BaseNode;
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

        public void SetProjectContext(object contents, bool force = false)
        {
            if (projectContext == contents && !force)
            {
                return;
            }

            var visibility = contents != null;

            contents ??= "A project or evaluation must be selected.";

            projectContext = contents;
            propertiesAndItemsContext.Content = contents;
            projectContextBorder.IsVisible = true;
            projectContextLabel.IsVisible = visibility;
            propertiesAndItemsControl.TopPanel.IsVisible = visibility;
            if (contents != null &&
                !string.IsNullOrEmpty(propertiesAndItemsControl.SearchText) &&
                leftPaneTabControl.SelectedItem == propertiesAndItemsTab)
            {
                propertiesAndItemsControl.RetriggerSearch();
            }
        }

        public IProjectOrEvaluation GetProjectContext()
        {
            return projectContext as IProjectOrEvaluation;
        }

        private object currentBreadcrumb;

        public void UpdateBreadcrumb(object item)
        {
            if (currentBreadcrumb == item)
            {
                return;
            }

            currentBreadcrumb = item;

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
            // TemplateApplied can re-fire (e.g. on a theme change); initialize only once
            TemplateApplied -= BuildControl_Loaded;

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

        public string SearchText => searchLogControl?.SearchText;

        public void SelectItem(BaseNode item)
        {
            var parentChain = item.GetParentChainExcludingThis();

            foreach (var node in parentChain)
            {
                if (node is TreeNode treeNode)
                    treeNode.IsExpanded = true;
            }

            SelectTree();

            // The item's TreeViewItem must exist and be arranged before SelectedItem is
            // set. If it's created later (ancestors were just expanded for the first time),
            // TreeView.ContainerForItemPreparedOverride sees the container's style-bound
            // IsSelected=false and removes the item from SelectedItems, silently dropping
            // the selection. And if it exists but is stale (ancestors were re-expanded),
            // BringIntoView scrolls using pre-expansion geometry, landing the row behind
            // the horizontal scrollbar.
            treeView.UpdateLayout();

            treeView.SelectedItem = item;
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }

            if (args.Key == Key.F && args.KeyModifiers.HasFlag(KeyModifiers.Control))
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
            else if (args.Key >= Key.A && args.Key <= Key.Z && args.KeyModifiers == KeyModifiers.None)
            {
                SelectItemByKey((char)('A' + args.Key - Key.A));
                args.Handled = true;
            }
        }

        public bool IsFindVisible
        {
            get => findControl.IsVisible;
            set
            {
                findControl.IsVisible = value;
                if (value)
                {
                    findTextBox.Focus();
                    UpdateFindContent();
                }
                else
                {
                    ActiveTreeView?.Focus();
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
                findLabel.Text = $"Filter children of: {TextUtilities.ShortenValue(GetText(treeNode), trimPrompt: "", maxChars: 100)}";
                if (nodeFilters.TryGetValue(treeNode, out var filter))
                {
                    findTextBox.Text = filter;
                }
                else
                {
                    findTextBox.Text = "";
                }
            }
            else
            {
                IsFindVisible = false;
            }
        }

        private void SearchTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                if (string.IsNullOrEmpty(searchLogControl.SearchText))
                {
                    ActiveTreeView?.Focus();
                }
                else
                {
                    searchLogControl.SearchText = "";
                }

                e.Handled = true;
            }
        }

        private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key == Key.Escape)
                {
                    if (!string.IsNullOrEmpty(findTextBox.Text))
                    {
                        findTextBox.Text = "";
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
            else if (e.KeyModifiers == KeyModifiers.Control)
            {
                if (e.Key == Key.F)
                {
                    IsFindVisible = false;
                    FocusSearch();
                    e.Handled = true;
                }
            }
        }

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = findTextBox.Text?.Trim() ?? "";

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
            else if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                nodeFilters.Remove(node);
            }
            else
            {
                nodeFilters[node] = text;
            }

            foreach (var child in node.Children)
            {
                bool visible = string.IsNullOrEmpty(text);
                if (!visible)
                {
                    var nodeText = GetText(child);
                    visible = nodeText != null && nodeText.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (child is IExpandable expandable)
                {
                    expandable.IsVisible = visible;
                }
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

        public void SelectSearchTab(string newText = null)
        {
            if (newText != null)
            {
                searchLogControl.SearchText = newText;
            }

            leftPaneTabControl.SelectedItem = searchLogTab;
        }

        public void SelectPropertiesAndItemsTab(string newText = null)
        {
            if (newText != null)
            {
                propertiesAndItemsControl.SearchText = newText;
            }

            leftPaneTabControl.SelectedItem = propertiesAndItemsTab;
        }

        public void SelectFindInFilesTab(string newText = null)
        {
            if (!findInFilesTab.IsVisible)
            {
                return;
            }

            if (newText != null)
            {
                findInFilesControl.SearchText = newText;
            }

            leftPaneTabControl.SelectedItem = findInFilesTab;
            findInFilesControl.searchTextBox.Focus();
            findInFilesControl.searchTextBox.SelectAll();
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
            if (ActiveTreeView?.SelectedItem is BaseNode node)
            {
                var text = node.GetFullText();
                CopyToClipboard(text);
            }
        }

        public void CopySubtree(TreeView tree = null, bool visibleOnly = false)
        {
            tree = tree ?? ActiveTreeView;
            if (tree == null)
            {
                return;
            }

            if (tree.SelectedItem is BaseNode treeNode)
            {
                var text = Microsoft.Build.Logging.StructuredLogger.StringWriter.GetString(treeNode, visibleOnly);
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

        public void ShowFileInExplorer()
        {
            string path = FileExplorerHelper.GetFilePathFromNode(treeView.SelectedItem as BaseNode);

            if (path != null)
            {
                FileExplorerHelper.ShowInExplorer(path);
            }
        }

        private bool CanShowInExplorer()
        {
            return FileExplorerHelper.GetFilePathFromNode(treeView.SelectedItem as BaseNode) is not null;
        }

        public void ViewProperty()
        {
            var selectedItem = treeView.SelectedItem;
            if (selectedItem is Property property)
            {
                SearchForProperty(property.Name);
            }
            else if (selectedItem is PropertyAssignmentMessage assignment)
            {
                SearchForProperty(assignment.Parent.Title);
            }
            else if (selectedItem is Folder reassignmentFolder
                && reassignmentFolder.Parent is TimedNode parent
                && (parent.Name == Strings.PropertyReassignmentFolder || parent.Name == Strings.PropertyAssignmentFolder))
            {
                SearchForProperty(reassignmentFolder.Name);
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
                if (treeNode is Project)
                {
                    searchLogControl.SearchText += $" project({treeNode.Name})";
                }
                else
                {
                    searchLogControl.SearchText += $" under(${treeNode.TypeName} {treeNode.Name})";
                }

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
            if (treeView.SelectedItem is NamedNode treeNode)
            {
                searchLogControl.SearchText += $" notunder(${treeNode.TypeName} {treeNode.Name})";
                SelectSearchTab();
            }
        }

        public void SearchInclusiveWithinThisTimespan()
        {
            if (treeView.SelectedItem is TimedNode timedNode)
            {
                DateTime starTime = timedNode.StartTime;
                DateTime endTime = timedNode.EndTime;
                searchLogControl.SearchText += $" start<\"{TextUtilities.Display(endTime, displayDate: true, fullPrecision: true)}\" end>\"{TextUtilities.Display(starTime, displayDate: true, fullPrecision: true)}\" ";
                SelectSearchTab();
            }
        }

        public void SearchExclusiveWithinThisTimespan()
        {
            if (treeView.SelectedItem is TimedNode timedNode)
            {
                DateTime starTime = timedNode.StartTime;
                DateTime endTime = timedNode.EndTime;
                searchLogControl.SearchText += $" start>\"{TextUtilities.Display(starTime, displayDate: true, fullPrecision: true)}\" end<\"{TextUtilities.Display(endTime, displayDate: true, fullPrecision: true)}\"";
                SelectSearchTab();
            }
        }

        public void CopyChildren()
        {
            if (treeView.SelectedItem is TreeNode node && node.HasChildren)
            {
                var children = node.Children.Select(c => c.GetFullText());
                var text = string.Join(Environment.NewLine, children);
                CopyToClipboard(text);
            }
        }

        public void SortChildrenByName()
        {
            var selectedItem = treeView.SelectedItem;
            if (selectedItem is TreeNode treeNode)
            {
                treeNode.SortChildren();
            }
        }

        public void SortChildrenByDuration()
        {
            var selectedItem = treeView.SelectedItem;
            if (selectedItem is TreeNode treeNode)
            {
                treeNode.SortChildren(TreeNode.CompareByDuration);
            }
        }

        public void FilterChildren()
        {
            IsFindVisible = !IsFindVisible;
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

        private void CopyToClipboard(string text)
        {
            try
            {
                text = text.Replace("\0", "");
                TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text);
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

                        if (diagnostic.IsTextShortened)
                        {
                            return DisplayText(diagnostic.Text, diagnostic.GetType().Name);
                        }

                        break;

                    case Target target when target.Parent is Folder:
                        return SearchForTarget(target.Name);
                    case Target target:
                        return DisplayTarget(
                            target.SourceFilePath,
                            target.Name,
                            evaluation: target.Project.GetEvaluation());
                    case Task task:
                        return DisplayTask(task);
                    case AddItem addItem:
                        return DisplayAddRemoveItem(addItem.Parent, addItem.LineNumber ?? 0);
                    case RemoveItem removeItem:
                        return DisplayAddRemoveItem(removeItem.Parent, removeItem.LineNumber ?? 0);
                    case Item embedItem when embedItem.Parent is AddItem parentAddItem && parentAddItem.Name == "EmbedInBinlog":
                        return DisplayEmbeddedFile(embedItem);
                    case Item pathItem when
                        pathItem.Parent == null &&
                        searchLogControl.SearchText.Contains("$copy") &&
                        searchLogControl.ResultsList.ItemsSource is IEnumerable<BaseNode> copyResults &&
                        copyResults.Contains(pathItem):
                        return SearchForFullPath(pathItem.Text);
                    case Project projectRef when
                        searchLogControl.SearchText.Contains("$projectreference"):
                        return SearchForProject(Path.GetFileName(projectRef.ProjectFile));
                    case ProxyNode proxy when
                        searchLogControl.SearchText.Contains("$projectreference") &&
                        proxy.Original is Project originalProject:
                        return SearchForProject(Path.GetFileName(originalProject.ProjectFile));
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

                        // if a preprocessed text is selected and we can find the requested file in the preprocessed text,
                        // navigate to that instead of opening in a separate file
                        if (hasSourceFile is Import import)
                        {
                            string sourceFilePath = import.ImportedProjectFilePath;

                            if (documentWell.SelectedTextViewer is { } currentTextViewer &&
                                currentTextViewer.EditorExtension is { } extension &&
                                extension.PreprocessContext != null)
                            {
                                int offset = extension.PreprocessContext.FindFileOffset(sourceFilePath);
                                if (offset > 0)
                                {
                                    currentTextViewer.TextEditor.CaretOffset = offset;
                                    currentTextViewer.TextEditor.ScrollToLine(currentTextViewer.TextEditor.TextArea.Caret.Line);
                                    return true;
                                }
                            }
                        }

                        return DisplayFile(hasSourceFile.SourceFilePath, line, evaluation: evaluation);
                    case SourceFileLine sourceFileLine when sourceFileLine.Parent is SourceFile sourceFile && sourceFile.SourceFilePath != null:
                        return DisplayFile(sourceFile.SourceFilePath, sourceFileLine.LineNumber);
                    case Property property:
                        return SearchForProperty(property.Name);
                    case Folder reassignmentFolder when reassignmentFolder.Parent is TimedNode reassignmentParent &&
                        (reassignmentParent.Name == Strings.PropertyReassignmentFolder || reassignmentParent.Name == Strings.PropertyAssignmentFolder):
                        return SearchForProperty(reassignmentFolder.Name);
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
            else if (matcher.Terms.Count == 0 && matcher.ProjectMatchers.Count > 0)
            {
                text = $"{text} {filePath}";
                searchLogControl.SearchText = text;
                return true;
            }

            return false;
        }

        private bool SearchForProject(string name)
        {
            var text = $"$projectreference project({name})";
            searchLogControl.SearchText = text;
            return true;
        }

        private bool SearchForTarget(string name)
        {
            string text = searchLogControl.SearchText;
            var matcher = new NodeQueryMatcher(text);
            string project = "";
            if (matcher.ProjectMatchers.Count == 1)
            {
                project = $" project({matcher.ProjectMatchers[0].Query})";
            }

            text = $"$target \"{name}\"{project}";
            searchLogControl.SearchText = text;
            return true;
        }

        private bool SearchForProperty(string name)
        {
            SelectPropertiesAndItemsTab($"$property \"{name}\"");
            return true;
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

            var context = preprocessedFileManager.TryGetContext(sourceFilePath);
            evaluation ??= context?.Evaluation;

            var editorExtension = new EditorExtension();
            editorExtension.PreprocessContext = context;
            editorExtension.Evaluation = evaluation;

            editorExtension.ImportSelected += import =>
            {
                if (import != null)
                {
                    UpdateBreadcrumb(import);
                }
                else if (evaluation != null)
                {
                    UpdateBreadcrumb(evaluation);
                }
            };
            editorExtension.GoToProperty += propertyName =>
            {
                SearchForProperty(propertyName);
            };

            documentWell.DisplaySource(
                preprocessableFilePath,
                text.Text,
                lineNumber,
                column,
                preprocess,
                navigationHelper,
                editorExtension);
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

        public bool DisplayTarget(string sourceFilePath, string targetName, string taskName = null, ProjectEvaluation evaluation = null)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return false;
            }

            SourceTextXml.TryGetXml(text, out var root);
            int startPosition = 0;
            int line = 0;

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

            return DisplayFile(sourceFilePath, line + 1, evaluation: evaluation);
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

        private static void TreeViewItem_RequestBringIntoView(TreeViewItem treeViewItem, RequestBringIntoViewEventArgs e)
        {
            if (e.Handled || e.TargetObject is not Control item)
            {
                return;
            }

            var viewer = treeViewItem.FindAncestorOfType<ScrollViewer>();
            if (viewer == null)
            {
                return;
            }

            // requires ScrollViewer.AllowAutoHide=False (set in Styles.xaml): with auto-hide,
            // the Fluent template overlays the scrollbars on the viewport, so Viewport.Height
            // includes the horizontal scrollbar and rows aligned to it end up hidden behind it
            double viewportHeight = viewer.Viewport.Height;

            // the row header is the interesting part; the whole item includes children
            double itemHeight = item is TreeViewItem
                ? (item as TreeViewItem).FindDescendantOfType<Border>()?.Bounds.Height ?? item.Bounds.Height
                : item.Bounds.Height;

            Point? topLeftInViewerCoordinates = item.TranslatePoint(new Point(), viewer);
            if (topLeftInViewerCoordinates == null)
            {
                return;
            }

            var itemTop = topLeftInViewerCoordinates.Value.Y;

            // take over from ScrollContentPresenter entirely; scroll vertically
            // when needed but never touch the horizontal offset
            e.Handled = true;

            double newY = viewer.Offset.Y;
            if (itemTop < 0)
            {
                // scrolled off the top: align the row with the top of the viewport
                newY += itemTop;
            }
            else if (itemTop + itemHeight > viewportHeight)
            {
                // below the viewport: align with the bottom edge, but for rows taller
                // than the viewport fall back to aligning the top
                newY += Math.Min(itemTop, itemTop + itemHeight - viewportHeight);
            }

            if (newY != viewer.Offset.Y)
            {
                viewer.Offset = new Vector(viewer.Offset.X, newY);
            }
        }

        public void DisplayStats()
        {
            if (!File.Exists(LogFilePath))
            {
                return;
            }

            var statsRoot = Build.FindChild<Folder>(static f => f.Name.StartsWith(Strings.Statistics));
            if (statsRoot != null)
            {
                return;
            }

            var recordStats = BinlogStats.Calculate(this.LogFilePath);
            var records = recordStats.CategorizedRecords;

            statsRoot = DisplayRecordStats(records, Build);

            var treeStats = Build.Statistics;
            DisplayTreeStats(statsRoot, treeStats, recordStats);

            statsRoot.AddChild(new Property { Name = "BinlogFileFormatVersion", Value = Build.FileFormatVersion.ToString() });
            statsRoot.AddChild(new Property { Name = "FileSize", Value = recordStats.FileSize.ToString("N0") });
            statsRoot.AddChild(new Property { Name = "UncompressedStreamSize", Value = recordStats.UncompressedStreamSize.ToString("N0") });
            statsRoot.AddChild(new Property { Name = "RecordCount", Value = recordStats.RecordCount.ToString("N0") });

            // This is needed as a workaround for a weird WPF bug; replacing the Children collection
            // acts as a Reset. See https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/487
            Build.MakeChildrenObservable();
        }

        private void DisplayTreeStats(Folder statsRoot, BuildStatistics treeStats, BinlogStats recordStats)
        {
            var buildMessageNode = statsRoot.FindChild<Folder>(static n => n.Name.StartsWith("BuildMessage", StringComparison.Ordinal));
            var taskInputsNode = buildMessageNode.FindChild<Folder>(static n => n.Name.StartsWith("Task Input", StringComparison.Ordinal));
            var taskOutputsNode = buildMessageNode.FindChild<Folder>(static n => n.Name.StartsWith("Task Output", StringComparison.Ordinal));

            AddTopTasks(treeStats.TaskParameterMessagesByTask, taskInputsNode);
            AddTopTasks(treeStats.OutputItemMessagesByTask, taskOutputsNode);

            if (recordStats.StringTotalSize > 0)
            {
                var strings = new Item
                {
                    Text = BinlogStats.GetString("Strings", recordStats.StringTotalSize, recordStats.StringCount, recordStats.StringLargest)
                };
                var allStringText = recordStats.AllStrings.Count > 0
                    ? string.Join("\n", recordStats.AllStrings)
                    : "Strings are not tracked for large binlogs";
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
