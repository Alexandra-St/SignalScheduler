# Manual verification

## Current beta verification

See [the beta acceptance record](BETA_ACCEPTANCE.md) for current automated evidence
and remaining checks. On 2026-10-07 the operator reported real installation,
bundled CLI detection, text/photo/image-only delivery, cancellation, restart and
phone-approved isolated QR linking on the DMG-installed Apple Silicon app.
Clean-Mac verification remains pending; these results apply to the development Mac.

Earlier development versions were reported to deliver scheduled text to self and
another recipient, preserve the queue across restart, and cancel pending messages.
Those historical results do not validate the current beta artifact.

## Automated regression checks

Run `dotnet test Tests/SignalScheduler.Tests.csproj -c Release` and
`python3 -m unittest discover -s packaging -p 'test_*.py'` from the repository root.
Tests use synthetic data, local subprocess fixtures and headless UI. They do not
access a real Signal account, send messages or open the real encrypted queue.

## Acceptance checks

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
