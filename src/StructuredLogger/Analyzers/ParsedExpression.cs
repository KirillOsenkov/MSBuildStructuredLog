using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger;

public class ParsedExpression
{
    public ParsedExpression()
    {
    }

    public string Text { get; set; }

    public string FilePath { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public int Position { get; set; }

    public IList<Span> PropertyReads { get; set; } = new List<Span>();
    public HashSet<string> PropertyNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static ParsedExpression Parse(string expression)
    {
        var spans = TextUtilities.SplitIntoParenthesizedSpans(expression, "$(", ")");

        var result = new ParsedExpression();
        result.Text = expression;

        int index = 0;

        foreach (var span in spans)
        {
            if (span.StartsWith("$(") && span.EndsWith(")"))
            {
                var propertyName = span.Substring(2, span.Length - 3);
                bool isProperty = true;

                if (propertyName.StartsWith("Registry:") ||
                    propertyName.StartsWith("["))
                {
                    isProperty = false;
                }

                if (isProperty)
                {
                    var dot = propertyName.IndexOf('.');
                    if (dot >= 0)
                    {
                        propertyName = propertyName.Substring(0, dot);
                    }

                    result.PropertyNames.Add(propertyName);
                    result.PropertyReads.Add(new Span(index + 2, propertyName.Length));
                }
            }

            index += span.Length;
        }

        return result;
    }
}