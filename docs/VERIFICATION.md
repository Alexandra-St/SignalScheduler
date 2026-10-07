# Manual verification

## Reported verification

The application was built and run on an Apple Silicon Mac. The operator confirmed scheduled text delivery to self, cancellation, queue persistence across restart and delivery to another recipient. The image update was compiled on macOS and clipboard image import produced a thumbnail.

Photo delivery, image-only delivery, attachment persistence after restart, offline errors and interrupted-send recovery are acceptance checks below; they are not claimed as verified. The authoring environment is Linux without a .NET SDK, so it has not independently compiled the project. There is no automated test suite yet.

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
