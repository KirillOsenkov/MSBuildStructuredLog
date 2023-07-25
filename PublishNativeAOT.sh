#!/bin/sh
dotnet publish -c Release -r linux-x64 -p:PublishNativeAot=True src/StructuredLogViewer.Avalonia/StructuredLogViewer.Avalonia.csproj
strip bin/StructuredLogViewer.Avalonia/Release/net7.0/linux-x64/publish/StructuredLogViewer.Avalonia