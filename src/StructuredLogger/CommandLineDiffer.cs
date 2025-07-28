using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StructuredLogger
{
    public class CommandLineDiffer
    {
        private static bool IsSwitchToken(char token)
        {
            switch (token)
            {
                case '/':
                case '-':
                    return true;
                default:
                    break;
            }

            return false;
        }

        private static char GetNextLetter(string text, int startIndex, out int endIndex)
        {
            char c = char.MinValue;

            while (startIndex < text.Length)
            {
                c = text[startIndex];
                if (char.IsWhiteSpace(c))
                {
                    startIndex++;
                    continue;
                }

                break;
            }

            endIndex = startIndex;
            return c;
        }


        public static int GetIndexOfFirstDifference(string str1, string str2, bool caseSensitive)
        {
            int minLength = Math.Min(str1.Length, str2.Length);

            for (int i = 0; i < minLength; i++)
            {
                if (caseSensitive && str1[i] != str2[i])
                {
                    return i;
                }
                else if (!caseSensitive && char.ToLowerInvariant(str1[i]) != char.ToLowerInvariant(str2[i]))
                {
                    return i;
                }
            }

            // If one string is longer than the other, the difference starts at the end of the shorter string
            if (str1.Length != str2.Length)
            {
                return minLength;
            }

            // No differences found
            return -1;
        }


        public static int CountOccurrences(string input, string target, int endIndex = -1)
        {
            if (endIndex >= input.Length && endIndex < 0) { endIndex = input.Length - 1; } // Ensure index is within bounds
            int count = 0;

            int startIndex = 0;
            while (startIndex < input.Length && startIndex < endIndex)
            {
                startIndex = input.IndexOf(target, startIndex);
                if (startIndex == -1 || startIndex >= endIndex)
                    break;

                count++;
                startIndex += target.Length;
            }

            return count;
        }

        public static List<string> ParseParameters(string cmdLine, int startIndex, CommandLineDiffSetting setting = null)
        {
            setting ??= CommandLineDiffSetting.Default;
            var parameters = new List<string>();
            bool inQuotes = false;
            char escapeChar = '\\';
            var current = new List<char>();

            for (int i = startIndex; i < cmdLine.Length; i++)
            {
                char c = cmdLine[i];

                if (c == '"' && (i == 0 || cmdLine[i - 1] != escapeChar))
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Count > 0)
                    {
                        parameters.Add(new string(current.ToArray()));
                        current.Clear();
                    }
                }
                else
                {
                    if (c == escapeChar && i + 1 < cmdLine.Length && cmdLine[i + 1] == '"')
                    {
                        i++; // Skip the escape character
                        current.Add('"');
                    }
                    else
                    {
                        current.Add(c);
                    }
                }
            }

            if (current.Count > 0)
            {
                parameters.Add(new string(current.ToArray()));
            }

            return parameters;
        }

        public static bool TryParseExe(string commandLine, out string program, CommandLineDiffSetting setting = null)
        {
            setting ??= CommandLineDiffSetting.Default;
            program = "";

            char c = GetNextLetter(commandLine, 0, out int startIndex);


            if (c == '\"')
            {
                int endIndex = commandLine.IndexOf('\"', startIndex + 1);
                if (endIndex != -1)
                {
                    program = commandLine.Substring(startIndex + 1, (endIndex - 1) - startIndex);
                    return true;
                }

                return false;
            }

            {
                int endIndex = commandLine.IndexOf(".exe", setting.ToStringComparison);
                if (endIndex != -1)
                {
                    program = commandLine.Substring(startIndex, (endIndex + 4) - startIndex);
                    return true;
                }
            }

            return false;
        }

        public static bool TryParseCommandLine(string commandLine, out List<string> result, CommandLineDiffSetting setting = null)
        {
            setting ??= CommandLineDiffSetting.Default;
            result = new List<string>();
            int startIndex = 0;

            if (TryParseExe(commandLine, out string program, setting))
            {
                startIndex = program.Length + 1;
                result.Add(program);
            }

            var paramResult = ParseParameters(commandLine, startIndex, setting);
            result.AddRange(paramResult);

            return true;
        }

        private class ParameterEntry
        {
            public string Parameter { get; set; }

            public string Prefix { get; set; } = string.Empty;

            public bool Matched { get; set; } = false;

            public bool ParameterMatched { get; set; } = false;

            public static List<ParameterEntry> ToList(List<string> paramList)
            {
                List<ParameterEntry> parameterEntries = new List<ParameterEntry>(paramList.Count);
                string lastSwitchParam = string.Empty;

                foreach (string param in paramList)
                {
                    ParameterEntry paraEntry = new ParameterEntry()
                    {
                        Parameter = param,
                    };

                    if (IsSwitchToken(param[0]))
                    {
                        // Note: generally switches don't have prefix.
                        lastSwitchParam = param;
                    }
                    else if (!string.IsNullOrEmpty(lastSwitchParam))
                    {
                        // Note: generally a switch only have one argument, but there might be exceptions.
                        paraEntry.Prefix = lastSwitchParam;
                        lastSwitchParam = string.Empty;
                    }

                    parameterEntries.Add(paraEntry);
                }

                return parameterEntries;
            }
        }

        public class CommandLineDiffSetting
        {
            public static readonly CommandLineDiffSetting Default = new CommandLineDiffSetting();

            public bool CaseSensitive { get; set; } = true;

            public StringComparison ToStringComparison => CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        }

        public static bool TryCompare(string left, string right, out List<string> leftRemainder, out List<string> rightRemainder, CommandLineDiffSetting setting = null)
        {
            setting ??= CommandLineDiffSetting.Default;
            leftRemainder = new List<string>();
            rightRemainder = new List<string>();

            if (!TryParseCommandLine(left, out var cmdLeft, setting) || !TryParseCommandLine(right, out var cmdRight, setting))
            {
                return false;
            }

            // First pass: Matches with the same index.

            var leftParams = ParameterEntry.ToList(cmdLeft);
            var rightParams = ParameterEntry.ToList(cmdRight);

            for (int i = 0; i < leftParams.Count; i++)
            {
                if (i < rightParams.Count && leftParams[i].Parameter.Equals(rightParams[i].Parameter, setting.ToStringComparison))
                {
                    if (leftParams[i].Prefix.Equals(rightParams[i].Prefix, setting.ToStringComparison))
                    {
                        leftParams[i].Matched = true;
                        rightParams[i].Matched = true;
                    }
                }
            }

            // Second pass: Find n^2 all matches.

            var leftParamRemainder = leftParams.Where(p => !p.Matched).ToList();
            var rightParamRemainder = rightParams.Where(p => !p.Matched).ToList();

            for (int i = 0; i < leftParamRemainder.Count; i++)
            {
                for (int j = 0; j < rightParamRemainder.Count; j++)
                {
                    if (!rightParamRemainder[j].Matched &&
                        leftParamRemainder[i].Parameter.Equals(rightParamRemainder[j].Parameter, setting.ToStringComparison))
                    {
                        if (leftParamRemainder[i].Prefix.Equals(rightParamRemainder[j].Prefix, setting.ToStringComparison))
                        {
                            leftParamRemainder[i].Matched = true;
                            rightParamRemainder[j].Matched = true;
                        }
                    }
                }
            }

            // Third Pass: Find standalone switches.

            for (int i = 0; i < leftParamRemainder.Count; i++)
            {
                for (int j = 0; j < rightParamRemainder.Count; j++)
                {
                    if (!rightParamRemainder[j].Matched &&
                        leftParamRemainder[i].Parameter.Equals(rightParamRemainder[j].Parameter, setting.ToStringComparison))
                    {
                        leftParamRemainder[i].Matched = true;
                        rightParamRemainder[j].Matched = true;
                    }
                }
            }

            // Populate output remainder

            foreach (var param in leftParamRemainder)
            {
                if (!param.Matched)
                {
                    leftRemainder.Add(param.Parameter);
                }
            }

            foreach (var param in rightParamRemainder)
            {
                if (!param.Matched)
                {
                    rightRemainder.Add(param.Parameter);
                }
            }

            return true;
        }
    }
}
