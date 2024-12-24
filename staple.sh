#!/bin/bash

unzip StructuredLogViewer-x64.zip
xcrun stapler staple StructuredLogViewer.app
rm StructuredLogViewer-x64.zip
zip -r StructuredLogViewer-x64.zip StructuredLogViewer.app
rm -r StructuredLogViewer.app

unzip StructuredLogViewer-arm64.zip
xcrun stapler staple StructuredLogViewer.app
rm StructuredLogViewer-arm64.zip
zip -r StructuredLogViewer-arm64.zip StructuredLogViewer.app
rm -r StructuredLogViewer.app

