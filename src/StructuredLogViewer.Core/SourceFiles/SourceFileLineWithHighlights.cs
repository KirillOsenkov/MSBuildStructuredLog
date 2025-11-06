using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger;

public class PropertyUsage
{
    public string Name;
    public int Position;
    public int RelativePosition;
    public bool IsWrite;
}

public class SourceFileLineWithHighlights : SourceFileLine
{
    private List<PropertyUsage> usages = new();

    public void AddUsage(PropertyUsage usage)
    {
        usages.Add(usage);
    }

    private bool isBold;
    public bool IsBold
    {
        get => isBold;
        set => SetField(ref isBold, value);
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