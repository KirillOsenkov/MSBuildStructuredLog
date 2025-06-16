using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger;

public class Vertex
{
    public string Value;
    public string Key;
    public HashSet<Vertex> Children;
    public bool IsReferenced;
    public bool WasProcessed;
    public bool IsProcessing;

    public void AddChild(Vertex destination)
    {
        Children ??= new();
        Children.Add(destination);
    }
}

public class Digraph
{
    private Dictionary<string, Vertex> vertices;

    private Digraph(Dictionary<string, Vertex> vertices)
    {
        this.vertices = vertices;
    }

    public static Digraph Load(string filePath)
    {
        var vertices = new Dictionary<string, Vertex>(StringComparer.OrdinalIgnoreCase);

        var edges = ReadEdges(filePath);

        foreach (var edge in edges)
        {
            var source = GetVertex(edge.source);
            var destination = GetVertex(edge.destination);
            destination.IsReferenced = true;

            source.AddChild(destination);
        }

        Vertex GetVertex(string s)
        {
            if (!vertices.TryGetValue(s, out var vertex))
            {
                vertex = new Vertex() { Value = s, Key = s };
                vertices[s] = vertex;
            }

            return vertex;
        }

        return new Digraph(vertices);
    }

    public void RemoveTransitiveEdges()
    {
        var unreferenced = vertices.Values.Where(n => !n.IsReferenced).ToArray();

        var nodeList = new List<Vertex>();
        foreach (var key in unreferenced)
        {
            RemoveTransitiveEdges(key, nodeList);
        }
    }

    private void RemoveTransitiveEdges(Vertex project, List<Vertex> chain)
    {
        for (int i = 0; i < chain.Count - 1; i++)
        {
            var parent = chain[i];
            parent.Children.Remove(project);
        }

        if (project.Children != null && project.Children.Count > 0)
        {
            chain.Add(project);

            foreach (var reference in project.Children.ToArray())
            {
                RemoveTransitiveEdges(reference, chain);
            }

            chain.RemoveAt(chain.Count - 1);
        }
    }

    public string GetDotText()
    {
        var writer = new System.IO.StringWriter();
        writer.WriteLine("digraph {");
        WriteVertices(writer);
        writer.WriteLine("}");
        return writer.ToString();
    }

    public string GetMermaidText()
    {
        var writer = new System.IO.StringWriter();
        writer.WriteLine("graph RL");
        WriteVertices(writer, arrow: "-->", quotes: false);
        return writer.ToString();
    }

    public string GetTgfText()
    {
        var writer = new System.IO.StringWriter();

        var dic = new Dictionary<Vertex, int>();

        var nodes = vertices.Values.ToArray();
        for (int i = 0; i < nodes.Length; i++)
        {
            dic[nodes[i]] = i;
            writer.WriteLine($"{i} {nodes[i].Key}");
        }

        writer.WriteLine("#");

        foreach (var vertex in nodes)
        {
            if (vertex.Children != null && vertex.Children.Count > 0)
            {
                foreach (var child in vertex.Children)
                {
                    writer.WriteLine($"{dic[vertex]} {dic[child]}");
                }
            }
        }
        
        return writer.ToString();
    }

    private void WriteVertices(
        TextWriter writer,
        string arrow = "->",
        string indent = "  ",
        bool quotes = true)
    {
        foreach (var project in vertices.Values.OrderBy(r => r.Value, StringComparer.OrdinalIgnoreCase))
        {
            if (project.Children == null || project.Children.Count == 0)
            {
                continue;
            }

            var projectKey = project.Key;
            if (!quotes)
            {
                projectKey = projectKey.TrimQuotes();
            }

            foreach (var reference in project.Children.OrderBy(r => r.Value, StringComparer.OrdinalIgnoreCase))
            {
                var referenceKey = reference.Key;
                if (!quotes)
                {
                    referenceKey = referenceKey.TrimQuotes();
                }

                writer.WriteLine($"{indent}{projectKey} {arrow} {referenceKey}");
            }
        }
    }

    private static IEnumerable<(string source, string destination)> ReadEdges(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (i == 0 && (line.StartsWith("digraph") || line.StartsWith("graph")))
            {
                continue;
            }

            line = line.Trim();

            if (i == line.Length - 1)
            {
                if (line == "}")
                {
                    continue;
                }
            }

            int arrow = line.IndexOf("-->");
            int arrowLength = 3;
            if (arrow == -1)
            {
                arrow = line.IndexOf("->");
                arrowLength = 2;
            }

            if (arrow == -1)
            {
                continue;
            }

            var left = line.Substring(0, arrow).Trim();
            var right = line.Substring(arrow + arrowLength).Trim();

            yield return (left, right);
        }
    }

    public void TransitiveReduction()
    {
        var all = vertices.Values;
        var originalEdges = all.ToDictionary(v => v, v => new HashSet<Vertex>(v.Children ?? Enumerable.Empty<Vertex>()));

        var visited = new HashSet<Vertex>();
        var stack = new Stack<Vertex>();

        foreach (var source in all)
        {
            foreach (var destination in originalEdges[source].ToList())
            {
                source.Children.Remove(destination);

                if (!CanReach(source, destination, visited, stack))
                {
                    source.Children.Add(destination);
                }

                visited.Clear();
                stack.Clear();
            }
        }
    }

    private static bool CanReach(Vertex start, Vertex target, HashSet<Vertex> visited, Stack<Vertex> stack)
    {
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == target)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (current.Children != null)
            {
                foreach (var child in current.Children)
                {
                    if (!visited.Contains(child))
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        return false;
    }
}