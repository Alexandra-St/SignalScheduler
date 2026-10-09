# Bundled third-party components

SignalScheduler source code is licensed under MIT. Independently executed third-party
components retain their own licenses; inclusion does not relicense those components.

## signal-cli 0.14.9

Copyright its respective contributors. GNU GPL version 3 or later.
The unmodified upstream distribution, including LICENSE, remains under
`Contents/Resources/signal-cli/`. Its dependency JARs retain their embedded notices.
Upstream source and build instructions: https://github.com/AsamK/signal-cli/tree/v0.14.9
Exact downloaded binary URL and SHA-256 are in `dependencies.lock.json`.

## Eclipse Temurin Java 25.0.4.1+1

Copyright its respective contributors. GNU GPL version 2 with the Classpath Exception,
plus the third-party licenses included in its `legal` directory.
The unmodified runtime retains its license/notice files under
`Contents/Resources/jre/Contents/Home/`.
Upstream release, source metadata and build provenance:
https://github.com/adoptium/temurin25-binaries/releases/tag/jdk-25.0.4.1%2B1

## Distribution preparation

These local development bundles are not a public release. Before distributing binaries,
assemble and publish the complete corresponding source materials required by each
component's licenses, including covered dependencies and build materials, alongside
the release. Links to upstream repositories alone are not a substitute for that step.
Dependency updates must update the lock file, notices and source materials together.

## QRCoder

QRCoder 1.6.0 generates device-link QR images locally in memory.
Copyright (c) 2013-2018 Raffael Herrmann. Licensed under MIT; the exact upstream
license is included as `Contents/Resources/QRCoder-LICENSE.txt`.
Source: https://github.com/codebude/QRCoder/tree/bd980577640c47f8bb881cf24c8443415a579d36

## .NET and presentation dependencies

Every bundle includes `Contents/Resources/ThirdPartyLicenses`: the exact .NET
runtime LICENSE and THIRD-PARTY-NOTICES, NuGet package metadata and embedded
notices, upstream MIT license texts for Avalonia, MicroCom, Tmds.DBus and QRCoder,
and license/notice entries from the bundled Java JARs. `inventory.json` identifies
the runtime and package versions. This inventory includes restore dependencies
for other platforms as well; their inclusion does not mean those binaries ship
in the macOS app. Technical source/license coverage review passed for the dependency
set unchanged through 0.3.0 build 29; see [source review](docs/SOURCE_REVIEW.md).
Before release, regenerate the final matching source-material archive and SHA-256
after final approved test/documentation commits and attach them alongside the DMG.

Upstream supplemental texts:
- Avalonia: https://github.com/AvaloniaUI/Avalonia/blob/d6edb46ce04f983892a61d3abf906014d3f5ec8d/licence.md
- MicroCom: https://github.com/kekekeks/MicroCom/blob/master/LICENSE
- Tmds.DBus: https://github.com/tmds/Tmds.DBus/blob/f0b2c29f57bdc7efaeccaa61ca9f13e9a0eebff7/COPYING

The checked-in MicroCom license text is retained even if the upstream branch changes.
