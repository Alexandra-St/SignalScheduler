# Manual verification

## Current beta verification

Current candidate: **0.3.0 build 29**. The latest Release suite passed 171 .NET
tests; packaging verification passed 16 tests. Development-Mac acceptance and
native UI layout/rendering review are complete, with version-specific evidence in
[the beta acceptance record](BETA_ACCEPTANCE.md).

Build 29 passed Safari-download installation and production startup, phone linking
and scheduled delivery in a fresh Apple Silicon macOS VM. An app-specific quarantine
workaround was required; ordinary Gatekeeper/Open Anyway approval is not proven.
A second physical Mac and Intel hardware were not tested. Recovery regression
tests use isolated queues and fake sends; live process kill, power loss and network
interruption are not confirmed.

The 2026-10-07 installation, bundled CLI detection, text/photo/image-only delivery,
cancellation, restart and isolated QR results apply to the earlier development-Mac
artifact; they are historical evidence, not a new build 29 run of every scenario.

Earlier development versions were reported to deliver scheduled text to self and
another recipient, preserve the queue across restart, and cancel pending messages.
Those historical results do not validate the current beta artifact.

## Automated regression checks

Run `dotnet test Tests/SignalScheduler.Tests.csproj -c Release` and
`python3 -m unittest discover -s packaging -p 'test_*.py'` from the repository root.
Tests use synthetic data, local subprocess fixtures and headless UI. They do not
access a real Signal account, send messages or open the real encrypted queue.

## Acceptance checks

This is a reusable scenario list, not a statement that every scenario has passed
manually on the current candidate. Consult the dated acceptance record for results.

Use synthetic messages and your own Signal account. Never attach account output or real conversations to public reports.

| Scenario | Expected result |
| --- | --- |
| Text scheduled two minutes ahead | One message; status indicates CLI acceptance |
| Cancel before due time | Cancelled state persists; no send attempt |
| Restart before due time | Pending item remains and is attempted at due time |
| Small PNG with a caption | One message with the image and caption |
| Image without text | Image is accepted for scheduling and received |
| Clipboard screenshot | Thumbnail appears; the received image matches |
| Restart with a pending image | Image bytes survive independently of the original file |
| Remove a composer attachment | Removed image is absent from the scheduled item |
| More than 8 images or 20 MiB total | Import is rejected with a limit message |
| Resume more than five minutes late | Missed state; no automatic send |
| Network unavailable during attempted send | Uncertain outcome; no automatic retry |
| Terminate during sending and reopen | Unknown state; no automatic resend |
| Open a second application instance | Queue is not opened concurrently |
| Ambiguous or skipped daylight-saving time | Scheduling is rejected |

After an uncertain outcome, check Signal before scheduling a copy. A successful process exit is not proof of recipient delivery.

## Upgrade smoke check on macOS

Build using `bash build-mac.sh`, run the automated tests, then verify the existing queue loads, text and image sends to self, clipboard import, cancellation, Use as new message and close/reopen behavior. Do not delete application data or relink Signal for this update.
