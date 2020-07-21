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

namespace StructuredLogViewerWASM
{
    public class TreeFormatting
    {
        [Inject]
        static IJSRuntime JSRuntime { get; set; }

        public static RenderFragment<RadzenTreeItem> TreeDesign = (RadzenTreeItem context) => builder =>
        {
            builder.OpenComponent<RadzenIcon>(0);
            builder.AddAttribute(1, "Icon", "crop_16_9");
            var node = (BaseNode)context.Value;
            string color = TreeFormatting.ColorSelector(node);
            string margin = TreeFormatting.MarginSelector(context);
            builder.AddAttribute(2, "Style", "color: " + color + ";margin-left: " + margin);
            builder.CloseComponent();
            builder.AddContent(3, context.Text);
        };

        //#E7E6E9 is the color I want for when something is being selected
        //nothing if it's not selected
        public static RenderFragment<RadzenTreeItem> TreeDesignFiles = (RadzenTreeItem context) => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", ((BaseNode)context.Value).Id.ToString());
            if (((BaseNode)context.Value).IsSelected)
                builder.AddAttribute(2, "Style", "background-color: #E7E6E9");
            builder.OpenComponent<RadzenIcon>(0);
            builder.AddAttribute(3, "Icon", "crop_16_9");
            var node = (BaseNode)context.Value;
            string color = TreeFormatting.ColorSelector(node);
            string margin = TreeFormatting.MarginSelector(context);
            builder.AddAttribute(4, "Style", "color: " + color + ";margin-left: " + margin);
            builder.CloseComponent();
            builder.AddContent(5, context.Text);
            builder.CloseElement();
            
            ((BaseNode)context.Value).IsSelected = false;
        };

        private static ValueTask<bool> ScrollToElementId(string elementId)
        {
            object[] my_Array = new object[1] {elementId};
            return JSRuntime.InvokeAsync<bool>("scrollToElementId", my_Array);
        }

        public static void OnExpandFiles(TreeExpandEventArgs args)
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
            args.Children.Template = TreeDesignFiles;
            // args.Children.Selected = ((node) => { return ((BaseNode)node).IsSelected; });
            args.Children.Expanded = ((node) => { return ((BaseNode)node).IsExpanded; });
            ((BaseNode)(args.Value)).IsExpanded = true;
        }

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
           // args.Children.Selected = ((node) => { return ((BaseNode)node).IsSelected; });
            args.Children.Expanded = ((node) => { return ((BaseNode)node).IsExpanded; });
            ((BaseNode)(args.Value)).IsExpanded = true;
        }
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
        public static string MarginSelector(RadzenTreeItem context)
        {
            return context.HasChildren ? "0px" : "24px";
        }
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
