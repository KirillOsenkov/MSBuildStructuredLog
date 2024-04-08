using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace StructuredLogViewer
{
    [DebuggerDisplay("{Level}> {Result} : {Text}")]
    public class ConditionNode : IEnumerable<ConditionNode>
    {
        public enum ConditionOperator
        {
            None,
            AND,
            OR,
        }

        public enum EqualityOperator
        {
            None,
            Equal,
            NotEqual,
            Not,

            // numeric comparison
            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual,
        }

        private class ParsingState
        {
            public StringBuilder literalBuilder = new();
            public string leftString = "";
            public string rightString = "";
            public EqualityOperator comparer = EqualityOperator.None;
            public bool DoEvaluate = false;

            public bool SaveToNode(ConditionNode currentNode)
            {
                bool saved = false;

                if (literalBuilder.Length > 0)
                {
                    currentNode.Text = literalBuilder.ToString();

                    if (DoEvaluate)
                    {
                        currentNode.Result = this.Compare();
                    }

                    saved = true;
                }

                Reset();

                return saved;
            }

            public void AddValue(string value)
            {
                if (DoEvaluate)
                {
                    if (string.IsNullOrEmpty(leftString))
                    {
                        leftString = value;
                    }
                    else if (string.IsNullOrEmpty(rightString))
                    {
                        rightString = value;
                    }
                }

                literalBuilder.Append(value);
            }

            public bool Compare()
            {
                // if numeric comparison
                if (comparer >= EqualityOperator.LessThan && comparer <= EqualityOperator.GreaterThanOrEqual)
                {
                    if (leftString.Count(p => p == '.') > 1 || rightString.Count(p => p == '.') > 1)
                    {
                        if (Version.TryParse(leftString, out var leftValue)
                            && Version.TryParse(rightString, out var rightValue))
                        {
                            if (comparer == EqualityOperator.LessThan)
                            { return leftValue < rightValue; }
                            else if (comparer == EqualityOperator.LessThanOrEqual)
                            { return leftValue <= rightValue; }
                            else if (comparer == EqualityOperator.GreaterThan)
                            { return leftValue > rightValue; }
                            else if (comparer == EqualityOperator.GreaterThanOrEqual)
                            { return leftValue >= rightValue; }
                        }
                    }
                    else if (Double.TryParse(leftString, out var leftValue)
                             && Double.TryParse(rightString, out var rightValue))
                    {
                        if (comparer == EqualityOperator.LessThan)
                        { return leftValue < rightValue; }
                        else if (comparer == EqualityOperator.LessThanOrEqual)
                        { return leftValue <= rightValue; }
                        else if (comparer == EqualityOperator.GreaterThan)
                        { return leftValue > rightValue; }
                        else if (comparer == EqualityOperator.GreaterThanOrEqual)
                        { return leftValue >= rightValue; }
                    }

                    return false;
                }

                if (comparer == EqualityOperator.None || comparer == EqualityOperator.Not)
                {
                    if (string.IsNullOrEmpty(rightString))
                    {
                        if (bool.TryParse(leftString, out bool leftValue))
                        {
                            return comparer == EqualityOperator.Not ? !leftValue : leftValue;
                        }

                        // Unable to evaluate Exists() function, always return true.
                        return true;
                    }
                }

                if (comparer == EqualityOperator.NotEqual)
                {
                    return !string.Equals(leftString, rightString, StringComparison.OrdinalIgnoreCase);
                }

                if (comparer == EqualityOperator.Equal)
                {
                    return string.Equals(leftString, rightString, StringComparison.OrdinalIgnoreCase);
                }

                throw new NotImplementedException();
            }

            private void Reset()
            {
                literalBuilder.Clear();
                leftString = "";
                rightString = "";
                comparer = EqualityOperator.None;
            }
        }

        // Process() computes if it is true or not.
        public bool Result = true;

        public int Level = 0;

        // AND or OR between Children.
        public ConditionOperator Operator = ConditionOperator.None;

        public string Text = "";

        public ConditionNode Parent;

        public List<ConditionNode> Children = new();

        public static ConditionNode ParseAndProcess(string unevaluated, string evaluated)
        {
            var unevaluatedNode = Parse(unevaluated);
            var evaluatedNode = Parse(evaluated, true);

            return Process(unevaluatedNode, evaluatedNode);
        }

        public static ConditionNode Process(ConditionNode unevaluatedNode, ConditionNode evaluatedNode)
        {
            var unevalEnum = unevaluatedNode.GetEnumerator();
            var evalEnum = evaluatedNode.GetEnumerator();

            bool unevalNext = true;
            bool evalNext = true;

            while (unevalNext && evalNext)
            {
                unevalNext = unevalEnum.MoveNext();
                evalNext = evalEnum.MoveNext();

                if (unevalNext != evalNext)
                {
                    throw new Exception("Condition parsing return a different number of nodes {unevalEnum.Count()} vs {evalEnum.Count()}.");
                }

                if (unevalNext)
                {
                    if (!string.IsNullOrEmpty(unevalEnum.Current.Text) && !string.IsNullOrEmpty(unevalEnum.Current.Text))
                    {
                        unevalEnum.Current.Text = $"{unevalEnum.Current.Text} \u2794 {evalEnum.Current.Text}";
                    }

                    unevalEnum.Current.Result = evalEnum.Current.Result;
                }
            }

            return unevaluatedNode;
        }

        public static ConditionNode Parse(string text, bool doEvaluate = false)
        {
            var root = new ConditionNode()
            {
                Result = true,
                Operator = ConditionOperator.AND,
            };

            ConditionNode currentGroup = root;

            ParsingState state = new ParsingState();
            state.DoEvaluate = doEvaluate;
            int level = 0;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                switch (c)
                {
                    case '(':
                        {
                            var newGroup = new ConditionNode()
                            {
                                Level = level,
                                Parent = currentGroup
                            };

                            level++;

                            currentGroup.Children.Add(newGroup);
                            currentGroup = newGroup;
                        }
                        break;
                    case ')':
                        {
                            var childNode = new ConditionNode()
                            {
                                Level = level,
                                Parent = currentGroup,
                            };

                            if (state.SaveToNode(childNode))
                            {
                                currentGroup.Children.Add(childNode);
                            }

                            level--;

                            if (state.DoEvaluate)
                            {
                                ComputeGroupResult(currentGroup);
                            }

                            currentGroup = currentGroup.Parent;
                        }
                        break;
                    case '\'':
                    case '\"':
                        {
                            int endIndex = FindMatchingQuote(text, i);
                            if (endIndex == -1)
                            {
                                throw new Exception("Can't parse");
                            }

                            // subtract trailing quote
                            if (endIndex - i - 1 == 0)
                            {
                                state.literalBuilder.Append(c);
                                state.literalBuilder.Append(c);
                            }
                            else
                            {
                                // skip starting quote and subtract trailing quote
                                string value = text.Substring(i + 1, endIndex - i - 1);

                                state.literalBuilder.Append(c);
                                state.AddValue(value);
                                state.literalBuilder.Append(c);
                            }

                            i = endIndex;
                        }
                        break;
                    case '$':
                    case '@':
                    case '%':
                        {
                            if (text[i + 1] == '(')
                            {
                                int endPos = FindMatchingParam(text, i + 1);
                                state.literalBuilder.Append(text.Substring(i, endPos - i + 1));
                                i = endPos;
                            }
                        }
                        break;
                    case 'a':
                    case 'A':
                    case 'o':
                    case 'O':
                        {
                            var op = IsAndOrToken(text, i, out int nextPos);

                            if (op != ConditionOperator.None)
                            {
                                // Create a new sister node
                                if (currentGroup.Operator != ConditionOperator.None && currentGroup.Operator != op)
                                {
                                    // TODO: handle mixing AND and OR.
                                }

                                currentGroup.Operator = op;

                                var childNode = new ConditionNode()
                                {
                                    Level = level,
                                    Parent = currentGroup,
                                };

                                if (state.SaveToNode(childNode))
                                {
                                    currentGroup.Children.Add(childNode);
                                }

                                i = nextPos;
                                break;
                            }
                        }
                        goto default;
                    case '!':
                        {
                            if (IsSpecialFunction(text, i + 1, out int newIndex))
                            {
                                // !Exists()
                                state.literalBuilder.Append(text, i, newIndex - i + 1);

                                var childNode = new ConditionNode() { Level = level, Parent = currentGroup };
                                if (state.SaveToNode(childNode))
                                {
                                    currentGroup.Children.Add(childNode);
                                }

                                i = newIndex;
                            }
                            else
                            {
                                state.comparer = EqualityOperator.Not;
                                state.literalBuilder.Append(c);
                            }
                        }
                        break;
                    case '=':
                        {
                            if (state.comparer == EqualityOperator.LessThan)
                            {
                                state.comparer = EqualityOperator.LessThanOrEqual;
                            }
                            else if (state.comparer == EqualityOperator.GreaterThan)
                            {
                                state.comparer = EqualityOperator.GreaterThanOrEqual;
                            }
                            else if (state.comparer == EqualityOperator.Not)
                            {
                                state.comparer = EqualityOperator.NotEqual;
                            }
                            else if (state.comparer == EqualityOperator.None)
                            {
                                state.comparer = EqualityOperator.Equal;
                            }

                            state.literalBuilder.Append(c);
                        }
                        break;
                    case '<':
                        {
                            state.comparer = EqualityOperator.LessThan;
                            state.literalBuilder.Append(c);
                        }
                        break;
                    case '>':
                        {
                            state.comparer = EqualityOperator.GreaterThan;
                            state.literalBuilder.Append(c);
                        }
                        break;
                    default:
                        {
                            if (IsSpecialFunction(text, i, out int newIndex))
                            {
                                state.literalBuilder.Append(text, i, newIndex - i + 1);

                                var childNode = new ConditionNode() { Level = level, Parent = currentGroup };
                                if (state.SaveToNode(childNode))
                                {
                                    currentGroup.Children.Add(childNode);
                                }

                                i = newIndex;
                            }
                            else if (!Char.IsWhiteSpace(c))
                            {
                                // could just a string.
                                int endPos = text.IndexOf(' ', i);
                                if (endPos == -1)
                                {
                                    endPos = text.Length;
                                }

                                // subtract trailing quote
                                if (endPos - i > 0)
                                {
                                    string value = text.Substring(i, endPos - i);
                                    state.AddValue(value);
                                }

                                i = endPos;
                            }
                        }
                        break;
                }
            }

            // Check if there is content is the last node.

            var child = new ConditionNode() { Level = level, Parent = currentGroup };
            if (state.SaveToNode(child))
            {
                currentGroup.Children.Add(child);
            }

            // Unwind to the root node.
            if (state.DoEvaluate)
            {
                while (currentGroup != null)
                {
                    ComputeGroupResult(currentGroup);
                    currentGroup = currentGroup.Parent;
                }
            }

            return root;
        }

        private static void ComputeGroupResult(ConditionNode group)
        {
            if (group.Children.Count > 0)
            {
                // Compare all the children using the Operator
                bool result = false;

                if (group.Operator == ConditionOperator.OR)
                {
                    foreach (var child in group.Children)
                    {
                        if (child.Result)
                        {
                            result = true;
                            break;
                        }
                    }
                }
                else if (group.Operator == ConditionOperator.AND)
                {
                    result = true;

                    foreach (var child in group.Children)
                    {
                        if (!child.Result)
                        {
                            result = false;
                            break;
                        }
                    }
                }

                group.Result = result;
            }
        }

        private static ConditionOperator IsAndOrToken(string text, int index, out int newIndex)
        {
            // check if the next few characters matches 'and' or 'or' keyword
            // var characters = text.AsSpan(i, 3);
            if (text[index + 0] == 'a' || text[index + 0] == 'A')
            {
                if ((text[index + 1] == 'n' || text[index + 1] == 'N') &&
                    (text[index + 2] == 'd' || text[index + 2] == 'D'))
                {
                    newIndex = index + 3;
                    return ConditionOperator.AND;
                }
            }
            else if (text[index + 0] == 'o' || text[index + 0] == 'O')
            {
                if (text[index + 1] == 'r' || text[index + 1] == 'R')
                {
                    newIndex = index + 2;
                    return ConditionOperator.OR;
                }
            }

            newIndex = index;
            return ConditionOperator.None;
        }

        private static string[] SpecialFunctions = { "Exists", "HasTrailingSlash" };

        private static bool IsSpecialFunction(string text, int index, out int newIndex)
        {
            char c = text[index];
            foreach (string special in SpecialFunctions)
            {
                // fast character check
                if (char.ToUpper(c) == special[0])
                {
                    if (string.CompareOrdinal(text, index, SpecialFunctions[0], 0, SpecialFunctions[0].Length) == 0)
                    {
                        newIndex = FindMatchingParam(text, index + SpecialFunctions[0].Length);
                        return true;
                    }
                }
            }

            newIndex = index;
            return false;
        }

        private static int FindMatchingParam(string text, int index)
        {
            int level = 0;

            do
            {
                char c = text[index];
                if (c == ')')
                {
                    level--;
                }
                else if (c == '(')
                {
                    level++;
                }

                index++;
            } while (level > 0);

            return index - 1;
        }

        private static int FindMatchingQuote(string text, int index)
        {
            char quote = text[index];

            do
            {
                index++;
                char c = text[index];

                if (c == quote)
                {
                    return index;
                }

                if (c == '(')
                {
                    index = FindMatchingParam(text, index);
                }
            }
            while (index < text.Length);

            return -1;
        }

        public IEnumerator<ConditionNode> GetEnumerator()
        {
            yield return this;
            foreach (var child in Children)
            {
                foreach (var childNode in child)
                {
                    yield return childNode;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            yield return this;
            foreach (var child in Children)
            {
                foreach (var childNode in child)
                {
                    yield return childNode;
                }
            }
        }
    }

}
