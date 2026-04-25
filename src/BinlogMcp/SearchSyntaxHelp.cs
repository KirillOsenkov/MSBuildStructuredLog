using System;
using System.IO;

namespace BinlogMcp;

/// <summary>
/// Loads the search-query syntax reference from the embedded
/// <c>SearchSyntax.md</c> resource. Cached after first read.
/// </summary>
internal static class SearchSyntaxHelp
{
    private const string ResourceName = "SearchSyntax.md";

    private static string text;

    public static string Text
    {
        get
        {
            var local = text;
            if (local != null)
            {
                return local;
            }

            var assembly = typeof(SearchSyntaxHelp).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");
            using var reader = new StreamReader(stream);
            local = reader.ReadToEnd();
            text = local;
            return local;
        }
    }
}
