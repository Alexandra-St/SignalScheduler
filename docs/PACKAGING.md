# Bundled runtime and QR onboarding

## Implemented

- Host-specific self-contained .NET bundle plus JVM signal-cli 0.14.9 and Temurin JRE 25.0.4.1+1.
- Official pinned artifact URLs and SHA-256 values in `packaging/dependencies.lock.json`.
- arm64 and x64 JRE selections; this implementation was exercised on arm64 macOS.
- Build-time downloads only. Runtime performs no component installation or downloading.
- A launcher using only the private Java runtime, independent of JAVA_HOME/PATH and inherited JVM options.
- Automatic mode prefers the packaged runtime. A broken packaged runtime fails visibly rather than silently executing a system fallback. Settings can still select a custom executable.
- Default signal-cli user-data location is unchanged. No queue, Keychain entry, linked account or private keys are migrated, deleted or placed in the app bundle.
- Upstream runtime legal files, signal-cli GPL text and third-party notices included in Resources.

## Automated checks

```bash
python3 -m unittest discover -s packaging -p 'test_*.py'
dotnet test Tests/SignalScheduler.Tests.csproj -c Release
bash build-mac.sh
```

`build-mac.sh` runs `packaging/verify_bundle.py` after signing. The smoke check verifies
ad hoc signing, relocates the app into a temporary path containing spaces, clears
the normal development environment, supplies deliberately invalid inherited Java
settings, and verifies the pinned CLI version and an empty JSON account list under
an explicit temporary `--config` directory. It never queries the real account or sends.
The temporary relocated copy preserves macOS signing attributes with ditto; runtime data
and any JNI extraction remain separate and are discarded after the check.

## Before public distribution

- Build and test on a clean Mac without Homebrew, Java or .NET; verify GUI startup,
  bundled account linking, text/attachments, sleep/restart recovery, and upgrades.
- Verify an existing linked device is recognized without relinking or changing its data.
- Validate Intel binaries on Intel hardware (or a suitable separately verified environment).
- Complete corresponding-source distribution materials for bundled covered components and
  dependencies; upstream links alone are not the release's compliance mechanism.
- Fix the known Send at validation/feedback bug and replace the placeholder icon.
- Current beta DMGs are ad hoc signed and not notarized. Developer ID signing and
  notarization are optional future work, not requirements for the initial public beta.

## Updating components

Choose and test fixed upstream versions; review their platform/runtime requirements;
update every artifact URL and SHA-256, license text/provenance and notices together.
Re-run packaging tests, application tests and relocated-bundle checks. Do not update
components independently of a tested application release in this initial implementation.

## QR onboarding checks

The welcome screen appears only after a successful account lookup returns no accounts.
Discovery errors retain a recovery status; they do not suggest relinking an existing device.
Linking runs `link -n "Signal Scheduler"`, streams the link URI into a locally generated
QR image, and never retains subprocess diagnostics or the URI. Cancel, window close and
a three-minute timeout stop the process and remove the image. Account discovery runs
after each attempt to reconcile approval that races cancellation. Existing accounts
continue using the original data directory without automatic linking.

Synthetic subprocess and headless UI tests exercise early QR display, successful account
selection, cancellation, timeout, retry, window closing, failure recovery and existing
accounts. The operator reported real phone approval and text/photo/image-only
delivery on the DMG-installed development Mac on 2026-10-07; see
`docs/BETA_ACCEPTANCE.md`. Clean-Mac checks remain separate and pending.

## Create an installation image

After `bash build-mac.sh`, run:

```bash
python3 packaging/distribute.py
```

The local image is written under `build/distribution/` with `local` in its name,
a SHA-256 sidecar, the app, an Applications shortcut and short installation instructions.
The image is compressed/read-only. Checks verify its integrity, mount it read-only,
verify the app signature and exercise its private CLI against temporary empty account data.
This does not install or replace the user's existing app or query their account.

## Optional future Developer ID workflow (prepared, not yet exercised)

Requires a valid **Developer ID Application** certificate and its private key on the
build Mac, plus a notarytool Keychain profile configured locally. Do not put account
passwords, API keys, certificates or private keys in the repository. The bundle ID
must be a stable identifier owned by the publisher; `local.SignalScheduler` is only
for development.

```bash
python3 packaging/distribute.py --release \
  --identity 'Developer ID Application: Publisher (TEAMID)' \
  --notary-profile SignalScheduler-notary \
  --bundle-id com.publisher.SignalScheduler
```

The script works on a copy of the development app. It signs JNI libraries embedded
in JARs before rewriting those JARs, then signs native binaries, Java and the outer
app in order. Already Java-signed JARs are rejected rather than invalidated. The .NET
host allows JIT; Java additionally allows unsigned executable memory for its VM.
Debugging entitlements are excluded. Runtime compatibility of these entitlements
requires real signed-build verification.

It submits the app archive, requires an Accepted response, staples and validates
the app ticket, creates and signs the DMG, submits that image, staples its ticket,
and runs Gatekeeper checks. Reports stay in ignored build output. No upload occurs
in the default local mode. Missing release prerequisites fail before packaging.

Official references: [Apple notarization](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
and [.NET macOS deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos).
A successful notarization is separate from the manual clean-Mac, phone-linking,
Intel and source-distribution requirements listed above.

## Beta source-material collection

Run `python3 packaging/prepare_beta_sources.py` after building. This downloads the
version-matched signal-cli, Temurin, Temurin build scripts, libsignal and Signal Service
source archives; collects Maven source JARs/POMs for the actual bundled JAR inventory;
collects Rust registry archives with Cargo.lock checksums and pinned Git sources;
and copies Java legal materials and cached NuGet notices. Assets and SHA-256 values
are recorded under ignored `build/beta-materials/manifest.json`. The script is scoped
to the dependency versions pinned in this beta; changes require reviewing its source
URLs and build provenance together with the binary lock.

The manifest deliberately remains `REVIEW_REQUIRED`: fetched source archives do not
prove complete corresponding source. In particular review native submodules/build
prerequisites such as BoringSSL, inherited Maven licenses and NuGet notice coverage.
Do not describe this collection as complete or publish the matching DMG until those
items are resolved. The release draft and live test record are in `docs/BETA_RELEASE.md`
and `docs/BETA_ACCEPTANCE.md`.

## Version policy

`Version.props` supplies the .NET application version and build number.
`packaging/write_app_info.py` uses the same values for macOS Info.plist, and the
DMG filename reads version/build from that plist. Increment BuildNumber before
each new test/distribution build; increment Version for each released update.
This policy is enforced by project instructions and the release checklist;
automatic build-number allocation is not implemented. The current icon build is 0.2.1,
build 3, distinct from the previously verified 0.2.0 artifact.

## Application icon

The user-supplied artwork is kept unchanged at `Assets/app-icon.png`.
`packaging/build_icon.py` uses macOS sips/iconutil to generate all standard
16–1024 pixel representations into Resources/SignalScheduler.icns before signing.
Info.plist names that resource; the main Avalonia window uses the same PNG.
Bundle verification checks the icon resource. Finder/Dock/search appearance must
still be confirmed manually after installing the new build.
