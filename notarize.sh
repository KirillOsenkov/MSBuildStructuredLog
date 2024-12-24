#!/bin/bash

# xcrun notarytool store-credentials "notarytool-password" --apple-id "<appleid>" --team-id XXXXXXXXXX --password xxxxxxxxxxxxxxxxxxxxxxxxxx

xcrun notarytool submit StructuredLogViewer-arm64.zip --keychain-profile notarytool-password --wait
xcrun notarytool submit StructuredLogViewer-x64.zip --keychain-profile notarytool-password --wait
