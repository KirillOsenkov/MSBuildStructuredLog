#!/usr/bin/env bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "${DIR}/src/StructuredLogViewer.Avalonia" || exit
dotnet run StructuredLogViewer.Avalonia.csproj
