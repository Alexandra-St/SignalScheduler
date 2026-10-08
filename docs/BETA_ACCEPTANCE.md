# Beta acceptance record

## Artifact and evidence boundaries

Version: Signal Scheduler 0.2.0. Manual report date: 2026-10-07.
Platform: Apple Silicon development Mac; macOS version was not supplied.
Artifact reported tested: `build/distribution/SignalScheduler-0.2.0-arm64-local.dmg`,
installed and launched from `/Applications`. Results below are operator-reported
real end-to-end checks of this beta artifact, not inferred from automated tests.

## Automated checks

The app passed 95 .NET tests on Apple Silicon macOS. Packaging initially passed
10 tests; adding the isolated QR-test helper brought the packaging suite to 11.
The DMG passed checksum verification, read-only mounting, signature verification,
relocation and isolated empty-account CLI checks. Headless QR tests use synthetic
URIs and remain separate from the real phone-approved linking results below.

## Manual checks on the development Mac

| Check | Observed result | Result |
| --- | --- | --- |
| DMG installation | App, Applications shortcut and Install.txt visible; earlier app kept as backup; new app copied to Applications | PASS |
| Launch from Applications | Main UI opened normally; no macOS warning on this local DMG on this Mac | PASS |
| Existing linked account | Automatically detected; From populated; Account ready; no relinking required; account data preserved after app replacement | PASS |
| Bundled CLI | Automatic detection selected the installed app's Resources/signal-cli-launcher; version 0.14.9; signal-cli ready | PASS |
| Scheduled text to self | Pending → Sent; exactly one message received with matching text | PASS |
| Photo with caption | One image received with its caption, not a separate text message; no duplicate | PASS |
| Image-only and Paste screenshot | Empty Message accepted; screenshot received; no blank text or duplicate | PASS |
| Cancel pending message | Canceled state appeared; canceled message not delivered | PASS |
| Restart before due time | Pending item survived full close/reopen; sent at due time and received; final status Sent | PASS |
| Isolated QR setup | Same bundled CLI, separate temporary config with zero accounts; sends blocked; ordinary queue had no Pending messages during QR testing | PASS |
| Empty-config onboarding | Welcome screen displayed Connect Signal and Refresh accounts | PASS |
| Real QR generation | Connect Signal window displayed QR, phone instructions and Cancel | PASS |
| QR cancellation | QR disappeared; cancellation status, Try again and Close appeared | PASS |
| QR retry | Fresh linking attempt and new QR appeared | PASS |
| Phone-approved QR linking | Phone scanned and approved the retry QR; Mac reported Signal connected and offered Close | PASS |
| Restore normal configuration | Use automatic detection restored the installed bundled launcher, version 0.14.9 and ready status | PASS |

The existing linked account was not removed for the QR test. The temporary launcher
used a separate config and named the additional device Signal Scheduler QR Test.
Removal of that temporary phone-linked device is recommended after testing but
was not confirmed by the report. No account identifiers or QR/link secrets are
included in this record.

A successful local launch without a warning does not establish Gatekeeper behavior
for a browser-downloaded artifact on another Mac. Sent remains successful CLI
completion; actual receipt was independently observed in the delivery tests above.

## Still pending or not claimed

- Clean Mac without Homebrew, Java or .NET: installation, linking and delivery.
- Browser-downloaded copy's first-launch/Gatekeeper behavior.
- Real QR expiration and window-close cancellation (automated coverage exists).
- Attachment persistence across restart; the restart report confirms a pending
  message, but does not specify that it included an attachment.
- Intel hardware validation and recovery checks not described in the report.
- Removal of the temporary phone-linked test device.

## Follow-up manual verification — 2026-10-08

The operator confirmed the rebuilt app's Send at fix works correctly and feedback
displays properly. Result: **PASS** for the reported validation/feedback issue.
The updated source passed 109 .NET tests. This follow-up applies to the rebuilt
.app, not the earlier DMG, and does not extend clean-Mac verification.

## Recipient support follow-up — manual checks pending

The To field now accepts phone numbers, full Signal usernames and username links.
No new queue schema is introduced; the saved recipient is resolved by signal-cli
at send time. Verify real text and photo sends via username and link in the new
build; prior phone-number tests do not validate these new routes.

## Remaining release work

- Incorporate the verified Send at fix into the final DMG; the earlier tested beta
  DMG does not contain it.
- Manually check the supplied custom icon in Finder, Dock and search after installing
  0.2.1 build 3. Artwork and icon packaging are implemented; appearance is not yet
  manually confirmed.
- Finish independent review of third-party corresponding-source and license
  materials, including outstanding source-manifest review items.
- Record clean-Mac verification separately; do not conflate it with this Mac.

Apple Developer membership, Developer ID signing and notarization are not beta
release requirements at this stage. The public beta will disclose its current
ad hoc signed, non-notarized status and document first launch.

No commit, push, tag or publication has been approved. Obtain explicit approval
before any of those actions; release assets must match the final approved source.

## Follow-up: recipient formats and icon (2026-10-08)

The operator confirmed real delivery using the full Signal username and full
username link, and confirmed the custom icon appears correctly. This records
manual acceptance of those features; it does not claim clean-Mac acceptance.
The previous modified-link attempt resulted in Unknown/failed with no delivery.

## Packaging follow-up: 0.2.1 build 5

This build adds runtime license/notice files. Automated relocation, private Java,
empty account discovery using a temporary config, mounted DMG verification and
12 packaging tests passed. The source audit verified 753 source/POM assets and
79 matching bundled JAR hashes with no integrity errors. Full license/source
review is still open; see SOURCE_REVIEW.md. Installation/update and data
preservation on a clean Mac remain pending.
