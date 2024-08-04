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

    public static FilePickerFileType Xml { get; } =
        new("XML")
        {
            Patterns = new[] { "*.xml" },
            MimeTypes = new[] { "application/xml", "text/xml" },
            AppleUniformTypeIdentifiers = new[] { "public.xml" }
        };

    public static FilePickerFileType MsBuildProj { get; } =
        new("MsBuild project file")
        {
            Patterns = new[] { "*.csproj", "*.fsproj", "*.vbproj", "*.shproj", "*.wapproj", "*.vcxproj", "*.vcproj", "*.msbuildproj" },
            MimeTypes = new[] { "application/xml", "text/xml" },
            AppleUniformTypeIdentifiers = new[] { "public.xml" }
        };

    public static FilePickerFileType Sln { get; } =
        new("Solution File")
        {
            Patterns = new[] { "*.sln", "*.slnf" },
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
