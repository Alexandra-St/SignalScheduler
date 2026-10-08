# Signal Scheduler 0.3.0 beta — draft release notes

Schedule Signal messages and photos from your Mac. Connect your phone by scanning
an in-app QR code, choose the sending account, and set a local send time.
The app bundles signal-cli and Java; Homebrew, Java and .NET are not required to run it.

## Install

This beta is for **Apple Silicon Macs**. Intel builds have not been validated.

1. Download the DMG and its SHA-256 file from this release.
2. Open the DMG and drag **Signal Scheduler** to **Applications**. For an update,
   first quit the app and choose **Replace** when prompted. Eject the DMG and
   launch from Applications. Your queue and linked account stay outside the app.
3. Open the app from Applications. This beta is ad hoc signed, **not Developer ID
   signed or notarized**. If macOS blocks it because the developer cannot be verified,
   open **System Settings → Privacy & Security → Open Anyway**, then confirm.
   Follow [Apple's instructions](https://support.apple.com/en-gb/102445).
4. If no linked account is found, choose **Connect Signal**. On your phone open
   **Signal → Settings → Linked Devices → Link New Device**, scan the QR and approve.
   Existing signal-cli accounts are detected without relinking.
5. Send a test message to your own number first. Keep the app open and your Mac awake
   with network access until scheduled messages are sent.

Do not disable Gatekeeper globally. If macOS reports malware or a damaged app rather
than an unidentified developer, stop and report the exact warning before proceeding.

## What's included

- Scheduled text, photos and clipboard screenshots.
- One To field for phone numbers, full Signal usernames or username links.
- A detected-account selector; no manual sender-account input.
- Local encrypted queue, with its key stored in macOS Keychain.
- QR connection, cancellation and a fresh attempt after expiration.
- Upcoming and History lists, status filters, full message details and attachment previews.
- Text editing for Pending messages, cancellation, reuse and guarded history deletion.
- Reschedule for Missed messages creates a new Pending copy and preserves the original history.
- Startup missed-message notification with Review messages, Show all missed and Dismiss.
- Numeric keyboard inputs for local date (dd.MM.yyyy) and time (HH:mm), with future-time and DST validation.
- Explicit message statuses and conservative recovery after uncertain sends.
- Settings for choosing a custom signal-cli executable when needed.

## Known limitations

- The app must remain open and the Mac awake. Scheduling is approximate.
- Usernames and username links are resolved at send time. Changed/deleted usernames
  or reset links may no longer resolve; stable recipient UUID storage is not included.
- Real username/link delivery was verified on the development Mac.
- **Sent** records successful signal-cli completion; it does not confirm delivery or reading.
- A message more than five minutes overdue becomes Missed without a send attempt.
  A message within that window may send after the app resumes; a three-minute delay
  was manually verified on the development Mac.
- Blocked means the CLI file was unavailable before sending. Check Settings before reusing it.
- Uncertain sends are not retried automatically. Check Signal before scheduling a copy.
- The beta is not notarized. Installation, delivery and real phone linking passed
  on an Apple Silicon development Mac; clean-Mac and browser-download first-launch
  verification remain pending. Developer ID/notarization are optional at this stage.
- Custom icon appearance was verified on the development Mac.
- Intel hardware is not validated. No automatic updates are included.
- Matching source/license materials passed technical coverage checks and accompany
  the candidate. Clean-Mac acceptance remains pending; Replace update, attachment preservation
  and one-time post-update delivery passed on the development Mac.

## Reporting a problem

Include macOS version, Mac architecture, app version, what you tried, expected behavior,
and the visible error. Use synthetic text and redact phone numbers. Do not upload QR codes,
linking URIs, Signal account directories, queue files, Keychain data or real conversations.

## Release-owner checklist — remove from published notes

- [x] Candidate version/build: 0.3.0 build 29, sourced from Version.props.
- [x] Development-Mac installation, real QR, delivery and build 4 → 6 Replace checks recorded separately.
- [x] UI redesign and Missed improvements approved, committed and pushed through 319509b.
- [x] Technical dependency source/notice review completed; dependency versions unchanged.
- [x] Verify candidate build 29 .app, DMG, source archive, hashes and metadata (2026-10-08).
- [x] Development-Mac Replace acceptance recorded for build 27; build 29 fixes and QR follow-up accepted separately.
- [x] Record real QR linking after redesign, timeout/retry and window-close cancellation on build 29.
- [ ] Complete clean-Mac/browser-download acceptance.
- [ ] Complete native synthetic screenshots and the current UI acceptance report.
- [x] Operator approved committing and pushing the QR acceptance and subsequent release preparation.
- [ ] Obtain separate approval for a prerelease tag and GitHub Release publication.
- [ ] Attach matching DMG, SHA-256 files and complete source materials; update pending claims.
