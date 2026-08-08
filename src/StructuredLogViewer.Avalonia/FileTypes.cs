using Avalonia.Platform.Storage;

namespace StructuredLogViewer.Avalonia;

public class FileTypes
{
    public static string BinlogDefaultExtension = ".binlog";

    public static FilePickerFileType Binlog { get; } =
        new("Binary Log")
        {
            Patterns = new[] { "*.binlog", "*.buildlog" },
            MimeTypes = new[] { "application/binlog", "application/buildlog" },
            AppleUniformTypeIdentifiers = new []{ "public.data" }
        };

    public static FilePickerFileType MsBuildProj { get; } =
        new("MsBuild project file")
        {
            // match any *proj extension like the WPF viewer (csproj, vbproj, esproj, sqlproj, ...)
            Patterns = new[] { "*.proj", "*.*proj" },
            MimeTypes = new[] { "application/xml", "text/xml" },
            AppleUniformTypeIdentifiers = new[] { "public.xml" }
        };

    public static FilePickerFileType Sln { get; } =
        new("Solution File")
        {
            // no *.slnf - OpenFile doesn't support building solution filters
            Patterns = new[] { "*.sln", "*.slnx"},
            MimeTypes = new[] { "text/plain" },
            AppleUniformTypeIdentifiers = new[] { "public.text" }
        };

    public static FilePickerFileType Exe { get; } =
        new("Executable")
        {
            Patterns = new[] { "*.exe", "*.dll" },
            MimeTypes = new[] { "application/octet-stream" },
            AppleUniformTypeIdentifiers = new []{ "public.data", "public.executable", "public.windows-executable" }
        };
}
