using System.IO;
using System.Collections;
using Microsoft.Build.Logging.StructuredLogger;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using System;
using Microsoft.AspNetCore.Components;
using Microsoft.Msagl.Core.Layout;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Msagl.Core.ProjectionSolver;

namespace StructuredLogViewerWASM
{
    public class TreeFormatting
    {
        [Inject]
        static IJSRuntime JSRuntime { get; set; }

        /// <summary>
        /// Creates the render fragment for each tree node (selects icon/color, margins, and text to show)
        /// </summary>
        public static RenderFragment<RadzenTreeItem> TreeDesign = (RadzenTreeItem context) => builder =>
        {
            var node = (BaseNode)context.Value;
            string margin = TreeFormatting.MarginSelector(context);
            string icon = IconSelector(node);

            builder.OpenElement(3, "img");
            builder.AddAttribute(4, "src", icon);
            builder.AddAttribute(5, "aria-label", icon);
            builder.AddAttribute(2, "Style", "margin-left: " + margin);
            builder.CloseElement();
            builder.AddContent(3, context.Text);
        };

        /// <summary>
        /// Used only for the FileTree panel, is necessary to allow for scrolling/highlighting
        /// if a user selects a node from the search bar and wants to find it in the FileTree
        /// </summary>
        public static RenderFragment<RadzenTreeItem> TreeDesignFiles = (RadzenTreeItem context) => builder =>
        {
            var node = (BaseNode)context.Value;
            string margin = TreeFormatting.MarginSelector(context);
            string icon = IconSelector(node);

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", ((BaseNode)context.Value).Id.ToString());
            if (((BaseNode)context.Value).IsSelected)
            {
                builder.AddAttribute(2, "Style", "background-color: #E7E6E9");
                ((BaseNode)context.Value).IsSelected = false;
            }
            builder.OpenElement(3, "img");
            builder.AddAttribute(4, "src", icon);
            builder.AddAttribute(5, "aria-label", icon);
            builder.AddAttribute(2, "Style", "margin-left: " + margin);
            builder.CloseElement();
            builder.AddContent(3, context.Text);
            builder.CloseElement();
        };

        /// <summary>
        /// Used only for File Tree, calls the TreeDesignFiles method instead of TreeDesign
        /// </summary>
        /// <param name="args"> Holds the nodes to add the expansion preferences to</param>
        public static void OnExpandFiles(TreeExpandEventArgs args)
        {
            OnExpand(args);
            args.Children.Template = TreeDesignFiles;
        }

        /// <summary>
        /// Determines how the children of a node should be designed
        /// </summary>
        /// <param name="args"> Holds the children nodes </param>
        public static void OnExpand(TreeExpandEventArgs args)
        {
            args.Children.Data = ((TreeNode)args.Value).Children;
            args.Children.Text = TextSelector;
            args.Children.HasChildren = (node) =>
            {
                if (node is TreeNode)
                    return ((TreeNode)node).HasChildren;
                else
                    return false;
            };
            args.Children.Template = TreeDesign;
            args.Children.Expanded = ((node) => { return ((BaseNode)node).IsExpanded; });
            ((BaseNode)(args.Value)).IsExpanded = true;
        }

        /// <summary>
        /// Determines which icon should be used to distinguish visually what the tree node is
        /// </summary>
        /// <param name="node"> node to find icon for</param>
        /// <returns> name of icon to use </returns>
        public static string IconSelector(Object node)
        {
            string img = "/icons/";
            if (node is ProxyNode)
            {
                node = ((ProxyNode)node).Original;
            }

            if (node is Folder)
            {
                img += "FolderClosed.png";
            } else if (node is Solution)
            {
                img += "SolutionV15.png";
            } else if (node is Project)
            {
                string extension = ((Project)node).ProjectFileExtension;
                if (extension.Equals(".csproj"))
                {
                    img += "CSProjectNode.png";
                } else if (extension.Equals(".fsproj"))
                {
                    img += "FSProjectNode.png";
                } else if (extension.Equals(".vbproj"))
                {
                    img += "VBProjectNode.png";
                } else
                {
                    img += "VisualStudio.png";
                }
            }
            else if (node is Target)
            {
                img += "TargetFile.ico";
            }
            else if (node is Microsoft.Build.Logging.StructuredLogger.Task)
            {
                img += "task.ico";
            }
            else if (node is Parameter)
            {
                img += "Parameter.png";
            }
            else if (node is Microsoft.Build.Logging.StructuredLogger.Property)
            {
                img += "Property.png";
            }
            else if (node is Item)
            {
                img += "Item.png";
            }
            else if (node is Metadata)
            {
                img += "Metadata.png";
            }
            else if (node is Message)
            {
                img += "Message.png";
            }
            else if (node is Import)
            {
                img += "Import.png";
            }
            else if (node is NoImport)
            {
                img += "NoImport.png";
            }
            else if (node is Error)
            {
                img += "StatusError.png";
            }
            else if (node is Warning)
            {
                img += "StatusWarning.png";
            } else
            {
                img += "FilledRectangle.png";
            }
            return img;
        }

        /// <summary>
        /// Finds the proper text to be used for a tree node
        /// </summary>
        /// <param name="node"> node to find name for </param>
        /// <returns>name of the node to display on tree </returns>
        public static string TextSelector(Object node)
        {
            if (node is ProxyNode)
            {
                node = ((ProxyNode)node).Original;
            }
            string text = node switch
            {
                Project p => p.Name,
                Target t => "Target " + t.Name,
                Folder f => f.Name,
                ProjectEvaluation pe => pe.Name,
                Microsoft.Build.Logging.StructuredLogger.Task ta => "Task " + ta.Title + " duration " + ta.DurationText,
                AddItem a => "Add Item " + a.Name,
                RemoveItem r => "Remove Item " + r.Name,
                Item i => i.NameAndEquals + " " + i.ShortenedText,
                Metadata m => m.NameAndEquals + " " + m.ShortenedValue,
                EvaluationProfileEntry epe => epe.Title + " " + epe.DurationText + " " + epe.ShortenedElementDescription,
                Property p => p.NameAndEquals + " " + p.ShortenedValue,
                Parameter p => p.Name,
                Message m => m.ShortenedText,
                Import i => "Import " + i.Name + " " + i.Location,
                NoImport ni => "NoImport " + ni.Name + " " + ni.Location + " " + ni.Text,
                Error e => e.ToString(),
                Warning w => w.ToString(),
                Note n => n.Text,
                SourceFile sf => sf.Name,
                SourceFileLine sfl => sfl.LineNumber + " " + sfl.LineText,
                ProxyNode pn => pn.TypeName,
                TimedNode t => t.Name,
                _ => node.ToString()
            };
            return text;
        }

        /// <summary>
        /// Finds the amount of margin there should be for a node, to align the node names properly
        /// </summary>
        /// <param name="context"> Node to detemine margins for</param>
        /// <returns>string value of the margin amount </returns>
        public static string MarginSelector(RadzenTreeItem context)
        {
            return context.HasChildren ? "0px" : "24px";
        }

        /// <summary>
        /// Finds the proper color for the node icon
        /// </summary>
        /// <param name="node"> node the icon will be placed with </param>
        /// <returns> selected color for the icon </returns>
        public static string ColorSelector(BaseNode node)
        {
            if (node is ProxyNode)
            {
                node = ((ProxyNode)node).Original;
            }
            string color = "Black";
            if (node is Folder)
            {
                color = "GoldenRod";
            }
            else if (node is Target)
            {
                color = "MediumPurple";
            }
            else if (node is Microsoft.Build.Logging.StructuredLogger.Task)
            {
                color = "DodgerBlue";
            }
            else if (node is Parameter)
            {
                color = "DodgerBlue";
            }
            else if (node is Microsoft.Build.Logging.StructuredLogger.Property)
            {
                color = "DodgerBlue";
            }
            else if (node is Item)
            {
                color = "MediumAquamarine";
            }
            else if (node is Metadata)
            {
                color = "MediumAquamarine";
            }
            else if (node is Message)
            {
                color = "LightGray";
            }
            else if (node is Import)
            {
                color = "Sienna";
            }
            else if (node is NoImport)
            {
                color = "Red";
            }
            else if (node is Error)
            {
                color = "Red";
            }
            else if (node is Warning)
            {
                color = "Gold";
            }
            return color;
        }
    }
}
