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
