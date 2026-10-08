# Signal Scheduler 0.2.0 beta — draft release notes

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
- Explicit message statuses and conservative recovery after uncertain sends.
- Settings for choosing a custom signal-cli executable when needed.

## Known limitations

- The app must remain open and the Mac awake. Scheduling is approximate.
- Usernames and username links are resolved at send time. Changed/deleted usernames
  or reset links may no longer resolve; stable recipient UUID storage is not included.
- Username/link delivery awaits manual verification in the new build.
- **Sent** records successful signal-cli completion; it does not confirm delivery or reading.
- Uncertain sends are not retried automatically. Check Signal before scheduling a copy.
- The beta is not notarized. Installation, delivery and real phone linking passed
  on an Apple Silicon development Mac; clean-Mac and browser-download first-launch
  verification remain pending. Developer ID/notarization are optional at this stage.
- Custom icon packaging is implemented; installed Finder/Dock/search appearance awaits verification.
- Intel hardware is not validated. No automatic updates are included.
- Corresponding-source materials are being assembled. This draft is not publication-ready
  until the source checklist and live acceptance checks are complete.

## Reporting a problem

Include macOS version, Mac architecture, app version, what you tried, expected behavior,
and the visible error. Use synthetic text and redact phone numbers. Do not upload QR codes,
linking URIs, Signal account directories, queue files, Keychain data or real conversations.

## Release-owner checklist — remove from published notes

- [ ] Increment Version for the release and BuildNumber for the new build in Version.props.
- [ ] Verify .NET metadata, Info.plist and DMG filename share the same version/build.

- [x] Record operator-reported installation, QR and Note to Self checks on the development Mac.
- [ ] Record clean-Mac verification separately.
- [x] Manually verify the Send at validation/feedback fix in the rebuilt app (2026-10-08).
- [ ] Include the verified fix in the final DMG.
- [ ] Verify the supplied custom icon in Finder, Dock and search in the final installed build.
- [ ] Complete and review source/license materials; resolve manifest gaps.
- [ ] Obtain approval to commit, push, tag and publish.
- [ ] Build from the approved commit and rerun package verification.
- [ ] Attach the DMG, matching SHA-256 and complete matching source materials.
- [ ] Use a prerelease tag, for example `v0.2.0-beta.1`; replace this draft's pending claims.
