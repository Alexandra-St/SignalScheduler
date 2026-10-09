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
   try **System Settings → Privacy & Security → Open Anyway**, then confirm if available.
   Follow [Apple's instructions](https://support.apple.com/en-gb/102445).
4. If no linked account is found, choose **Connect Signal**. On your phone open
   **Signal → Settings → Linked Devices → Link New Device**, scan the QR and approve.
   Existing signal-cli accounts are detected without relinking.
5. Send a test message to your own number first. Keep the app open and your Mac awake
   with network access until scheduled messages are sent.

Do not disable Gatekeeper globally. If macOS reports malware or a damaged app rather
than an unidentified developer, stop and report the exact warning before proceeding.

For this trusted beta, the following command in Terminal removes the download
quarantine attribute from this app only. Then open it again from Applications:

```bash
xattr -dr com.apple.quarantine "/Applications/Signal Scheduler.app"
```

This first-launch workaround was verified in a fresh macOS VM after a Safari
download. It does not notarize the app or change Gatekeeper globally. The command
reported permission errors on some read-only bundled Java license files, but the
app subsequently launched. If launch still fails, report the exact error; do not
change system-wide security settings. Open Anyway was not successfully completed
in that VM; why it was unavailable or ineffective remains an unresolved follow-up.

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
- The beta is not notarized. Build 29 installation, production startup, real phone
  linking and scheduled delivery passed in a fresh Apple Silicon macOS VM, with
  the first-launch command above. This is VM acceptance, not a second physical Mac.
  Apple Developer membership, Developer ID and notarization are outside the beta plan.
- Custom icon appearance was verified on the development Mac.
- Intel hardware is not validated. No automatic updates are included.
- The unchanged dependency set passed technical source/license coverage checks.
  The final matching source-material archive and SHA-256 must be regenerated after
  the final approved test/documentation commits and attached to the release.
  Historical Replace update, attachment preservation
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
- [x] Historical verification of build 29 .app, DMG, then-current source archive, hashes and metadata (2026-10-08); later source edits require a new archive.
- [x] Development-Mac Replace acceptance recorded for build 27; build 29 fixes and QR follow-up accepted separately.
- [x] Record real QR linking after redesign, timeout/retry and window-close cancellation on build 29.
- [x] Record clean macOS VM/browser-download acceptance for build 29, including the quarantine workaround and its limits.
- [x] Complete native synthetic layout/rendering review of the build 29 UI; this does not replace production acceptance.
- [x] Verify recovery with isolated encrypted queues and fake sends (171 Release tests passed).
- [x] Operator approved committing and pushing the QR acceptance and subsequent release preparation.
- [ ] Obtain separate approval for a prerelease tag and GitHub Release publication.
- [ ] Attach matching DMG, SHA-256 files and complete source materials; update pending claims.
- [ ] Regenerate and verify the final source-material archive and its hash after the final approved test/documentation commits; earlier archives do not include this consistency pass.
