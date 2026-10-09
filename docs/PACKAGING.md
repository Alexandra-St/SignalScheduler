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

- Build 29 passed fresh Apple Silicon macOS VM installation, GUI startup, account
  linking and scheduled text delivery with the documented quarantine workaround.
  A separate physical Mac, physical sleep/wake and clean-environment upgrades
  remain distinct checks; see BETA_ACCEPTANCE.md for the evidence boundaries.
- Existing-account recognition and data preservation passed on the development Mac;
  historical Replace results apply to their recorded builds.
- Intel is unvalidated and is not a target of this Apple Silicon beta.
- Technical source/notice coverage passed for the unchanged build 29 dependency set.
  Regenerate the final matching source-material archive and SHA-256 after final
  approved test/documentation commits, and attach them beside the matching DMG.
- Send at feedback, custom icon and native build 29 layout/rendering were accepted;
  native review does not replace production or Gatekeeper verification.
- Current beta DMGs are ad hoc signed and not notarized. Developer ID signing and
  notarization and Apple Developer membership are outside the beta plan.

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
`docs/BETA_ACCEPTANCE.md`. Later build 29 clean VM results are recorded separately there.

## Create an installation image

After `bash build-mac.sh`, run:

```bash
python3 packaging/distribute.py
```

The local image is written under `build/distribution/` with `local` in its name,
a SHA-256 sidecar, the app and an Applications shortcut. The installer uses a
code-rendered dark background, a drag arrow and an embedded short instruction;
Install.txt is no longer visible.
The image is compressed/read-only. Checks verify its integrity, mount it read-only,
verify the app signature and exercise its private CLI against temporary empty account data.
This does not install or replace the user's existing app or query their account.

The packaging step uses a temporary writable image, generates a 600×360-point
Retina background with Swift/AppKit, and saves the icon layout and 600-point-wide
Finder window using packaging/layout_dmg.applescript. It then detaches the image
and converts it to compressed read-only format. It requires macOS Finder and
permission for osascript to automate Finder; a missing saved .DS_Store fails
packaging rather than producing an unstyled installer. The mounted image is
always detached on layout failure. No global Finder preferences are changed.
The .background directory and .DS_Store are hidden. App is placed at (160,170),
Applications at (440,170), with 96-point icons. Installation/update and Gatekeeper
instructions remain in BETA_TESTER.md and BETA_RELEASE.md.

## Optional future Developer ID workflow (prepared, not yet exercised)

This existing workflow is outside the agreed beta plan. Revisit it only if the
owner explicitly changes that decision; it is not a remaining beta task.

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

The collection script deliberately emits `REVIEW_REQUIRED`: fetching archives does
not itself prove complete corresponding source. A fresh collection or changed
dependency set requires review of native submodules/build prerequisites, inherited
Maven licenses and NuGet notices. The current dependency set has already passed
technical source/notice coverage review; its provenance is unchanged from the
build 26 review baseline through build 29. See [SOURCE_REVIEW.md](SOURCE_REVIEW.md).

Before publication, regenerate the final matching archive and SHA-256 after the
final approved test/documentation commits using `python3 packaging/package_beta_sources.py`,
verify its source snapshot and attach it beside the matching DMG. Historical archive
checks do not validate later edits. Tag/publication require separate approval.

## Version policy

`Version.props` supplies the .NET application version and build number.
`packaging/write_app_info.py` uses the same values for macOS Info.plist, and the
DMG filename reads version/build from that plist. Increment BuildNumber before
each new test/distribution build; increment Version for each released update.
This policy is enforced by project instructions and the release checklist;
automatic build-number allocation is not implemented. Current candidate: 0.3.0
build 29 with the styled installer. Earlier acceptance records retain their original version/build.

## Application icon

The user-supplied artwork is kept unchanged at `Assets/app-icon.png`.
`packaging/build_icon.py` uses macOS sips/iconutil to generate all standard
16–1024 pixel representations into Resources/SignalScheduler.icns before signing.
Info.plist names that resource; the main Avalonia window uses the same PNG.
Bundle verification checks the icon resource. Finder/Dock/search appearance was confirmed on the development Mac.
Build 29 clean VM installation is recorded separately in BETA_ACCEPTANCE.md.
