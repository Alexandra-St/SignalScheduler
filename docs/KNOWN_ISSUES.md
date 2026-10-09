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

Build 29 passed browser-download installation and production startup in a fresh
Apple Silicon macOS VM after the app-specific quarantine command documented in
BETA_RELEASE.md. Native synthetic layout/rendering review was accepted separately.
Recovery passed isolated encrypted-queue tests; actual process termination, power
loss and live network interruption are not claimed. Intel is unvalidated and is not a
release target. Sent indicates successful CLI completion, not a delivery receipt.
Blocked still appears under that name; renaming it to Cannot send is not implemented.

## Follow-up regressions — build 29

- Blank History filter after handling the final new Missed while History was hidden:
  fixed with stable options and a return to All statuses; covered by UI tests.
- Automatic detection: checkbox now invokes reset; reset does not require a linked
  account. Covered with synthetic empty-account discovery.
- Local history without a selected account is retained intentionally; the UI now
  explains this.
- Operator reported the fixes working normally on the development Mac. Real QR
  timeout and Try again also passed manual testing on 2026-10-08.

The operator subsequently confirmed build 29 QR window-close cancellation, a
fresh attempt, phone-approved linking and restoration of automatic detection.
The development-Mac QR checklist is complete. Subsequent clean VM results are
recorded in BETA_ACCEPTANCE.md.

## Gatekeeper first launch — deferred investigation

The Safari-downloaded ad hoc build was blocked in the VM with an unverified-app
warning. App-specific quarantine removal allowed launch. The signature was valid;
Gatekeeper rejection alone does not demonstrate bundle damage. The reason the
Open Anyway workflow did not succeed has not been established. Investigating it
is deferred; Apple Developer membership and notarization are outside the beta plan.
