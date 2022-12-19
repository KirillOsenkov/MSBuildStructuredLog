using Avalonia.Platform.Storage;

namespace StructuredLogViewer.Avalonia;

public class FileTypes
{
    public static string BinlogDefaultExtension = ".binlog";

    public static FilePickerFileType Binlog { get; } =
        new("Binary Log (*.binlog, *.buildlog)")
        {
            Patterns = new[] { "*.binlog", "*.buildlog" },
            MimeTypes = new[] { "application/octet-stream" },
            AppleUniformTypeIdentifiers = new []{ "public.data" }
        };

    public static FilePickerFileType Xml { get; } =
        new("XML (*.xml)")
        {
            Patterns = new[] { "*.xml" },
            MimeTypes = new[] { "application/xml", "text/xml" },
            AppleUniformTypeIdentifiers = new[] { "public.xml" }
        };

    public static FilePickerFileType MsBuildProj { get; } =
        new("MsBuild project file (*proj)")
        {
            Patterns = new[] { "*proj" },
            MimeTypes = new[] { "application/xml", "text/xml" },
            AppleUniformTypeIdentifiers = new[] { "public.xml" }
        };

    public static FilePickerFileType Sln { get; } =
        new("Solution File (*.sln)")
        {
            Patterns = new[] { "*.sln" },
            MimeTypes = new[] { "text/plain" },
            AppleUniformTypeIdentifiers = new[] { "public.text" }
        };

    public static FilePickerFileType Exe { get; } =
        new("Executable (*.exe)")
        {
            Patterns = new[] { "*.exe" },
            MimeTypes = new[] { "application/octet-stream" },
            AppleUniformTypeIdentifiers = new []{ "public.data", "public.executable", "public.windows-executable" }
        };

    public static FilePickerFileType Dll { get; } =
        new("Dynamic library (*.dll)")
        {
            Patterns = new[] { "*.dll" },
            MimeTypes = new[] { "application/octet-stream" },
            AppleUniformTypeIdentifiers = new []{ "public.data" }
        };
}
