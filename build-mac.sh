#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
case "$(uname -m)" in arm64) rid=osx-arm64;; x86_64) rid=osx-x64;; *) echo 'Run on macOS'; exit 1;; esac
[ "$(uname -s)" = Darwin ] || { echo 'Run on macOS'; exit 1; }
command -v dotnet >/dev/null || { echo 'Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0'; exit 1; }
dotnet publish -c Release -r "$rid" --self-contained true -o build/publish
app='build/Signal Scheduler.app'
mkdir -p "$app/Contents/MacOS"
cp -R build/publish/. "$app/Contents/MacOS/"
cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>SignalScheduler</string>
<key>CFBundleIdentifier</key><string>local.SignalScheduler</string>
<key>CFBundleName</key><string>Signal Scheduler</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleShortVersionString</key><string>0.2.0</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
codesign --force --deep --sign - "$app"
echo "Built: $app"
