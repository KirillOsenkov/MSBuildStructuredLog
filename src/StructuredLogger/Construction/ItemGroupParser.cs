using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class ItemGroupParser
    {
        [ThreadStatic]
        private static readonly List<Span> lineSpans = new List<Span>(10240);

        /// <summary>
        /// Parses a log output string to a list of Items (e.g. ItemGroup with metadata or property string).
        /// </summary>
        /// <param name="message">The message output from the logger.</param>
        /// <param name="prefix">The prefix parsed out (e.g. 'Output Item(s): '.).</param>
        /// <returns>List of items within the list and all metadata.</returns>
        public static object ParsePropertyOrItemList(string message, string prefix, StringCache stringTable)
        {
            if (!Utilities.ContainsLineBreak(message))
            {
                var nameValue = Utilities.ParseNameValue(message, trimFromStart: prefix.Length);
                var property = new Property
                {
                    Name = stringTable.Intern(nameValue.Key),
                    Value = stringTable.Intern(nameValue.Value)
                };
                return property;
            }

            lineSpans.Clear();
            message.CollectLineSpans(lineSpans, includeLineBreakInSpan: false);

            var parameter = new Parameter();

            if (lineSpans[0].Length > prefix.Length)
            {
                // we have a weird case of multi-line value
                var nameValue = Utilities.ParseNameValue(message, lineSpans[0].Skip(prefix.Length));

                parameter.Name = stringTable.Intern(nameValue.Key);

                parameter.AddChild(new Item
                {
                    Text = stringTable.Intern(nameValue.Value)
                });

                for (int i = 1; i < lineSpans.Count; i++)
                {
                    parameter.AddChild(new Item
                    {
                        Text = stringTable.Intern(message.Substring(lineSpans[i]))
                    });
                }

                return parameter;
            }

            Item currentItem = null;
            Property currentProperty = null;
            foreach (var lineSpan in lineSpans)
            {
                var numberOfLeadingSpaces = Utilities.GetNumberOfLeadingSpaces(message, lineSpan);
                switch (numberOfLeadingSpaces)
                {
                    case 4:
                        if (message[lineSpan.End - 1] == '=')
                        {
                            parameter.Name = stringTable.Intern(message.Substring(lineSpan.Start + 4, lineSpan.Length - 5));
                        }
                        break;
                    case 8:
                        var skip8 = message.Substring(lineSpan.Skip(8));
                        var equals = skip8.IndexOf('=');
                        if (equals != -1)
                        {
                            var kvp = Utilities.ParseNameValueWithEqualsPosition(skip8, equals);
                            currentProperty = new Property
                            {
                                Name = stringTable.Intern(kvp.Key),
                                Value = stringTable.Intern(kvp.Value)
                            };
                            parameter.AddChild(currentProperty);
                            currentItem = null;
                        }
                        else
                        {
                            currentItem = new Item
                            {
                                Text = stringTable.Intern(skip8)
                            };
                            parameter.AddChild(currentItem);
                            currentProperty = null;
                        }
                        break;
                    case 16:
                        if (currentItem != null)
                        {
                            var span16 = lineSpan.Skip(16);
                            var equals16 = message.IndexOf(span16, '=');
                            if (equals16 == -1)
                            {
                                // must be a continuation of the metadata value from the previous line
                                if (currentItem.HasChildren)
                                {
                                    var metadata = currentItem.Children[currentItem.Children.Count - 1] as Metadata;
                                    if (metadata != null)
                                    {
                                        var currentLine = message.Substring(span16);
                                        if (!string.IsNullOrEmpty(metadata.Value))
                                        {
                                            metadata.Value = metadata.Value + currentLine;
                                        }
                                        else
                                        {
                                            metadata.Value = currentLine;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var nameValue = Utilities.ParseNameValueWithEqualsPosition(message, span16, equals16);
                                var metadata = new Metadata
                                {
                                    Name = stringTable.Intern(nameValue.Key),
                                    Value = stringTable.Intern(nameValue.Value)
                                };
                                currentItem.AddChild(metadata);
                            }
                        }
                        break;
                    default:
                        var line = message.Substring(lineSpan);
                        if (numberOfLeadingSpaces == 0 && line == prefix)
                        {
                            continue;
                        }

                        // must be a continuation of a multi-line value
                        if (currentProperty != null)
                        {
                            currentProperty.Value += "\n" + line;
                        }
                        else if (currentItem != null && currentItem.HasChildren)
                        {
                            var metadata = currentItem.Children[currentItem.Children.Count - 1] as Metadata;
                            if (metadata != null)
                            {
                                metadata.Value = (metadata.Value ?? "") + line;
                            }
                        }
                        break;
                }
            }

            return parameter;
        }
    }
}
