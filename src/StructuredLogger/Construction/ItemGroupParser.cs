using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class ItemGroupParser
    {
        [ThreadStatic]
        private static List<Span> lineSpans;

        /// <summary>
        /// Parses a log output string to a list of Items (e.g. ItemGroup with metadata or property string).
        /// </summary>
        /// <param name="message">The message output from the logger.</param>
        /// <param name="prefix">The prefix parsed out (e.g. 'Output Item(s): '.).</param>
        /// <returns>List of items within the list and all metadata.</returns>
        public static BaseNode ParsePropertyOrItemList(string message, string prefix, StringCache stringTable, bool isOutputItem = false)
        {
            if (!TextUtilities.ContainsLineBreak(message))
            {
                var nameValue = TextUtilities.ParseNameValue(message, trimFromStart: prefix.Length);
                if (!isOutputItem)
                {
                    var property = new Property
                    {
                        Name = stringTable.Intern(nameValue.Key),
                        Value = stringTable.Intern(nameValue.Value)
                    };
                    return property;
                }
                else
                {
                    var singleItem = new AddItem { Name = stringTable.Intern(nameValue.Key) };
                    var item = new Item { Text = stringTable.Intern(nameValue.Value) };
                    singleItem.AddChild(item);
                    return singleItem;
                }
            }

            // Can't use a field initializer with ThreadStatic.
            if (lineSpans == null)
            {
                lineSpans = new List<Span>(10240);
            }

            lineSpans.Clear();
            message.CollectLineSpans(lineSpans, includeLineBreakInSpan: false);

            NamedNode parameter = isOutputItem ? new AddItem() : new Parameter();

            if (lineSpans[0].Length > prefix.Length)
            {
                // we have a weird case of multi-line value
                var nameValue = TextUtilities.ParseNameValue(message, lineSpans[0].Skip(prefix.Length));

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
                if (TextUtilities.IsWhitespace(message, lineSpan))
                {
                    continue;
                }

                var numberOfLeadingSpaces = TextUtilities.GetNumberOfLeadingSpaces(message, lineSpan);
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
                            var kvp = TextUtilities.ParseNameValueWithEqualsPosition(skip8, equals);
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
                        if (currentItem == null && currentProperty != null)
                        {
                            // we incorrectly interpreted the previous line as Property, not Item (because it had '=')
                            // and so we created a property out of name/value.
                            // Fix this by turning it into an Item.
                            if (parameter.LastChild == currentProperty)
                            {
                                currentItem = new Item
                                {
                                    Text = stringTable.Intern(currentProperty.Name + "=" + currentProperty.Value)
                                };
                                parameter.Children.RemoveAt(parameter.Children.Count - 1);
                                currentProperty = null;
                                parameter.AddChild(currentItem);
                            }
                        }

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
                                var nameValue = TextUtilities.ParseNameValueWithEqualsPosition(message, span16, equals16);
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

        public static void ParseThereWasAConflict(TreeNode parent, string message, StringCache stringTable)
        {
            if (lineSpans == null)
            {
                lineSpans = new List<Span>(10240);
            }

            lineSpans.Clear();
            message.CollectLineSpans(lineSpans, includeLineBreakInSpan: false);

            Item item4 = null;
            Item item8 = null;
            Item item10 = null;

            for (int i = 0; i < lineSpans.Count; i++)
            {
                var lineSpan = lineSpans[i];
                var numberOfLeadingSpaces = TextUtilities.GetNumberOfLeadingSpaces(message, lineSpan);
                switch (numberOfLeadingSpaces)
                {
                    case 0:
                    case 4:
                        item4 = Add(parent, message, lineSpan, numberOfLeadingSpaces, stringTable);
                        item8 = null;
                        item10 = null;
                        break;
                    case 8:
                        item8 = Add(item4, message, lineSpan, numberOfLeadingSpaces, stringTable);
                        item10 = null;
                        break;
                    case 10:
                        item10 = Add(item8, message, lineSpan, numberOfLeadingSpaces, stringTable);
                        break;
                    case 12:
                        Add(item10, message, lineSpan, numberOfLeadingSpaces, stringTable);
                        break;
                    default:
                        Add(item10 ?? item8 ?? item4 ?? parent, message, lineSpan, numberOfLeadingSpaces, stringTable);
                        break;
                }
            }

            static Item Add(TreeNode parent, string text, Span span, int spaces, StringCache stringTable)
            {
                if (spaces >= span.Length || parent == null)
                {
                    return null;
                }

                string line = text.Substring(span.Start + spaces, span.Length - spaces);

                var item = new Item
                {
                    Text = stringTable.Intern(line)
                };
                parent.AddChild(item);
                return item;
            }
        }
    }
}
