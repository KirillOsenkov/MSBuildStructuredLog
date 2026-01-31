#!/usr/bin/env bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
dotnet run --project "${DIR}/src/StructuredLogViewer.Avalonia" -- "$@"
