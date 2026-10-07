# SignalScheduler

A macOS companion for scheduling Signal messages and image attachments. Built with C# and Avalonia, it keeps the queue encrypted on the local machine and sends through a linked `signal-cli` device. Explicit message states and conservative recovery rules reduce the risk of duplicate or unexpectedly late sends.

## Problem

Scheduling a message should not require remembering to return to a conversation at the right time. SignalScheduler provides scheduled messaging for desktop Signal users through a separate composer and queue; it does not modify the official Signal Desktop interface.

## Screenshots & demo

- **Composer — placeholder:** recipient, message, image thumbnails and scheduled time.
- **Queue — placeholder:** pending, cancelled and completed messages.
- **Demo GIF — placeholder:** add a screenshot, schedule a message and observe its status change.

Public screenshots will use synthetic content and redact account and recipient identifiers.

## Features

- Schedule text and images to phone-number recipients.
- Detect the local linked account automatically; choose explicitly when several accounts exist.
- Select multiple photos or paste a screenshot from the macOS clipboard.
- Review image thumbnails and remove attachments before scheduling.
- Send images with or without a caption; up to 8 images and 20 MiB total per message.
- Cancel pending messages or copy an existing message into the composer.
- Persist message content, attachments and metadata in an AES-256-GCM encrypted queue.
- Store the encryption key in macOS Keychain.
- Recover interrupted sends as unknown rather than retrying automatically. Unknown or unsupported stored statuses also require manual review.
- Reject invalid or ambiguous daylight-saving times.
- Package a self-contained macOS application for Apple Silicon or Intel.

## Architecture

```mermaid
flowchart TD
    UI["Avalonia composer and queue"] --> Store["Encrypted local queue"]
    UI --> Worker["In-process scheduler"]
    Worker --> Store
    Keychain["macOS Keychain"] --> Store
    Worker --> CLI["signal-cli linked device"]
    CLI --> Signal["Signal service"]
```

The UI handles composition and queue actions. `EncryptedQueueStore` encrypts snapshots, flushes writes before replacement and holds an exclusive instance lock. The ViewModel drives a five-second dispatcher timer; `MessageDispatcher` selects due messages and sends them serially through `ISignalSender`. `SignalCliAdapter` implements the external Signal integration.

## Tech stack

| Area | Technology |
| --- | --- |
| Language / runtime | C# / .NET 8 |
| Desktop UI | Avalonia 11.3, Fluent theme |
| Persistence | JSON snapshots encrypted with AES-256-GCM |
| Key storage | macOS Keychain via `/usr/bin/security` |
| Signal integration | External `signal-cli` executable |
| Screenshot import | Native macOS clipboard via AppleScript |
| Packaging | Bash, `dotnet publish`, ad hoc code signing |

## How it works

1. Link `signal-cli` to an existing Signal account.
2. Compose a message, attach images if needed and select a future local time.
3. Save the message and its attachments to the encrypted queue.
4. When due, persist the `Sending` state before attempting the send.
5. Record acceptance by `signal-cli`, or flag an uncertain outcome for manual review.

Messages up to five minutes overdue are attempted when the app resumes. Older messages are marked missed and require manual rescheduling. A timeout, nonzero exit or interruption never triggers an automatic resend; check Signal before scheduling a copy.

`Sent` means `signal-cli` accepted the request, not that the recipient received or read it. Cancelled and completed messages remain in the encrypted queue until deleted.

## Getting started

### Requirements

- macOS; Apple Silicon or Intel.
- .NET 8 SDK to build the application.
- A working, current `signal-cli` installation.
- Signal on a primary mobile device for linking.

`signal-cli` is not bundled. Use its [installation documentation](https://github.com/AsamK/signal-cli) for platform requirements.

### Install dependencies

With Homebrew:

```bash
brew install --cask dotnet-sdk@8
brew install signal-cli

dotnet --list-sdks
signal-cli --version
```

### Link your account

```bash
signal-cli link -n "SignalScheduler"
```

Scan the terminal QR code from Signal on your phone: **Settings → Linked Devices → Link New Device**. Keep the command running until linking completes. Use device linking, not registration of your existing number.

If needed, inspect the linked account and synchronize it:

```bash
signal-cli listAccounts
signal-cli -a YOUR_ACCOUNT receive
```

Replace `YOUR_ACCOUNT` locally with your account identifier. Do not share QR codes, linking URIs or account output in issues. Do not run a separate `signal-cli` daemon against the same account while using this application.

### Build and launch

From the repository root:

```bash
bash build-mac.sh
open "build/Signal Scheduler.app"
```

The script detects the architecture, publishes a self-contained application and signs it ad hoc for local use. It does not produce a notarized distribution release.

### Schedule a message

1. The app finds and validates `signal-cli` automatically using PATH, then standard Homebrew locations. For a custom installation, open **Settings → signal-cli**, choose an executable, or return to **Use automatic detection**. The path is read-only; the version is shown after validation.
2. The linked account is detected on startup. If several accounts exist, choose one; after linking, click **Refresh accounts**. The **From** dropdown is the only account selector; scheduling stays disabled until a detected account is selected. Enter the recipient's international phone number.
3. Add text or images. To capture a screenshot to the clipboard, press **Control + Shift + Command + 4**, then click **Paste screenshot**.
4. Enter the local date and time as `yyyy-MM-dd HH:mm` and click **Schedule message**.
5. Keep the application open and the Mac awake with network access.

Start with a message to your own number and verify it in **Note to Self**. See [manual verification](docs/VERIFICATION.md) for recovery and attachment checks.

### Update

Quit the application, replace the source files and rebuild. Moving the source folder or rebuilding does not require relinking Signal. Preserve the existing queue and Keychain entry.

## Project structure

| Path | Responsibility |
| --- | --- |
| `Program.cs`, `App.cs` | Entry point and Avalonia application initialization |
| `Models/` | Scheduled messages, image attachments and strongly typed `MessageStatus` values |
| `Services/MessageDispatcher.cs` | Due-message selection, serial sending and outcome transitions |
| `Persistence/EncryptedQueueStore.cs` | Queue snapshots, compatible status-string conversion, file replacement, instance locking and restart recovery |
| `Integrations/Signal/` | Signal sender boundary and `signal-cli` adapter |
| `Security/` | AES-GCM queue envelope and macOS Keychain access |
| `Infrastructure/` | Subprocess execution, macOS application paths and clipboard image import |
| `Views/MainWindow.cs` | Controls, file picker, thumbnails and queue rendering |
| `ViewModels/MainWindowViewModel.cs` | Composer state, validation, queue actions and UI lifecycle coordination |
| `Tests/` | Queue compatibility, encryption, dispatcher and subprocess integration regression tests |
| `SignalScheduler.csproj` | Application runtime and Avalonia dependencies |
| `build-mac.sh` | Architecture detection, publishing and macOS bundle signing |
| `.gitignore`, `.editorconfig` | Runtime-data exclusions and shared source formatting settings |
| `docs/VERIFICATION.md` | Automated verification scope and manual macOS acceptance checks |
| `SECURITY.md`, `LICENSE` | Security boundaries and MIT license |

### Run regression tests

```bash
dotnet test Tests/SignalScheduler.Tests.csproj -c Release
```

Tests use synthetic data and a fake Signal sender or local subprocess. They do not link an account or send real messages. Filesystem and subprocess checks are intended for macOS and Linux; they do not exercise Keychain, native clipboard permissions or live Signal delivery.

## Security & privacy

Scheduled messages, attachment bytes and metadata are stored locally in an encrypted file under `~/Library/Application Support/SignalScheduler/`. A randomly generated 256-bit key is kept in macOS Keychain. File and directory permissions restrict queue access to the current user. There is no application backend or telemetry; sending still connects to the Signal service through `signal-cli`.

**Credentials, Signal account data, linking QR codes, device identifiers, local databases, logs and real conversations must never enter this repository.** The application does not include an account configuration or credentials. `signal-cli` manages its own account keys and storage outside the project.

Message text is passed on standard input, not shell command arguments. Image files are temporarily materialized in a private local directory for clipboard import or sending, then deleted; leftovers are cleaned on the next successful queue open. Deletion is not secure erasure, and image metadata is not stripped.

Encryption protects the queue at rest, not against code running as the unlocked macOS user. Account and recipient identifiers appear in subprocess arguments. Initial Keychain setup briefly passes the generated encryption key as an argument to `security`. See [SECURITY.md](SECURITY.md) for the current security boundaries.

## Roadmap

- Full-size attachment viewer.
- Date/time picker and quick scheduling presets.
- Contact selection and group recipients.
- Direct editing and rescheduling of pending messages.
- Extend automated coverage to composer validation and UI lifecycle behavior.
- Background execution, login startup and clearer sleep/offline handling.
- Explore scheduling inside a separately maintained Signal Desktop build.

## Limitations

- Uses **unofficial Signal integration** through `signal-cli`; not affiliated with or endorsed by Signal.
- macOS only in this version; Keychain and clipboard handling are platform-specific.
- Scheduling happens in this companion app, not inside the official Signal client.
- The app must remain running; it does not wake a sleeping Mac or send after being closed.
- Timing is approximate; the scheduler checks every five seconds and depends on connectivity.
- No automatic retries or exactly-once delivery guarantee.
- Recipients are phone numbers; no contact picker, groups, video or voice-message workflow.
- Thumbnails cannot yet be opened full-size; HEIC/HEIF previews depend on decoder support.
- The complete encrypted queue is rewritten on every change; retention is manual, and large histories consume memory and disk space.
- No independent security audit or notarized release yet. Automated tests cover queue and dispatch behavior; native macOS workflows require manual verification.
- `signal-cli` must stay compatible with Signal service changes.

## License

[MIT](LICENSE). External dependencies retain their own licenses; `signal-cli` is installed separately and is licensed under GPL-3.0-or-later.

Executable preferences are stored locally outside the repository. Automatic mode searches again on startup and before delivery; queued messages use the current validated executable rather than their historical saved path. Version validation checks identity output, not cryptographic authenticity.
