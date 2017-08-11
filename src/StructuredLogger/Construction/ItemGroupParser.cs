using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class ItemGroupParser
    {
        /// <summary>
        /// Parses a log output string to a list of Items (e.g. ItemGroup with metadata or property string).
        /// </summary>
        /// <param name="message">The message output from the logger.</param>
        /// <param name="prefix">The prefix parsed out (e.g. 'Output Item(s): '.).</param>
        /// <param name="name">Out: The name of the list.</param>
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

            message = message.Replace("\r\n", "\n");
            message = message.Replace('\r', '\n');
            var lines = message.Split('\n');

            var parameter = new Parameter();

            if (lines[0].Length > prefix.Length)
            {
                // we have a weird case of multi-line value
                var nameValue = Utilities.ParseNameValue(lines[0].Substring(prefix.Length));

                parameter.Name = stringTable.Intern(nameValue.Key);

                parameter.AddChild(new Item
                {
                    Text = stringTable.Intern(nameValue.Value)
                });

                for (int i = 1; i < lines.Length; i++)
                {
                    parameter.AddChild(new Item
                    {
                        Text = stringTable.Intern(lines[i])
                    });
                }

                return parameter;
            }

            Item currentItem = null;
            Property currentProperty = null;
            foreach (var line in lines)
            {
                var numberOfLeadingSpaces = Utilities.GetNumberOfLeadingSpaces(line);
                switch (numberOfLeadingSpaces)
                {
                    case 4:
                        if (line.EndsWith("=", StringComparison.Ordinal))
                        {
                            parameter.Name = stringTable.Intern(line.Substring(4, line.Length - 5));
                        }
                        break;
                    case 8:
                        if (line.IndexOf('=') != -1)
                        {
                            var kvp = Utilities.ParseNameValue(line.Substring(8));
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
                                Text = stringTable.Intern(line.Substring(8))
                            };
                            parameter.AddChild(currentItem);
                            currentProperty = null;
                        }
                        break;
                    case 16:
                        var currentLine = line.Substring(16);
                        if (currentItem != null)
                        {
                            if (!currentLine.Contains("="))
                            {
                                // must be a continuation of the metadata value from the previous line
                                if (currentItem.HasChildren)
                                {
                                    var metadata = currentItem.Children[currentItem.Children.Count - 1] as Metadata;
                                    if (metadata != null)
                                    {
                                        metadata.Value = stringTable.Intern((metadata.Value ?? "") + currentLine);
                                    }
                                }
                            }
                            else
                            {
                                var nameValue = Utilities.ParseNameValue(currentLine);
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
