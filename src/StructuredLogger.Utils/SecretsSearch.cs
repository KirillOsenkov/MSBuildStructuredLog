using System;
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
        private readonly Dictionary<string, Dictionary<SensitiveDataKind, List<SecretDescriptor>>> _secretDescriptorCache = new();
        private readonly Dictionary<string, IEnumerable<SearchResult>> _searchResultCache = new();

        private readonly Dictionary<SensitiveDataKind, ISensitiveDataDetector> _secretDetectors = new()
        {
            { SensitiveDataKind.CommonSecrets, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.CommonSecrets, false) },
            { SensitiveDataKind.ExplicitSecrets, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.ExplicitSecrets, false) },
            { SensitiveDataKind.Username, SensitiveDataDetectorFactory.GetSecretsDetector(SensitiveDataKind.Username, false) }
        };

        public SecretsSearch(Build build)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _search = new Search([build], build.StringTable.Instances, 5000, markResultsInTree: false);
        }

        public bool TryGetResults(NodeQueryMatcher nodeQueryMatcher, IList<SearchResult> resultCollector, int maxResults)
        {
            if (!string.Equals(nodeQueryMatcher.TypeKeyword, SecretKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var activeDetectors = GetActiveDetectors(nodeQueryMatcher.NotMatchers);
            var allResults = new List<SearchResult>();
            foreach (var stringInstance in _build.StringTable.Instances)
            {
                if (_searchResultCache.TryGetValue(stringInstance, out IEnumerable<SearchResult> cachedSearchResults))
                {
                    allResults.AddRange(cachedSearchResults);
                }
                else
                {
                    allResults.AddRange(GetSearchResult(stringInstance, activeDetectors));
                }
            }

            if (allResults.Any())
            {
                resultCollector.AddRange(allResults.Take(maxResults));
            }

            return true;
        }

        public List<SecretDescriptor> SearchSecrets(string stringInstance, NodeQueryMatcher? nodeQueryMatcher, int maxResults)
        {
            var detectors = GetActiveDetectors(nodeQueryMatcher == null ? [] : [nodeQueryMatcher]);

            if (_secretDescriptorCache.TryGetValue(stringInstance, out var cachedSecrets))
            {
                return cachedSecrets.Where(cS => detectors.Keys.Contains(cS.Key)).SelectMany(cr => cr.Value).Take(maxResults).ToList();
            }

            return GetSecrets(stringInstance, detectors);
        }

        private List<SecretDescriptor> GetSecrets(string stringInstance, Dictionary<SensitiveDataKind, ISensitiveDataDetector> activeDetectors)
        {
            if (_secretDescriptorCache.TryGetValue(stringInstance, out var cachedSensitiveData) && cachedSensitiveData.Count > 0)
            {
                return cachedSensitiveData.SelectMany(csd => csd.Value).ToList();
            }

            var sensitiveData = new Dictionary<SensitiveDataKind, List<SecretDescriptor>>();
            foreach (var detector in activeDetectors)
            {
                Dictionary<SensitiveDataKind, List<SecretDescriptor>> detectedData = detector.Value.Detect(stringInstance);
                foreach (var kvp in detectedData)
                {
                    if (kvp.Value.Count > 0)
                    {
                        sensitiveData[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (sensitiveData.Count > 0)
            {
                _secretDescriptorCache.Add(stringInstance, sensitiveData);
            }

            return sensitiveData.SelectMany(csd => csd.Value).ToList();
        }

        private IEnumerable<SearchResult> GetSearchResult(string stringInstance, Dictionary<SensitiveDataKind, ISensitiveDataDetector> activeDetectors)
        {
            List<SearchResult> results = new();

            if (GetSecrets(stringInstance, activeDetectors).Count > 0)
            {
                var nodes = _search.FindNodes(stringInstance, CancellationToken.None);
                if (nodes.Any())
                {
                    results.AddRange(nodes);
                    _searchResultCache.Add(stringInstance, nodes);
                }
            }

            return results;
        }

        private Dictionary<SensitiveDataKind, ISensitiveDataDetector> GetActiveDetectors(IList<NodeQueryMatcher> notMatchers)
        {
            if (!notMatchers.Any())
            {
                return _secretDetectors;
            }

            var detectors = new Dictionary<SensitiveDataKind, ISensitiveDataDetector>(_secretDetectors);
            foreach (var notMatcher in notMatchers)
            {
                if (Enum.TryParse(notMatcher.Query, ignoreCase: true, out SensitiveDataKind excludedDetector))
                {
                    detectors.Remove(excludedDetector);
                }
            }

            return detectors;
        }
    }
}
