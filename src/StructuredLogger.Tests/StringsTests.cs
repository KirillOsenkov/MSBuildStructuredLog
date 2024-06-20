using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class StringsTests
    {
        //[Fact]
        public void TestStrings()
        {
            //var strings = Serialization.ReadStringsFromFile(@"C:\temp\strings.bin");
            string[] strings = ["abc", "ab", "abd", "ac"];
            Process(strings);
        }

        private void Process(IReadOnlyList<string> array)
        {
            var dic = new Dictionary<char, Dictionary<char, int>>();
            int maxWordLength = 0;
            char[] separators = [' ', '\n'];

            array = array
                .SelectMany(w => w.Split(separators, System.StringSplitOptions.RemoveEmptyEntries))
                .Distinct()
                .Where(w => w.Length < 255)
                .ToArray();

            foreach (var word in array)
            {
                for (int i = 0; i < word.Length - 1; i++)
                {
                    char current = word[i];
                    char next = word[i + 1];
                    Add(current, next);
                }

                if (word.Length > maxWordLength)
                {
                    maxWordLength = word.Length;
                }
            }

            var wordsWithWeights = new List<(string word, float weight)>();

            foreach (var word in array)
            {
                int sum = 0;
                for (int i = 0; i < word.Length - 1; i++)
                {
                    char current = word[i];
                    char next = word[i + 1];
                    int weight = Get(current, next);
                    sum += weight;
                }

                wordsWithWeights.Add((word, sum / (float)word.Length));
            }

            var ordered = wordsWithWeights.OrderBy(_ => _.weight).ToArray();
            var descending = wordsWithWeights.OrderByDescending(_ => _.weight).ToArray();

            var minWordByLength = new string[maxWordLength + 1];

            var outliers = new List<(string word, float weight, float outlier)>();

            for (int i = 0; i < ordered.Length - 1; i++)
            {
                var word = ordered[i].word;
                var length = word.Length;
                if (minWordByLength[length] == null)
                {
                    minWordByLength[length] = word;
                }

                var weightThis = ordered[i].weight;
                var weightNext = ordered[i + 1].weight;
                var delta = ordered[i + 1].word.Length - word.Length;
                outliers.Add((word, weightThis, delta / (float)word.Length));
            }

            var orderedOutliers = outliers
                .OrderBy(g => g.outlier * Math.Pow(g.word.Length, -0.1))
                .ThenBy(g => g.weight)
                .ToArray();

            //WriteCsv(outliers);

            var top = ordered.Take(10).ToArray();

            void Add(char a, char b)
            {
                if (!dic.TryGetValue(a, out var bucket))
                {
                    bucket = new Dictionary<char, int>();
                    dic[a] = bucket;
                }

                if (bucket.TryGetValue(b, out int value))
                {
                    bucket[b] = value + 1;
                }
                else
                {
                    bucket[b] = 1;
                }
            }

            int Get(char a, char b)
            {
                if (dic.TryGetValue(a, out var bucket) && bucket.TryGetValue(b, out var result))
                {
                    return result;
                }

                return 0;
            }
        }

        private void WriteCsv(List<(string word, float weight, float outlier)> outliers)
        {
            var sb = new StringBuilder();
            foreach (var item in outliers)
            {
                sb.AppendLine($"{item.weight},{item.outlier},{item.word.Length}");
            }

            var text = sb.ToString();
            File.WriteAllText(@"C:\temp\stats.csv", text);
        }
    }
}