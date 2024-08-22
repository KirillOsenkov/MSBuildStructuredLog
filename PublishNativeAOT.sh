#!/bin/sh
unameOut="$(uname -s)"
case "${unameOut}" in
    Darwin*)    rid=osx-arm64;;
    Linux*)     rid=linux-x64;;
    *)
                echo "Unknown OS"
                exit 1
                ;;
esac
dotnet publish -c Release -r ${rid} -p:PublishNativeAot=True src/StructuredLogViewer.Avalonia/StructuredLogViewer.Avalonia.csproj
