#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
case "$(uname -m)" in arm64) rid=osx-arm64; bundle_arch=arm64;; x86_64) rid=osx-x64; bundle_arch=x64;; *) echo 'Run on macOS'; exit 1;; esac
[ "$(uname -s)" = Darwin ] || { echo 'Run on macOS'; exit 1; }
command -v dotnet >/dev/null || { echo 'Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0'; exit 1; }
dotnet publish -c Release -r "$rid" --self-contained true -o build/publish
app='build/Signal Scheduler.app'
mkdir -p "$app/Contents/MacOS"
cp -R build/publish/. "$app/Contents/MacOS/"
python3 packaging/bundle_dependencies.py --resources "$app/Contents/Resources" --architecture "$bundle_arch"
python3 packaging/collect_runtime_notices.py --resources "$app/Contents/Resources"
python3 packaging/build_icon.py "$app/Contents/Resources/SignalScheduler.icns"
python3 packaging/write_app_info.py "$app/Contents/Info.plist"
codesign --force --deep --sign - "$app"
python3 packaging/verify_bundle.py "$app"
echo "Built: $app"
