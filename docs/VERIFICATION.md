# Manual verification

## Reported verification

The application was built and run on an Apple Silicon Mac. The operator confirmed scheduled text delivery to self, cancellation, queue persistence across restart and delivery to another recipient. The image update was compiled on macOS and clipboard image import produced a thumbnail.

Photo delivery, image-only delivery, attachment persistence after restart, offline errors and interrupted-send recovery are acceptance checks below; they are not claimed as verified. The structural refactor is compiled in the Linux authoring environment with a temporary .NET 8 SDK. Native macOS UI, Keychain, screenshot import and real delivery must be checked again on macOS after rebuilding.

## Automated regression checks

Run `dotnet test Tests/SignalScheduler.Tests.csproj -c Release` from the repository root. The xUnit suite uses synthetic data and fake senders; it never accesses a Signal account.

The refactor passed a Release compilation and 19 regression cases in the Linux authoring environment. VSTest could not start its test host because process metadata is unavailable in that environment; the same compiled xUnit test methods and inline-data cases were executed through a temporary direct invocation harness (19 passed, 0 failed). That harness is not part of the repository. Run the standard command above on macOS before publication.

Coverage includes:
- Reading the original nonce/tag/ciphertext envelope and legacy text-only JSON queues.
- Rejecting tampered ciphertext; preserving attachment bytes and cancellation after reopen.
- User-restricted queue permissions and encryption-key clearing on disposal.
- Future and cancelled items, overdue and blocked items, persisted Sending state and accepted sends.
- Uncertain/interrupted outcomes without automatic retry, overlapping ticks and oldest-due ordering.
- Literal message text through stdin, attachment argument ordering/bytes and temporary file cleanup.

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

## Refactor smoke check on macOS

Replace the old source folder with the new source folder; do not merge it with the previous monolithic files. Build using `bash build-mac.sh`, run the automated tests, then verify the existing queue loads, text and image sends to self, clipboard import, cancellation, copy to composer and close/reopen behavior. Do not delete application data or relink Signal for this update.
