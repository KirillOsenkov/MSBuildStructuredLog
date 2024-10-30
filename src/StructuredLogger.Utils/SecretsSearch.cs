using System;
using System.Collections.Generic;
using System.Linq;
using DotUtils.MsBuild.SensitiveDataDetector;
using Microsoft.Build.SensitiveDataDetector;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SecretsSearch : ISearchExtension
    {
        private readonly Build _build;
        private readonly Search _search;
        private readonly Dictionary<SensitiveDataKind, ISensitiveDataDetector> _detectors;
        private readonly Dictionary<string, Dictionary<SensitiveDataKind, List<SecretDescriptor>>> _secretCache = new();
        private readonly Dictionary<string, List<SearchResult>> _resultCache = new();

        public SecretsSearch(Build build)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _search = new Search([build], build.StringTable.Instances, 5000, markResultsInTree: false);

            _detectors = new()
            {
                { SensitiveDataKind.CommonSecrets, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.CommonSecrets, false) },
                { SensitiveDataKind.ExplicitSecrets, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.ExplicitSecrets, false) },
                { SensitiveDataKind.Username, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.Username, false) }
            };
        }

        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> results, int maxResults)
        {
            if (!string.Equals(matcher.TypeKeyword, "secret", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var activeDetectors = GetActiveDetectors(matcher.NotMatchers);
            var foundResults = ScanForSecrets(_build.StringTable.Instances, activeDetectors, maxResults);

            if (foundResults.Any())
            {
                foreach (var result in foundResults)
                {
                    results.Add(result);
                }

                return true;
            }

            return false;
        }

        public List<SecretDescriptor> SearchSecrets(string text, NodeQueryMatcher? matcher, int maxResults)
        {
            var activeDetectors = GetActiveDetectors(matcher == null ? [] : [matcher]);

            if (_secretCache.TryGetValue(text, out var cachedSecrets))
            {
                return cachedSecrets.Where(s => activeDetectors.ContainsKey(s.Key)).SelectMany(s => s.Value).Take(maxResults).ToList();
            }

            var secrets = DetectSecrets(text, activeDetectors);
            if (secrets.Any())
            {
                _secretCache[text] = secrets;
            }

            return secrets.Values.SelectMany(v => v).Take(maxResults).ToList();
        }

        private IEnumerable<SearchResult> ScanForSecrets(IEnumerable<string> stringsPool, IReadOnlyDictionary<SensitiveDataKind, ISensitiveDataDetector> detectors, int maxResults)
        {
            var results = new List<SearchResult>();

            foreach (var text in stringsPool)
            {
                if (_resultCache.TryGetValue(text, out List<SearchResult> cachedResults))
                {
                    results.AddRange(cachedResults);
                    if (results.Count >= maxResults)
                    {
                        break;
                    }

                    continue;
                }

                var secrets = DetectSecrets(text, detectors);

                if (secrets.Any())
                {
                    var nodes = _search.FindNodes(text, default);
                    if (nodes.Any())
                    {
                        _resultCache[text] = nodes.ToList();
                        results.AddRange(nodes);
                        if (results.Count >= maxResults)
                        {
                            break;
                        }
                    }
                }
            }

            return results.Take(maxResults);
        }

        private Dictionary<SensitiveDataKind, List<SecretDescriptor>> DetectSecrets(string text, IReadOnlyDictionary<SensitiveDataKind, ISensitiveDataDetector> detectors)
        {
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

            return results;
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
