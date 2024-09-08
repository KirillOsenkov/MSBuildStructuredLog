using System.Text;
using Xunit;

namespace StructuredLogger.Tests
{
    public class ParseIntoWordsTests
    {
        [Fact]
        public void TestParseIntoWords()
        {
            T("a b", "a", "b");
            T("a \"b c\"", "a", "b c");
            T("a \"b\" c\"", "a", "b", "c\"");
            T("a (b c)", "a", "(b c)");
            T("a (b\" c)", "a", "(b\" c)");
            T("a (b\"f\" c)", "a", "(b\"f\" c)");
            T("a \"b(\"", "a", "b(");
            T("a \"b)\"", "a", "b)");
            T("a \"(b\"", "a", "(b");
            T("a \")b\"", "a", ")b");
            T("a \")b(\"", "a", ")b(");
            T("a \"(b)\"", "a", "(b)");
        }

        private static void T(string query, params string[] expectedParts)
        {
            var actualParts = ParseIntoWords(query);
            Assert.Equal(expectedParts, actualParts);
        }

        private static List<string> ParseIntoWords(string query)
        {
            var result = new List<string>();

            StringBuilder currentWord = new StringBuilder();
            bool isInParentheses = false;
            bool isInQuotes = false;
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                switch (c)
                {
                    case ' ' when !isInParentheses && !isInQuotes:
                        result.Add(TrimQuotes(currentWord.ToString()));
                        currentWord.Clear();
                        break;
                    case '(' when !isInParentheses && !isInQuotes:
                        isInParentheses = true;
                        currentWord.Append(c);
                        break;
                    case ')' when isInParentheses && !isInQuotes:
                        isInParentheses = false;
                        currentWord.Append(c);
                        break;
                    case '"' when !isInParentheses:
                        isInQuotes = !isInQuotes;
                        currentWord.Append(c);
                        break;
                    default:
                        currentWord.Append(c);
                        break;
                }
            }

            result.Add(TrimQuotes(currentWord.ToString()));

            return result;
        }

        private static string TrimQuotes(string word)
        {
            if (word.Length > 2 && word[0] == '"' && word[word.Length - 1] == '"')
            {
                word = word.Substring(1, word.Length - 2);
            }

            return word;
        }
    }
}
