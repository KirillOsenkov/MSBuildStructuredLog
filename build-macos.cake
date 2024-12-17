#addin "Cake.Plist"

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

var version = EnvironmentVariable("APPVEYOR_BUILD_VERSION") ?? Argument("version", "0.0.1");

var artifactsDir = (DirectoryPath)Directory("./artifacts");

var netCoreAppsRoot= "./src";
var netCoreApp = "StructuredLogViewer.Avalonia";
var macAppName = "StructuredLogViewer";

var buildDirs = 
    GetDirectories($"{netCoreAppsRoot}/**/bin/**") + 
    GetDirectories($"{netCoreAppsRoot}/**/obj/**") + 
    GetDirectories($"{netCoreAppsRoot}/artifacts/*");

var netCoreProject = new {
        Path = $"{netCoreAppsRoot}/{netCoreApp}",
        Name = netCoreApp,
        Framework = XmlPeek($"{netCoreAppsRoot}/{netCoreApp}/{netCoreApp}.csproj", "//*[local-name()='TargetFramework']/text()"),
        Runtimes = XmlPeek($"{netCoreAppsRoot}/{netCoreApp}/{netCoreApp}.csproj", "//*[local-name()='RuntimeIdentifiers']/text()").Split(';')
    };


var certIsSet = !string.IsNullOrEmpty(EnvironmentVariable("P12_BASE64"));
var certNameIsSet = !string.IsNullOrEmpty(EnvironmentVariable("APPLE_CERT_NAME"));

var keychainPath = "app-signing.keychain";
var keychainPassword = Guid.NewGuid().ToString("N");

 Task("Clean")
 .Does(()=>{
     CleanDirectories(buildDirs);
 });

 Task("Restore-NetCore")
     .IsDependentOn("Clean")
     .Does(() =>
 {
    DotNetRestore(netCoreProject.Path);
 });

 Task("Build-NetCore")
     .IsDependentOn("Restore-NetCore")
     .Does(() =>
 {
    Information("Building: {0}", netCoreProject.Name);
    DotNetBuild(netCoreProject.Path, new DotNetBuildSettings {
        Configuration = configuration
    });
 });

 Task("Publish-NetCore")
     .IsDependentOn("Restore-NetCore")
     .Does(() =>
 {
    foreach(var runtime in netCoreProject.Runtimes)
    {
        if (!runtime.Contains("osx"))
            continue;

        var outputDir = artifactsDir.Combine(runtime);

        Information("Publishing: {0}, runtime: {1}", netCoreProject.Name, runtime);
        var settings = new DotNetPublishSettings
        {
            //Framework = netCoreProject.Framework,
            Configuration = configuration,
            Runtime = runtime,
            SelfContained = true,
            PublishSingleFile = true,
            OutputDirectory = outputDir.FullPath
        };
        if (configuration == "Release")
        {
            settings.MSBuildSettings = new DotNetMSBuildSettings()
                .WithProperty("DebugType", "None")
                .WithProperty("DebugSymbols", "false")
                .WithProperty("IncludeSymbolsInSingleFile", "false")
                .WithProperty("CopyOutputSymbolsToPublishDirectory", "false");
        }

        DotNetPublish(netCoreProject.Path, settings);
    }
 });

Task("Install-Certificate")
    .WithCriteria(certIsSet)
    .Does(() =>
{
    var p12Base64 = EnvironmentVariable("P12_BASE64")?.Replace("\r", "").Replace("\n", "");
    var p12Password = EnvironmentVariable("P12_PASSWORD");

    if (string.IsNullOrEmpty(p12Base64))
    {
        Error("P12_BASE64 environment variable is not set.");
        return;
    }

    if (string.IsNullOrEmpty(p12Password))
    {
        Error("P12_PASSWORD environment variable is not set.");
        return;
    }

    Information("Decoding the P12 certificate from base64...");

    var p12Bytes = Convert.FromBase64String(p12Base64);
    var tempP12File = "./certificate.p12";

    try
    {
        System.IO.File.WriteAllBytes(tempP12File, p12Bytes);

        Information("Creating temporary keychain...");
        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("create-keychain")
                .Append("-p")
                .AppendQuoted(keychainPassword)
                .AppendQuoted(keychainPath)
        });

        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("set-keychain-settings")
                .Append("-lut")
                .Append("21600")
                .AppendQuoted(keychainPath)
        });

        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("unlock-keychain")
                .Append("-p")
                .AppendQuoted(keychainPassword)
                .AppendQuoted(keychainPath)
        });

        DownloadAndInstallAppleCert("https://www.apple.com/certificateauthority/DeveloperIDG2CA.cer");
        DownloadAndInstallAppleCert("https://www.apple.com/certificateauthority/AppleRootCA-G2.cer");
        DownloadAndInstallAppleCert("https://www.apple.com/certificateauthority/AppleWWDRCAG2.cer");

        Information("Importing certificate to keychain...");
        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("import")
                .AppendQuoted(tempP12File)
                .Append("-P")
                .AppendQuoted(p12Password)
                .Append("-t")
                .Append("cert")
                // -A allows access for this cert for any app on the machine
                // But -T only limits it to specific apps (codesign and security here)
                // That's what Apple does for GitHub Actions:
                // https://github.com/Apple-Actions/import-codesign-certs/blob/63fff01cd422d4b7b855d40ca1e9d34d2de9427d/src/security.ts#L109-L113
                .Append("-A")
                .Append("-T")
                .Append("/usr/bin/codesign")
                .Append("-T")
                .Append("/usr/bin/security")
                .Append("-f")
                .Append("pkcs12")
                .Append("-k")
                .AppendQuoted(keychainPath)
        });

        Information("Set keychain partition");
        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("set-key-partition-list")
                .Append("-S")
                .Append("apple-tool:,apple:")
                .Append("-k")
                .AppendQuoted(keychainPassword)
                .AppendQuoted(keychainPath)
        });

        Information("Update keychains visibility");
        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("list-keychains")
                .Append("-d")
                .Append("user")
                .Append("-s")
                .AppendQuoted(keychainPath)
                .Append("login.keychain")
        });

        Information("Successfully imported the certificate.");
    }
    finally
    {
        System.IO.File.Delete(tempP12File);

        Information("Temporary P12 certificate file deleted.");
    }

    void DownloadAndInstallAppleCert(string cert)
    {
        var fileName = System.IO.Path.GetFileName(cert);
        RunToolWithOutput("curl", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("-o")
                .AppendQuoted(fileName)
                .AppendQuoted(cert)
        });
        RunToolWithOutput("security", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("import")
                .AppendQuoted(fileName)
                .Append("-T")
                .Append("/usr/bin/codesign")
                .Append("-T")
                .Append("/usr/bin/security")
                .Append("-k")
                .AppendQuoted(keychainPath)
        });
    }
});

 Task("Create-Bundle")
     .IsDependentOn("Publish-NetCore")
     .Does(() =>
 {
    var runtimeIdentifiers = netCoreProject.Runtimes.Where(r => r.StartsWith("osx"));
    foreach(var runtime in runtimeIdentifiers)
    {
        var workingDir = artifactsDir.Combine(runtime);
        var tempDir = artifactsDir.Combine("app");

        Information("Copying Info.plist");
        EnsureDirectoryExists(tempDir.Combine("Contents"));
        CopyFileToDirectory($"{netCoreAppsRoot}/{netCoreApp}/Info.plist", tempDir.Combine("Contents"));

        // Update versions in Info.plist
        var plistFile = tempDir.Combine("Contents").CombineWithFilePath("Info.plist");
        dynamic plist = DeserializePlist(plistFile);
        plist["CFBundleShortVersionString"] = version;
        plist["CFBundleVersion"] = version;
        SerializePlist(plistFile, plist);

        Information("Copying App Icons");
        EnsureDirectoryExists(tempDir.Combine("Contents/Resources"));
        CopyFileToDirectory($"{netCoreAppsRoot}/{netCoreApp}/StructuredLogViewer.icns", tempDir.Combine("Contents/Resources"));

        EnsureDirectoryExists(tempDir.Combine("Contents/Frameworks"));

        Information("Copying executables");
        MoveDirectory(workingDir, tempDir.Combine("Contents/MacOS"));

        Information("Finish packaging");
        EnsureDirectoryExists(workingDir);
        MoveDirectory(tempDir, workingDir.Combine($"{macAppName}.app"));
    }
 });

 Task("Sign-Bundle")
    .WithCriteria(certNameIsSet)
    .IsDependentOn("Install-Certificate")
    .IsDependentOn("Create-Bundle")
    .Does(() =>
{
    var runtimeIdentifiers = netCoreProject.Runtimes.Where(r => r.StartsWith("osx"));
    var entitlementsFilePath = $"{netCoreAppsRoot}/{netCoreApp}/Entitlements.plist";

    foreach(var runtime in runtimeIdentifiers)
    {
        Information($"Starting {runtime} macOS app signing");

        var appBundlePath = artifactsDir.Combine(runtime).Combine($"{macAppName}.app");
        var signingIdentity = EnvironmentVariable("APPLE_CERT_NAME");

        foreach (var dylib in GetFiles($"{appBundlePath}/**/*.dylib"))
        {
            Information($"Signing {dylib} library");

            // Sign the library:
            var args = new ProcessArgumentBuilder();
            args.Append("--force");
            args.Append("--sign");
            args.AppendQuoted(signingIdentity);
            args.AppendQuoted(dylib.ToString());

            RunToolWithOutput("codesign", new ProcessSettings
            {
                Arguments = args.RenderSafe()
            });
        }

        // Sign the bundle:
        {
            Information($"Signing {appBundlePath} bundle");

            var args = new ProcessArgumentBuilder();
            args.Append("--force");
            args.Append("--timestamp");
            args.Append("--options=runtime");
            args.Append("--entitlements");
            args.AppendQuoted(entitlementsFilePath);
            args.Append("--sign");
            args.AppendQuoted(signingIdentity);
            args.AppendQuoted(appBundlePath.ToString());

            RunToolWithOutput("codesign",new ProcessSettings
            {
                Arguments = args.RenderSafe()
            });
        }
    }
});

Task("Compress-Bundle")
    .IsDependentOn("Sign-Bundle")
    .Does(() =>
{
    var runtimeIdentifiers = netCoreProject.Runtimes.Where(r => r.StartsWith("osx"));
    foreach(var runtime in runtimeIdentifiers)
    {
        Information($"Compressing {runtime} macOS bundle");
        var appBundlePath = artifactsDir.Combine(runtime).Combine($"{macAppName}.app");
        var architecture = runtime[(runtime.IndexOf("-")+1)..];
        var zippedPath = artifactsDir.Combine(runtime).CombineWithFilePath($"../{macAppName}-{architecture}.zip");

        var args = new ProcessArgumentBuilder();
        args.Append("-c");
        args.Append("-k");
        args.Append("--keepParent");
        args.AppendQuoted(appBundlePath.ToString());
        args.AppendQuoted(zippedPath.ToString());

        // "Ditto" is absolutely necessary instead of "zip" command.
        // Otherwise no symlinks are saved, and notarize process would fail.
        RunToolWithOutput("ditto", new ProcessSettings
        {
            Arguments = args.RenderSafe()
        });
    }
});

Task("Cleanup-After-Sign")
    .WithCriteria(certIsSet)
    .IsDependentOn("Sign-Bundle")
    .Does(() =>
{
    RunToolWithOutput("security", new ProcessSettings
    {
        Arguments = new ProcessArgumentBuilder()
            .Append("delete-keychain")
            .AppendQuoted(keychainPath)
    });
});

Task("Package-Mac")
    .IsDependentOn("Create-Bundle")
    .IsDependentOn("Sign-Bundle")
    .IsDependentOn("Cleanup-After-Sign")
    .IsDependentOn("Compress-Bundle");

 Task("Default")
     .IsDependentOn("Restore-NetCore")
     .IsDependentOn("Publish-NetCore")
     .IsDependentOn("Package-Mac");

 RunTarget(target);


void RunToolWithOutput(
    string toolName, ProcessSettings settings,
    bool includeStandardOutput = false,
    bool includeStandardError = true)
{
    settings.RedirectStandardOutput = includeStandardOutput;
    if (includeStandardOutput)
    {
        settings.RedirectedStandardOutputHandler = line =>
        {
            if (line is not null)
                Information(line);
            return line;
        };
    }
    settings.RedirectStandardError = includeStandardError;
    if (includeStandardError)
    {
        settings.RedirectedStandardErrorHandler = line =>
        {
            if (line is not null)
                Error(line);
            return line;
        };
    }
    var exitCode = StartProcess(toolName, settings, out var lines);
    if (exitCode != 0)
    {
        throw new Exception($"The tool exited with code {exitCode}");
    }
}