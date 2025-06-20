using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger;

public class Vertex
{
    public string Value;
    public string Key;
    public HashSet<Vertex> Outgoing;
    public HashSet<Vertex> Incoming;
    public bool IsReferenced;
    public bool WasProcessed;
    public bool IsProcessing;

    public int Height { get; set; } = -1;
    public int Depth { get; set; } = -1;

    public int InDegree => Incoming?.Count ?? 0;
    public int OutDegree => Outgoing?.Count ?? 0;

    public void AddChild(Vertex destination)
    {
        Outgoing ??= new();
        Outgoing.Add(destination);
        destination.IsReferenced = true;

        destination.Incoming ??= new();
        destination.Incoming.Add(this);
    }
}

public class Digraph
{
    private Dictionary<string, Vertex> vertices;

    public IEnumerable<Vertex> Vertices => vertices.Values;

    public IEnumerable<Vertex> Sources => vertices.Values.Where(v => !v.IsReferenced).ToArray();

    private Digraph(Dictionary<string, Vertex> vertices)
    {
        this.vertices = vertices;
    }

    public Digraph()
    {
        vertices = new();
    }

    public void Add(Vertex vertex)
    {
        vertices[vertex.Value] = vertex;
    }

    public Vertex TryFindVertex(string value)
    {
        vertices.TryGetValue(value, out var result);
        return result;
    }

    public static Digraph Load(string filePath)
    {
        var vertices = new Dictionary<string, Vertex>(StringComparer.OrdinalIgnoreCase);

        var edges = ReadEdges(filePath);

        foreach (var edge in edges)
        {
            var source = GetVertex(edge.source);
            var destination = GetVertex(edge.destination);
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
            parent.Outgoing.Remove(project);
        }

        if (project.Outgoing != null && project.Outgoing.Count > 0)
        {
            chain.Add(project);

            foreach (var reference in project.Outgoing.ToArray())
            {
                RemoveTransitiveEdges(reference, chain);
            }

            chain.RemoveAt(chain.Count - 1);
        }
    }

    public void CalculateHeight()
    {
        foreach (var vertex in vertices.Values)
        {
            CalculateHeight(vertex);
        }
    }

    public void CalculateDepth()
    {
        foreach (var vertex in vertices.Values)
        {
            CalculateDepth(vertex);
        }
    }

    private void CalculateHeight(Vertex vertex)
    {
        if (vertex.Height > -1)
        {
            return;
        }

        int outdegree = 0;

        if (vertex.Outgoing != null && vertex.Outgoing.Count > 0)
        {
            foreach (var outgoing in vertex.Outgoing)
            {
                CalculateHeight(outgoing);
                int outgoingHeight = outgoing.Height + 1;
                if (outgoingHeight > outdegree)
                {
                    outdegree = outgoingHeight;
                }
            }
        }

        vertex.Height = outdegree;
    }

    private void CalculateDepth(Vertex vertex)
    {
        if (vertex.Depth > -1)
        {
            return;
        }

        int indegree = 0;

        if (vertex.Incoming != null && vertex.Incoming.Count > 0)
        {
            foreach (var incoming in vertex.Incoming)
            {
                CalculateDepth(incoming);
                int incomingHeight = incoming.Depth + 1;
                if (incomingHeight > indegree)
                {
                    indegree = incomingHeight;
                }
            }
        }

        vertex.Depth = indegree;
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
            if (vertex.Outgoing != null && vertex.Outgoing.Count > 0)
            {
                foreach (var child in vertex.Outgoing)
                {
                    writer.WriteLine($"{dic[vertex]} {dic[child]}");
                }
            }
        }

        return writer.ToString();
    }

    public string GetElkText()
    {
        var writer = new System.IO.StringWriter();

        var dic = new Dictionary<Vertex, int>();

        var nodes = vertices.Values.ToArray();
        for (int i = 0; i < nodes.Length; i++)
        {
            dic[nodes[i]] = i;
            writer.WriteLine($"node {nodes[i].Key}");
        }

        writer.WriteLine("#");

        foreach (var vertex in nodes)
        {
            if (vertex.Outgoing != null && vertex.Outgoing.Count > 0)
            {
                foreach (var child in vertex.Outgoing)
                {
                    writer.WriteLine($"edge {vertex.Key} {child.Key}");
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
            if (project.Outgoing == null || project.Outgoing.Count == 0)
            {
                continue;
            }

            var projectKey = project.Key;
            if (!quotes)
            {
                projectKey = projectKey.TrimQuotes();
            }

            foreach (var reference in project.Outgoing.OrderBy(r => r.Value, StringComparer.OrdinalIgnoreCase))
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
        var originalEdges = all.ToDictionary(v => v, v => new HashSet<Vertex>(v.Outgoing ?? Enumerable.Empty<Vertex>()));

        var visited = new HashSet<Vertex>();
        var stack = new Stack<Vertex>();

        foreach (var source in all)
        {
            foreach (var destination in originalEdges[source].ToList())
            {
                source.Outgoing.Remove(destination);

                if (!CanReach(source, destination, visited, stack))
                {
                    source.Outgoing.Add(destination);
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

            if (current.Outgoing != null)
            {
                foreach (var child in current.Outgoing)
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

    public void Cleanup()
    {
        foreach (var vertex in vertices.ToArray())
        {
            if (ShouldDelete(vertex.Key))
            {
                vertices.Remove(vertex.Key);
            }

            if (vertex.Value.Outgoing != null)
            {
                foreach (var child in vertex.Value.Outgoing.ToArray())
                {
                    if (ShouldDelete(child.Key))
                    {
                        vertex.Value.Outgoing.Remove(child);
                    }
                }
            }
        }
    }

    public bool ShouldDelete(string key)
    {
        if (key == "dirs.proj" ||
            key == "\"dirs.proj\"" ||
            key.EndsWith("\\dirs.proj\"") ||
            key.EndsWith("\\dirs.proj"))
        {
            return true;
        }

        if (key == "restore.proj" || key == "\"restore.proj\"")
        {
            return true;
        }

        return false;
    }
}