# Known issues and regression status

## Send at validation feedback — fixed; manual verification passed

Reported input: `2026-10-07 23:011`. The button appeared clickable, no message
was added, and the user saw no error. The source now validates immediately beside
the field and disables Schedule message while the time is invalid. Correction
clears the error; past times and daylight-saving gaps/overlaps are also rejected.
The scheduler revalidates at activation and the UI periodically rechecks whether
a previously future time has elapsed. Invalid text is not silently truncated.

Automated regression covers malformed input, invalid dates, empty and elapsed
times, daylight-saving transitions and visible field bindings. On 2026-10-08 the
operator confirmed the rebuilt app works correctly and validation feedback displays
properly. This closes the reported UI issue; individual daylight-saving scenarios
remain automated checks, not separately reported manual checks. The initial 0.2.0 beta DMG did not contain this fix; later build 6 did.

## UI follow-up — build 25

- Date/time fields use numeric keyboard input, dd.MM.yyyy and HH:mm. Suggestions,
  alternate date/time formats and extra shortcuts are deferred.
- Missed messages can be rescheduled as new Pending copies; history is retained.
- Startup notice counters update after deletion/rescheduling, with Dismiss available.
- Review messages is limited to the current startup notification; Show all missed
  displays the complete Missed history. No reviewed status is introduced.
- The filter-selection regression was fixed in build 25 and covered with the actual
  ComboBox binding, with and without a startup notice.
- Delete confirmation hides ordinary actions and offers only Cancel and one Delete.
- Pending text editing and full message text display are implemented.

## Remaining verification boundaries

Clean-Mac/browser-download first launch, current-candidate Replace acceptance,
real QR linking after redesign, real QR expiry, recovery after uncertain sends and
native synthetic screenshots remain pending. Intel is unvalidated and is not a
release target. Sent indicates successful CLI completion, not a delivery receipt.
Blocked still appears under that name; renaming it to Cannot send is not implemented.
