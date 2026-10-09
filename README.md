# SignalScheduler

**Send Later for Signal on macOS.**

Schedule Signal messages and images for a later time from a small desktop companion app. SignalScheduler does not replace or modify Signal Desktop.

Scheduled content stays in a local encrypted queue and is sent through a linked [`signal-cli`](https://github.com/AsamK/signal-cli) device. Account linking is handled through an in-app QR code.

The packaged app includes `signal-cli` and Java, so users do not need to install Homebrew, Java or .NET separately.

Built with C#, .NET 8 and Avalonia.

## Features

- Schedule text messages and images for a later time.
- Send to phone numbers, full Signal usernames or username links.
- Connect a new account through an in-app QR code.
- Use bundled `signal-cli` and Java without separate runtime setup; detect existing linked accounts automatically.
- Edit Pending message text, cancel Pending messages, reschedule Missed messages as new copies, and reuse saved messages.
- Keep scheduled messages in a local AES-256-GCM encrypted queue, with the key stored separately in macOS Keychain.
- Attach images from disk or paste a screenshot directly from the macOS clipboard.
- Recover conservatively after interrupted sends without automatic retries; Sent records successful CLI completion, not recipient delivery.

## Architecture

```mermaid
flowchart LR
    Window["MainWindow"] --> VM["MainWindowViewModel"]

    VM --> Dispatcher["MessageDispatcher"]
    VM --> Store["EncryptedQueueStore"]

    Dispatcher --> Store
    Dispatcher --> Sender["ISignalSender"]

    Config["ConfiguredSignalSender"] -. implements .-> Sender
    Config --> Adapter["SignalCliAdapter"]
    Adapter --> Runner["ProcessRunner"]
    Runner --> CLI["signal-cli"]

    Store --> Cipher["QueueCipher"]
    Keychain["macOS Keychain"] --> Cipher
```

`MainWindowViewModel` coordinates UI state and the scheduler tick. `MessageDispatcher` owns the delivery rules and depends only on `ISignalSender`, keeping Signal-specific process handling outside the scheduling logic.

Queue snapshots are encrypted before they reach disk. The encryption key is stored separately in macOS Keychain.

Before a send starts, the message is persisted as `Sending`. If the application is interrupted at that point, the message becomes `Unknown` on the next launch instead of being sent again automatically. This avoids turning an uncertain send into an accidental duplicate.

## Requirements

To run the packaged app: macOS and Signal on a primary phone for linking.
Homebrew, system Java and .NET are not required to run it.
The current beta targets Apple Silicon; Intel has not been validated.

To build from source: macOS, .NET 8 SDK, Python 3 and Internet access for the
first download of the pinned runtime dependencies. With Homebrew:

```bash
brew install --cask dotnet-sdk@8
```

## Build and run

```bash
git clone https://github.com/Alexandra-St/SignalScheduler.git
cd SignalScheduler

bash build-mac.sh
open "build/Signal Scheduler.app"
```

The build bundles pinned signal-cli and Temurin Java, verifies checksums, signs
locally and smoke-tests a relocated app against an isolated empty configuration.
It uses the private runtime; development runs outside the .app search PATH and
Homebrew locations. A custom executable can be selected in Settings.

Existing linked accounts are reused without relinking. For a new account, click
**Connect Signal**, then scan and approve on your phone through
**Signal → Settings → Linked Devices → Link New Device**.

To create a local DMG:

```bash
python3 packaging/distribute.py
```

The image is written under `build/distribution/`. Drag the app into Applications,
eject the DMG, then launch from Applications. To update, quit the app first and
choose **Replace**; keep your queue, Keychain key and Signal account data.
The current Apple Silicon beta is ad hoc signed and not notarized; macOS Gatekeeper
may require a one-time app-specific Terminal workaround. First-launch guidance is in the
[draft beta notes](docs/BETA_RELEASE.md). Developer ID/notarization are not
requirements for this beta.

Version and build number come from `Version.props` for .NET, Info.plist and DMG
naming. Increment the build number before each new test/distribution build and
increment the version for a released update. See [packaging](docs/PACKAGING.md).

The app must remain running and the Mac must stay awake until scheduled messages are sent.

## Tests

```bash
dotnet test Tests/SignalScheduler.Tests.csproj -c Release
python3 -m unittest discover -s packaging -p 'test_*.py'
```

The test suite covers queue compatibility and encryption, scheduling and send-state transitions, `signal-cli` integration boundaries, executable discovery and account selection. Tests use synthetic data and do not send real Signal messages. Headless UI
checks exercise bindings, Send at validation and QR cancellation/retry.
[Acceptance results](docs/BETA_ACCEPTANCE.md) separately record manual installation,
phone-approved QR linking and real delivery on the development Mac and a fresh
Apple Silicon macOS VM. The VM first launch required the app-specific quarantine
command in the [installation instructions](docs/BETA_RELEASE.md#install).

## Privacy and limitations

Scheduled messages, attachments and metadata stay on the local machine in an encrypted queue. SignalScheduler has no application backend or telemetry.

A few important caveats:

- Signal integration is provided through the unofficial `signal-cli` project. SignalScheduler is not affiliated with or endorsed by Signal.
- `Sent` means that `signal-cli` accepted the request; it does not mean the recipient received or read the message.
- There are no automatic retries after an uncertain send.
- The app cannot wake a sleeping Mac or send while it is closed.
- This version is macOS-only.

More details about the current security boundaries are available in [`SECURITY.md`](SECURITY.md).

## License

SignalScheduler is available under the [MIT License](LICENSE).

Bundled `signal-cli` is a separate GPL-3.0-or-later project; Java and its dependencies
retain their own licenses. See [third-party notices](THIRD_PARTY_NOTICES.md).

Technical source/license coverage review passed for the dependency set retained
through the current candidate, 0.3.0 build 29; see [source review](docs/SOURCE_REVIEW.md).
`python3 packaging/prepare_beta_sources.py` collects matching source archives,
Maven/Rust source assets and notices under ignored `build/beta-materials/`.
Fresh collection manifests start as REVIEW_REQUIRED; collection alone is not review.
Before release, regenerate the final matching source-material archive and SHA-256
after the final approved test/documentation commits, and attach them alongside the
matching DMG. Upstream links alone do not replace those materials.
