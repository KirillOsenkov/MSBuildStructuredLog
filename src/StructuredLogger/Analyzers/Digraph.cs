using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger;

public class Vertex
{
    public string Value { get; set; }
    public string Title { get; set; }

    internal HashSet<Vertex> outgoing;
    public IReadOnlyCollection<Vertex> Outgoing
    {
        get
        {
            if (outgoing == null)
            {
                return Array.Empty<Vertex>();
            }

            return outgoing;
        }
    }

    internal HashSet<Vertex> incoming;
    public IReadOnlyCollection<Vertex> Incoming
    {
        get
        {
            if (incoming == null)
            {
                return Array.Empty<Vertex>();
            }

            return incoming;
        }
    }

    internal HashSet<Vertex> transitiveOutgoing;
    public IReadOnlyCollection<Vertex> TransitiveOutgoing
    {
        get
        {
            if (transitiveOutgoing == null)
            {
                return Array.Empty<Vertex>();
            }

            return transitiveOutgoing;
        }
    }

    public IEnumerable<Vertex> NonRedundantOutgoing
    {
        get
        {
            if (outgoing == null)
            {
                return Array.Empty<Vertex>();
            }

            if (transitiveOutgoing == null)
            {
                return Outgoing;
            }

            return Outgoing.Except(TransitiveOutgoing).ToArray();
        }
    }

    public int InDegree => incoming?.Count ?? 0;
    public int OutDegree => outgoing?.Count ?? 0;

    public int Height { get; set; } = -1;
    public int Depth { get; set; } = -1;

    public void AddChild(Vertex destination)
    {
        outgoing ??= new();
        outgoing.Add(destination);

        destination.incoming ??= new();
        destination.incoming.Add(this);
    }
}

public class Digraph
{
    private Dictionary<string, Vertex> vertices;

    public IEnumerable<Vertex> Vertices => vertices.Values;
    public IEnumerable<Vertex> Sources => vertices.Values.Where(v => v.InDegree == 0).ToArray();
    public IEnumerable<Vertex> Sinks => vertices.Values.Where(v => v.OutDegree == 0).ToArray();

    private Digraph(Dictionary<string, Vertex> vertices)
    {
        this.vertices = vertices;
    }

    public Digraph()
    {
        vertices = new(StringComparer.OrdinalIgnoreCase);
    }

    public void Add(Vertex vertex)
    {
        vertices[vertex.Value] = vertex;
    }

    public Vertex GetOrCreate(string value, Func<string, string> titleGetter)
    {
        if (TryFindVertex(value) is not { } node)
        {
            node = new Vertex { Value = value };
            node.Title = titleGetter(value);
            Add(node);
        }

        return node;
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
                vertex = new Vertex() { Value = s, Title = s };
                vertices[s] = vertex;
            }

            return vertex;
        }

        return new Digraph(vertices);
    }

    public int MaxHeight => vertices.Count == 0 ? 0 : Vertices.Max(v => v.Height);

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

        int height = 0;

        foreach (var outgoing in vertex.Outgoing)
        {
            CalculateHeight(outgoing);
            int outgoingHeight = outgoing.Height + 1;
            if (outgoingHeight > height)
            {
                height = outgoingHeight;
            }
        }

        vertex.Height = height;
    }

    private void CalculateDepth(Vertex vertex)
    {
        if (vertex.Depth > -1)
        {
            return;
        }

        int depth = 0;

        if (vertex.InDegree > 0)
        {
            foreach (var incoming in vertex.Incoming)
            {
                CalculateDepth(incoming);
                int incomingDepth = incoming.Depth + 1;
                if (incomingDepth > depth)
                {
                    depth = incomingDepth;
                }
            }
        }

        vertex.Depth = depth;
    }

    public void ComputeTransitiveReduction()
    {
        var all = vertices.Values;
        var originalEdges = all.ToDictionary(v => v, v => new HashSet<Vertex>(v.Outgoing));

        var visited = new HashSet<Vertex>();
        var stack = new Stack<Vertex>();

        foreach (var source in all)
        {
            foreach (var destination in originalEdges[source].ToList())
            {
                source.outgoing.Remove(destination);

                if (CanReach(source, destination, visited, stack))
                {
                    source.transitiveOutgoing ??= new();
                    source.transitiveOutgoing.Add(destination);
                }

                source.outgoing.Add(destination);

                visited.Clear();
                stack.Clear();
            }
        }
    }

    public bool CanReach(Vertex start, Vertex end)
    {
        if (start.Height <= end.Height)
        {
            return false;
        }

        var visited = new HashSet<Vertex>();
        var stack = new Stack<Vertex>();

        bool result = CanReach(start, end, visited, stack);

        return result;
    }

    public void FindAllPaths(Vertex start, Vertex end, Action<IReadOnlyList<Vertex>> callback)
    {
        if (start.Height <= end.Height)
        {
            return;
        }

        var visited = new HashSet<Vertex>();
        var spine = new Stack<Vertex>();

        FindPaths(start, end, visited, spine, callback);
    }

    private static void FindPaths(
        Vertex start,
        Vertex target,
        HashSet<Vertex> visited,
        Stack<Vertex> spine,
        Action<IReadOnlyList<Vertex>> callback)
    {
        if (start == target)
        {
            callback(spine.ToArray());
            return;
        }

        if (!visited.Add(start))
        {
            return;
        }

        spine.Push(start);

        foreach (var child in start.Outgoing)
        {
            FindPaths(child, target, visited, spine, callback);
        }

        spine.Pop();
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

            foreach (var child in current.Outgoing)
            {
                if (!visited.Contains(child))
                {
                    stack.Push(child);
                }
            }
        }

        return false;
    }

    public IEnumerable<IEnumerable<Vertex>> RemoveCycles()
    {
        var stack = new List<Vertex>();
        var stackSet = new HashSet<Vertex>();
        var visited = new HashSet<Vertex>();

        var cycles = new List<IEnumerable<Vertex>>();

        foreach (var vertex in vertices.Values)
        {
            Visit(vertex);
        }

        return cycles;

        void Visit(Vertex vertex)
        {
            if (vertex.OutDegree == 0 || !visited.Add(vertex))
            {
                return;
            }

            stack.Add(vertex);
            stackSet.Add(vertex);

            foreach (var outgoing in vertex.Outgoing.ToArray())
            {
                if (stackSet.Contains(outgoing))
                {
                    vertex.outgoing.Remove(outgoing);
                    outgoing.incoming.Remove(vertex);
                    cycles.Add(stack.ToArray());
                    continue;
                }

                Visit(outgoing);
            }

            stack.RemoveAt(stack.Count - 1);
            stackSet.Remove(vertex);
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

        var nodes = vertices.Values.OrderBy(s => s.Title).ToArray();
        for (int i = 0; i < nodes.Length; i++)
        {
            dic[nodes[i]] = i;
            writer.WriteLine($"{i} {nodes[i].Title}");
        }

        writer.WriteLine("#");

        foreach (var vertex in nodes)
        {
            if (vertex.OutDegree > 0)
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

        var nodes = vertices.Values.OrderBy(s => s.Title).ToArray();
        for (int i = 0; i < nodes.Length; i++)
        {
            dic[nodes[i]] = i;
            writer.WriteLine($"node {nodes[i].Title}");
        }

        writer.WriteLine("#");

        foreach (var vertex in nodes)
        {
            if (vertex.OutDegree > 0)
            {
                foreach (var child in vertex.Outgoing)
                {
                    writer.WriteLine($"edge {vertex.Title} {child.Title}");
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
        foreach (var project in vertices.Values.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (project.OutDegree == 0)
            {
                continue;
            }

            var projectKey = project.Title;
            if (!quotes)
            {
                projectKey = projectKey.TrimQuotes();
            }

            foreach (var reference in project.Outgoing.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase))
            {
                var referenceKey = reference.Title;
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
}