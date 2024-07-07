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
        DotNetPublish(netCoreProject.Path, new DotNetPublishSettings {
            //Framework = netCoreProject.Framework,
            Configuration = configuration,
            Runtime = runtime,
            SelfContained = true,
            OutputDirectory = outputDir.FullPath
        });
    }
 });


 Task("Package-Mac")
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
        
        Information("Copying executables");
        MoveDirectory(workingDir, tempDir.Combine("Contents/MacOS"));

        Information("Finish packaging");
        EnsureDirectoryExists(workingDir);
        MoveDirectory(tempDir, workingDir.Combine($"{macAppName}.app"));

        var architecture = runtime[(runtime.IndexOf("-")+1)..];
        Zip(workingDir.FullPath, workingDir.CombineWithFilePath($"../{macAppName}-{architecture}.zip"));
    }
 });

 Task("Default")
     .IsDependentOn("Restore-NetCore")
     .IsDependentOn("Publish-NetCore")
     .IsDependentOn("Package-Mac");

 RunTarget(target);
