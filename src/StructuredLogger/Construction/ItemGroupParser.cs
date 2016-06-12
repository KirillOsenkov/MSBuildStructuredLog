using System;
using System.Collections.Generic;

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
            message = message.Replace("\r\n", "\n");
            message = message.Replace('\r', '\n');
            var lines = message.Split('\n');

            if (lines.Length == 1)
            {
                var line = lines[0];
                line = line.Substring(prefix.Length);
                var nameValue = ParseNameValue(line);
                var property = new Property
                {
                    Name = stringTable.Intern(nameValue.Key),
                    Value = stringTable.Intern(nameValue.Value)
                };
                return property;
            }

            var parameter = new Parameter();

            if (lines[0].Length > prefix.Length)
            {
                // we have a weird case of multi-line value
                var nameValue = ParseNameValue(lines[0].Substring(prefix.Length));

                parameter.Name = stringTable.Intern(nameValue.Key);

                parameter.AddChild(new Item
                {
                    Text = stringTable.Intern(nameValue.Value.Replace("\r", ""))
                });

                for (int i = 1; i < lines.Length; i++)
                {
                    parameter.AddChild(new Item
                    {
                        Text = stringTable.Intern(lines[i].Replace("\r", ""))
                    });
                }

                return parameter;
            }

            Item currentItem = null;
            foreach (var line in lines)
            {
                switch (GetNumberOfLeadingSpaces(line))
                {
                    case 4:
                        if (line.EndsWith("=", StringComparison.Ordinal))
                        {
                            parameter.Name = stringTable.Intern(line.Substring(4, line.Length - 5));
                        }
                        break;
                    case 8:
                        currentItem = new Item
                        {
                            Text = stringTable.Intern(line.Substring(8))
                        };
                        parameter.AddChild(currentItem);
                        break;
                    case 16:
                        if (currentItem != null)
                        {
                            var currentLine = line.Substring(16);
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
                                var nameValue = ParseNameValue(currentLine);
                                var metadata = new Metadata
                                {
                                    Name = stringTable.Intern(nameValue.Key),
                                    Value = stringTable.Intern(nameValue.Value)
                                };
                                currentItem.AddChild(metadata);
                            }
                        }
                        break;
                }
            }

            return parameter;
        }

        private static KeyValuePair<string, string> ParseNameValue(string nameEqualsValue)
        {
            var equals = nameEqualsValue.IndexOf('=');
            var name = nameEqualsValue.Substring(0, equals);
            var value = nameEqualsValue.Substring(equals + 1);
            return new KeyValuePair<string, string>(name, value);
        }

        private static int GetNumberOfLeadingSpaces(string line)
        {
            int result = 0;
            while (result < line.Length && line[result] == ' ')
            {
                result++;
            }

            return result;
        }
    }
}
