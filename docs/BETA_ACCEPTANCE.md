# Beta acceptance record

## Current status — 0.3.0 build 29

- Production application code: c2ee34f. Commit 6b8d391 added recovery tests;
  44c54d2 updated documentation and the archive README generator, without changing
  production application code.
- Latest Release .NET suite: 171 passing tests; packaging suite: 16 passing tests.
- Development-Mac acceptance and native build 29 layout/rendering review complete;
  individual historical results apply only to their recorded builds and scope.
- Fresh Apple Silicon macOS VM / Safari download / production startup acceptance
  passed with an app-specific quarantine workaround, including real linking and delivery.
  Ordinary Gatekeeper/Open Anyway success and a second physical Mac are not confirmed.
- Intel, live process kill, power loss and live network interruption are unverified;
  recovery regression coverage uses isolated encrypted queues and fake sends.
- Technical source/license coverage passed for the dependency set unchanged through
  build 29. Regenerate the final matching source-material archive and SHA-256 after
  final approved test/documentation commits before release. Tag and GitHub Release
  require separate approval.

## Chronological historical record

The dated entries below preserve what was known at each checkpoint. Older pending
items describe that checkpoint, not today's status; later entries record follow-up
results without rewriting earlier evidence.

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

Historical authorization state at the initial acceptance: no commit, push, tag or
publication had been approved. Current authorization is recorded in the follow-up below.

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

## Packaging and source review follow-up: 0.2.1 build 6

Build 6 retains supplemental Java copyright/NOTICE files and native libsignal
acknowledgments. The review identified and added the exact Temurin JDK commit
rather than relying only on the same-version tag. Native source restoration
passed, including BoringSSL. The audit verified 754 source/POM assets, 79 JAR
binaries and supplemental coverage for all 77 external JARs with no errors.
14 packaging tests passed. Native binaries were not rebuilt bit-for-bit.
A clean Mac and real Finder Replace update remain pending.

## Manual Replace update: build 4 to build 6 (2026-10-08)

The operator followed the DMG Replace update procedure on the development Mac
and reported that the sending account, message history, and pending scheduled
message with caption and attachment were preserved. Screenshots corroborate
Account ready and the restored pending message. Personal account identifiers,
message text and attachment filenames are intentionally not recorded here.

The test message is scheduled for 02:44 Europe/Lisbon. Actual receipt of the photo
and caption exactly once is still pending; Pending is not evidence of delivery.
This check does not establish clean-Mac acceptance.

## Manual post-update delivery confirmed (2026-10-08)

The operator confirmed successful receipt of the scheduled image and caption
exactly once after the Replace update from build 4 to build 6. Screenshots show
Status: Sent in the scheduler and the received image/caption in Signal Note to
Self. This closes post-update delivery and attachment preservation on the
development Mac. Clean-Mac acceptance remains pending. Personal message text,
account identifiers and screenshots are not stored in this report.

## UI and scheduling follow-up — 2026-10-08

The operator approved the redesigned UI and follow-up fixes. Application source
through 0.2.1 build 25 was committed and pushed to main at 319509b. This approval
covers those commits and that push; it does not authorize a tag or GitHub Release.

Build 25 passed 163 .NET tests and Release build-mac.sh checks for signature,
relocation, private Java and isolated account discovery. Headless tests cover
small/standard/wide layouts, old encrypted queue loading, numeric date/time fields,
DST, Pending editing, cancellation, reuse, deletion, status actions, account
selection, settings and synthetic QR linking. They do not establish native visual
acceptance, clean-Mac behavior or a new real phone-linking result.

Missed Reschedule preserves the original history and creates a new Pending copy.
The startup notice tracks its own newly missed IDs; Review messages shows those IDs,
Show all missed shows every Missed record. No reviewed message status is stored.
Filtering regression is covered through the ComboBox's actual two-way binding.

The operator reported reopening about three minutes after the scheduled time:
the message was delivered correctly and appeared Sent. More than five minutes
late remains Missed without sending. No personal message content is retained here.

## New release candidate — 0.3.0 build 26

The candidate advances the minor version for the new UI and compatible features.
Build 6 packaging/Replace evidence remains historical; it must not be attributed
to this candidate. Final package verification, current-candidate Replace acceptance,
real QR linking after redesign, clean-Mac/browser-download acceptance and native
screenshots with synthetic data remain separate checks. Release preparation
changes require their own commit/push approval; tag/publication require separate approval.

## Automated candidate verification — 0.3.0 build 26

163 Release .NET tests and 14 packaging tests passed. build-mac.sh passed signing,
relocation, private-Java and isolated account-discovery checks. The source/notice
audit verified 754 assets, 79 bundled JARs and 77 supplemental notice mappings,
with no integrity or missing-license-evidence errors. Dependency versions and
source provenance are unchanged. Real Signal sending was not performed during
these automated checks. DMG/archive verification is recorded separately below.

The 0.3.0 build 26 DMG passed image-integrity verification, read-only mounting,
bundle signature/relocation/private-Java/isolated-account checks, and was ejected.
Its versioned SHA-256 sidecar was generated. The source-material archive includes
the working release-preparation source; these changes are not yet committed.
Do not attribute the archive to 319509b alone or publish before approval.

## Candidate verification and Replace acceptance — 0.3.0 build 27

The cleanup source was committed and pushed at 98aead1. Build 27 passed 164
Release .NET tests and build-mac.sh bundle checks. The packaging suite passed
14 tests. The dependency source/notice audit verified 754 assets, 79 JARs and
77 supplemental mappings without errors. The DMG passed integrity, read-only
mounting, signature, relocation, private Java and isolated account checks.
Both SHA-256 sidecars were verified. All 257 tracked source files in the matching
source-material archive were compared with commit 98aead1.

The operator confirmed the requested Replace-update check was successful:
the app was updated from the build 27 DMG and the linked account, history,
Pending text and attachment were preserved. This is development-Mac manual
acceptance reported on 2026-10-08, not clean-Mac or real QR acceptance.
No new actual-delivery result is inferred from this preservation check.

The build 27 source archive remains the exact snapshot of 98aead1. This later
acceptance documentation does not change the packaged application. Tag and
GitHub Release publication remain separately unapproved.

## QR follow-up and regression fixes — 0.3.0 build 29

On 2026-10-08 the operator confirmed real QR timeout and Try again after the
redesign. New phone-approved linking and window-close cancellation are not
inferred from that report.

Build 29 fixes the blank History selection when the last startup-notice Missed
message is handled while History is hidden. Filter options retain their identity;
the exhausted new-Missed filter returns to All statuses. Automatic detection is
now an interactive reset control and resets the custom executable preference
independently of linked-account discovery. Saved local history remains available
without a selected Signal account, with an explicit explanation in the UI.

168 Release .NET tests and 16 packaging tests passed. Release build-mac.sh and
the DMG passed signature, relocation, private-Java and isolated-account checks;
the DMG integrity check passed. Automated tests used synthetic accounts and
performed no real Signal sends. The operator reported that the fixes now appear
to work normally and approved commit/push. This is development-Mac acceptance,
not clean-Mac acceptance or proof of all QR scenarios. A matching final source
archive, native synthetic screenshots and release publication remain separate work.

## Manual QR acceptance — build 29

On 2026-10-08 the operator confirmed the remaining QR procedure worked normally
on the installed build 29: closing the linking window, starting a fresh attempt,
phone-approved linking through a separate temporary configuration, and restoring
automatic detection followed by removal of the test device. Combined with the
earlier timeout/Try again report, the planned development-Mac QR scenarios are
complete. No account identifiers, QR codes or linking secrets are recorded.
This does not establish clean-Mac or browser-download acceptance.

## Recovery regression verification — 2026-10-09

Three isolated recovery cases passed against the current application code, using
an encrypted temporary queue and a fake sender; no Signal transmission occurred.
Persisted Sending recovers to Unknown across two reopenings and never dispatches.
A nonzero send result and an interrupted response retain UnknownOrFailed and
Unknown respectively after reopening; unrelated Pending work still dispatches
once and persists as Sent. These checks validate durable recovery state and
duplicate-send prevention, not an actual process kill, power loss, or a live
network interruption. Production application code was unchanged.

The full Release suite passed: 171 .NET tests, including these three cases.

## Native UI review — build 29

The operator accepted native layout/rendering review using a synthetic screenshot
harness at 86a5718. Application code is unchanged from build 29's c2ee34f;
intervening commits only changed documentation. This review confirms native
layout/rendering, not browser-download installation, production startup,
Keychain access or real scheduler execution.

## Clean macOS VM acceptance — build 29, 2026-10-09

Artifact: 0.3.0 build 29 arm64 local DMG. Environment: fresh macOS 27.0.1
(26A434), virtualized on Apple Silicon using Tart, with no host shared folders.
No Homebrew, Java or .NET prerequisites were installed for this procedure.
Results are operator-reported manual checks, separate from automated tests.

| Check | Observed result | Result |
| --- | --- | --- |
| Browser download and installation | Downloaded through Safari inside the VM; copied into Applications | PASS |
| Initial Gatekeeper behavior | Unverified-app warning blocked launch; Open Anyway was not successfully completed | OBSERVED LIMITATION |
| App-specific first-launch workaround | After `xattr -dr com.apple.quarantine "/Applications/Signal Scheduler.app"`, production UI opened with an empty local queue | PASS |
| QR and account selection | Phone-approved linking completed; account appeared with Account ready | PASS |
| Real scheduled text | Pending → Sent; operator confirmed correct receipt | PASS |
| Remaining requested functional checks | Operator confirmed remaining checks successful, including pending cancellation | PASS (operator report) |
| Queue/Keychain persistence | Operator explicitly confirmed close/reopen persistence check | PASS (operator report) |
| Test-device cleanup | Operator explicitly confirmed removal of the temporary linked device | PASS (operator report) |

Quarantine removal reported permission errors on read-only Java legal files;
the app nevertheless launched. The bundle signature was technically valid;
`spctl` rejection is expected for an ad hoc signature and does not by itself
prove damage. The reason Open Anyway did not succeed is unresolved and deferred.
Apple Developer membership, Developer ID and notarization remain outside the beta plan.

This establishes the tested free beta workflow in a clean VM with an explicit
first-launch workaround. It does not establish normal Gatekeeper approval,
acceptance on another physical Mac, physical sleep/wake, a live crash/network
fault, or every reusable tester-checklist item. Guest checksum verification and
an explicit prerequisite inventory were not separately confirmed. No account
identifiers, QR secrets or private queue data are retained in this report.
The VM and temporary testing tools were removed after acceptance.
