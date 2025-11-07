using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger;

public class PropertyUsage
{
    public string Name;
    public int Position;
    public int RelativePosition;
    public bool IsWrite;
    public bool PropertyOfInterest;
}

public class SourceFileLineWithHighlights : SourceFileLine
{
    private List<PropertyUsage> usages = new();

    public void AddUsage(PropertyUsage usage)
    {
        usages.Add(usage);
    }

    public IEnumerable<string> ReadProperties =>
        usages.Where(u => !u.IsWrite).Select(u => u.Name).Distinct(StringComparer.OrdinalIgnoreCase);

    public string WrittenProperty => usages.FirstOrDefault(u => u.IsWrite)?.Name;

    private bool isBold;
    public bool IsBold
    {
        get => isBold;
        set => SetField(ref isBold, value);
    }

    public override string ToolTip => CustomToolTip;

    private string customToolTip;
    public string CustomToolTip
    {
        get => customToolTip;
        set => SetField(ref customToolTip, value);
    }

    private bool isReadBeforeWrite;
    public bool IsReadBeforeWrite
    {
        get => isReadBeforeWrite;
        set => SetField(ref isReadBeforeWrite, value);
    }

    private List<object> highlights;
    public List<object> Highlights
    {
        get
        {
            if (highlights == null)
            {
                highlights = new List<object>();
                Populate();
            }

            return highlights;
        }
    }

    private void Populate()
    {
        usages.Sort((l, r) => l.Position - r.Position);

        int start = 0;
        for (int i = 0; i < usages.Count; i++)
        {
            var usage = usages[i];
            if (!usage.PropertyOfInterest)
            {
                continue;
            }

            if (start < usage.Position)
            {
                highlights.Add(LineText.Substring(start, usage.Position - start));
            }

            highlights.Add(
                new HighlightedText
                {
                    Text = LineText.Substring(usage.Position, usage.Name.Length),
                    Style = usage.IsWrite ? "write" : "read"
                });
            start = usage.Position + usage.Name.Length;
        }

        if (start < LineText.Length)
        {
            highlights.Add(LineText.Substring(start, LineText.Length - start));
        }
    }
}