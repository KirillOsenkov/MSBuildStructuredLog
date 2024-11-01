using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DotUtils.MsBuild.SensitiveDataDetector;
using Microsoft.Build.SensitiveDataDetector;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SecretsSearch : ISearchExtension
    {
        private readonly Build _build;
        private readonly Dictionary<SensitiveDataKind, ISensitiveDataDetector> _detectors;
        private readonly Dictionary<string, Dictionary<SensitiveDataKind, List<SecretDescriptor>>> _secretCache = new();

        public SecretsSearch(Build build)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));

            _detectors = new()
            {
                { SensitiveDataKind.CommonSecrets, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.CommonSecrets, false) },
                { SensitiveDataKind.ExplicitSecrets, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.ExplicitSecrets, false) },
                { SensitiveDataKind.Username, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.Username, false) }
            };
        }

        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> results, int maxResults)
        {
            if (string.Equals(matcher.TypeKeyword, "secret", StringComparison.OrdinalIgnoreCase))
            {
                var activeDetectors = GetActiveDetectors(matcher.NotMatchers);
                var foundResults = ScanForSecrets(_build.StringTable.Instances, activeDetectors, maxResults);

                if (foundResults.Any())
                {
                    foreach (var result in foundResults)
                    {
                        results.Add(result);
                    }
                }
                else
                {
                    results.Add(new SearchResult(new Message { Text = "No secret(s) were detected in the tree." }));
                }

                return true;
            }

            return false;
        }

        public List<SecretDescriptor> SearchSecrets(string text, IList<NodeQueryMatcher> matcher, int maxResults)
        {
            var activeDetectors = GetActiveDetectors(matcher);

            var secrets = DetectSecrets(text, activeDetectors);

            return secrets.Take(maxResults).ToList();
        }

        private IEnumerable<SearchResult> ScanForSecrets(IEnumerable<string> stringsPool, Dictionary<SensitiveDataKind, ISensitiveDataDetector> detectors, int maxResults)
        {
            var secretsSet = new HashSet<string>();
            foreach (var text in stringsPool)
            {
                var secretResults = DetectSecrets(text, detectors);
                foreach (SecretDescriptor secretDescriptor in secretResults)
                {
                    secretsSet.Add(secretDescriptor.Secret);
                }
            }

            var results = new List<SearchResult>();
            if (_build.SearchIndex is { } index)
            {
                foreach (var text in secretsSet)
                {
                    index.MaxResults = maxResults;
                    index.MarkResultsInTree = false;
                    IEnumerable<SearchResult> indexResults = index.FindNodes(text, CancellationToken.None);
                    if (indexResults.Any())
                    {
                        results.AddRange(indexResults);
                    }
                }
            }

            return results;
        }

        private List<SecretDescriptor> DetectSecrets(string text, Dictionary<SensitiveDataKind, ISensitiveDataDetector> detectors)
        {
            if (_secretCache.TryGetValue(text, out var cachedSecrets))
            {
                return cachedSecrets
                    .Where(kv => detectors.Any(d => d.Key == kv.Key))
                    .SelectMany(kv => kv.Value)
                    .ToList();
            }

            var results = new Dictionary<SensitiveDataKind, List<SecretDescriptor>>();

            foreach (var detector in detectors)
            {
                Dictionary<SensitiveDataKind, List<SecretDescriptor>> detectedSecrets = detector.Value.Detect(text);
                foreach (KeyValuePair<SensitiveDataKind, List<SecretDescriptor>> kv in detectedSecrets)
                {
                    if (kv.Value.Any())
                    {
                        results[kv.Key] = kv.Value;
                    }
                }
            }

            if (results.Any())
            {
                _secretCache[text] = results;
            }

            return results.Values.SelectMany(v => v).ToList();
        }

        private Dictionary<SensitiveDataKind, ISensitiveDataDetector> GetActiveDetectors(IList<NodeQueryMatcher> notMatchers)
        {
            if (!notMatchers.Any())
            {
                return new Dictionary<SensitiveDataKind, ISensitiveDataDetector>(_detectors);
            }

            return _detectors
                .Where(d => !notMatchers.Any(m => Enum.TryParse<SensitiveDataKind>(m.Query, true, out var kind) && kind == d.Key))
                .ToDictionary(k => k.Key, v => v.Value);
        }
    }
}
