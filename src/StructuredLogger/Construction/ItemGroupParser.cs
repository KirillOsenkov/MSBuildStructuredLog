using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    internal static class ItemGroupParser
    {
        /// <summary>
        /// Parses a log output string to a list of Items (e.g. ItemGroup with metadata or property string).
        /// </summary>
        /// <param name="message">The message output from the logger.</param>
        /// <param name="prefix">The prefix parsed out (e.g. 'Output Item(s): '.).</param>
        /// <param name="name">Out: The name of the list.</param>
        /// <returns>List of items within the list and all metadata.</returns>
        public static LogProcessNode ParsePropertyOrItemList(string message, string prefix)
        {
            LogProcessNode result;

            var lines = message.Split('\n');

            if (lines.Length == 1)
            {
                var line = lines[0];
                line = line.Substring(prefix.Length);
                var nameValue = ParseNameValue(line);
                result = new Property { Name = nameValue.Key, Value = nameValue.Value };
                return result;
            }

            if (lines[0].Length > prefix.Length)
            {
                // we have a weird case of multi-line value
                var nameValue = ParseNameValue(lines[0].Substring(prefix.Length));

                result = new InputParameter { Name = nameValue.Key };

                result.AddChild(new Item { Text = nameValue.Value.Replace("\r", "") });
                for (int i = 1; i < lines.Length; i++)
                {
                    result.AddChild(new Item { Text = lines[i].Replace("\r", "") });
                }

                return result;
            }

            result = new InputParameter();

            Item currentItem = null;
            foreach (var line in lines)
            {
                switch (GetNumberOfLeadingSpaces(line))
                {
                    case 4:
                        if (line.EndsWith("=", StringComparison.Ordinal))
                        {
                            result.Name = line.Substring(4, line.Length - 5);
                        }
                        break;
                    case 8:
                        currentItem = new Item { Text = line.Substring(8) };
                        result.AddChild(currentItem);
                        break;
                    case 16:
                        if (currentItem != null)
                        {
                            var nameValue = ParseNameValue(line.Substring(16));
                            var metadata = new Property(nameValue);
                            currentItem.AddChild(metadata);
                        }
                        break;
                }
            }

            return result;
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
