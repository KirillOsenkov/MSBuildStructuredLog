using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DotUtils.MsBuild.SensitiveDataDetector;
using Microsoft.Build.SensitiveDataDetector;
using NuGet.Packaging;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SecretsSearch : ISearchExtension
    {
        private readonly Search _search;
        private readonly Build _build;
        private const string SecretKeyword = "secret";
        private readonly ConcurrentDictionary<string, List<SecretDescriptor>> _cache = new ConcurrentDictionary<string, List<SecretDescriptor>>();

        private readonly List<ISensitiveDataDetector> SecretsDetectors = new List<ISensitiveDataDetector>
        {
            SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.CommonSecrets, false),
            SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.ExplicitSecrets, false),
            SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.Username, false)
        };

        public SecretsSearch(Build build)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));

            _search = new Search(
                new[] { build },
                build.StringTable.Instances,
                5000,
                markResultsInTree: false);
        }

        public bool TryGetResults(NodeQueryMatcher nodeQueryMatcher, IList<SearchResult> resultCollector, int maxResults)
        {
            if (!string.Equals(nodeQueryMatcher.TypeKeyword, SecretKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var stringInstance in _build.StringTable.Instances)
            {
                if (_cache.TryGetValue(stringInstance, out List<SecretDescriptor> cachedSensitiveData) && cachedSensitiveData.Count > 0)
                {
                    IEnumerable<SearchResult> nodes = _search.FindNodes(stringInstance, CancellationToken.None);
                    resultCollector.AddRange(nodes.Take(maxResults).Select(n => CreateSearchResult(n, cachedSensitiveData.First().Secret)));
                }
                else
                {
                    resultCollector.AddRange(GetSearchResult(stringInstance).Take(maxResults)); 
                }
            }

            return true;
        }

        public List<SecretDescriptor> GetSecrets(string stringInstance)
        {
            if (_cache.TryGetValue(stringInstance, out var cachedSensitiveData) && cachedSensitiveData.Count > 0)
            {
                return cachedSensitiveData;
            }

            var sensitiveData = new List<SecretDescriptor>();
            foreach (ISensitiveDataDetector detector in SecretsDetectors)
            {
                Dictionary<SensitiveDataKind, List<SecretDescriptor>> detectedData = detector.Detect(stringInstance);
                if (detectedData.Count > 0 && detectedData.First().Value.Count > 0)
                {
                    sensitiveData.AddRange(detectedData.First().Value);
                }
            }

            _cache.TryAdd(stringInstance, sensitiveData);

            return sensitiveData;
        }

        private List<SearchResult> GetSearchResult(string stringInstance)
        {
            List<SearchResult> results = new List<SearchResult>();
            List<SecretDescriptor> sensitiveData = GetSecrets(stringInstance);

            if (sensitiveData.Count > 0)
            {
                IEnumerable<SearchResult> nodes = _search.FindNodes(stringInstance, CancellationToken.None);
                results.AddRange(nodes.Select(n => CreateSearchResult(n, sensitiveData.First().Secret)));
            }

            return results;
        }

        private SearchResult CreateSearchResult(SearchResult nodeMatch, string secretText)
        {
            var proxy = new ProxyNode
            {
                Original = nodeMatch.Node,
                SearchResult = nodeMatch,
                Text = secretText
            };
            return new SearchResult(proxy);
        }
    }
}
