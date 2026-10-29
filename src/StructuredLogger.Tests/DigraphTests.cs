using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class DigraphTests
    {
        [Fact]
        public void VerticesOnPathsIncludeEveryBranchAndNothingElse()
        {
            // A -> B -> D -> E and A -> C -> D -> E are the paths from A to E.
            // X is reachable from A but cannot reach E; Y reaches D but is not reachable from A.
            var graph = Digraph.Create(new[]
            {
                ("A", "B"), ("A", "C"), ("B", "D"), ("C", "D"), ("D", "E"), ("A", "X"), ("Y", "D")
            });

            var onPath = graph.FindVerticesOnPaths(graph.TryFindVertex("A"), graph.TryFindVertex("E"));

            Assert.Equal(new[] { "A", "B", "C", "D", "E" }, onPath.Select(v => v.Value).OrderBy(v => v).ToArray());
        }

        [Fact]
        public void VerticesOnPathsIsEmptyWhenEndIsUnreachable()
        {
            var graph = Digraph.Create(new[] { ("A", "B"), ("C", "D") });

            Assert.Empty(graph.FindVerticesOnPaths(graph.TryFindVertex("A"), graph.TryFindVertex("D")));
            Assert.Empty(graph.FindVerticesOnPaths(graph.TryFindVertex("B"), graph.TryFindVertex("A")));
        }

        [Fact]
        public void VerticesOnPathsForTheSameVertexIsThatVertex()
        {
            var graph = Digraph.Create(new[] { ("A", "B") });
            var a = graph.TryFindVertex("A");

            Assert.Equal(new[] { a }, graph.FindVerticesOnPaths(a, a).ToArray());
        }

        [Fact]
        public void VerticesOnPathsDoesNotEnumeratePaths()
        {
            // 12 layers of 8 vertices, fully connected between adjacent layers:
            // 8^11 distinct paths from the first layer to the last, far too many to enumerate.
            const int layers = 12;
            const int width = 8;
            var edges = new List<(string, string)>();
            for (int layer = 0; layer < layers - 1; layer++)
            {
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        edges.Add(($"L{layer}_{i}", $"L{layer + 1}_{j}"));
                    }
                }
            }

            var graph = Digraph.Create(edges);

            var onPath = graph.FindVerticesOnPaths(graph.TryFindVertex("L0_0"), graph.TryFindVertex($"L{layers - 1}_0"));

            Assert.Equal(1 + (layers - 2) * width + 1, onPath.Count);
        }
    }
}
