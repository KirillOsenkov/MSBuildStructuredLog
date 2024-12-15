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
    System.IO.File.WriteAllBytes(tempP12File, p12Bytes);

    Information("Importing P12 certificate into the keychain...");

    var importArguments = new ProcessArgumentBuilder()
        .Append("import")
        .AppendQuoted(tempP12File)
        .Append("-k")
        .AppendQuoted("/Library/Keychains/System.keychain")
        .Append("-P")
        .AppendQuoted(p12Password);

    StartProcess("security", new ProcessSettings
    {
        Arguments = importArguments.RenderSafe(),
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });

    Information("Successfully imported the certificate.");

    System.IO.File.Delete(tempP12File);

    Information("Temporary P12 certificate file deleted.");
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
        var signingIdentity = "Developer ID Application";

        foreach (var dylib in GetFiles($"{appBundlePath}/**/*.dylib"))
        {
            Information($"Signing {dylib} library");

            // Sign the library:
            var args = new ProcessArgumentBuilder();
            args.Append("--force");
            args.Append("--sign");
            args.AppendQuoted(signingIdentity);
            args.AppendQuoted(dylib.ToString());

            StartProcess("codesign", new ProcessSettings
            {
                Arguments = args.RenderSafe(),
                RedirectStandardOutput = true,
                RedirectStandardError = true
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

            StartProcess("codesign", new ProcessSettings
            {
                Arguments = args.RenderSafe(),
                RedirectStandardOutput = true,
                RedirectStandardError = true
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

        // "Ditto" is absolutely necessary instead of "zip" command. Otherwise no symlinks are
        StartProcess("ditto", new ProcessSettings
        {
            Arguments = args.RenderSafe(),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
    }
});

Task("Package-Mac")
    .IsDependentOn("Create-Bundle")
    .IsDependentOn("Sign-Bundle")
    .IsDependentOn("Compress-Bundle");

 Task("Default")
     .IsDependentOn("Restore-NetCore")
     .IsDependentOn("Publish-NetCore")
     .IsDependentOn("Package-Mac");

 RunTarget(target);
