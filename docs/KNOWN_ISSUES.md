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
remain automated checks, not separately reported manual checks. The earlier beta
DMG does not contain this fix.
