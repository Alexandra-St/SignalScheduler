# Signal Scheduler — clean Mac beta check

For an Apple Silicon Mac that has no Homebrew, Java, .NET or previous signal-cli setup.
Do not install these prerequisites for the test. Use the DMG supplied with this checklist.
Record the macOS version, Mac model, application Version and BuildNumber.

## Installation and first launch

- [ ] Download the supplied DMG and SHA-256 file; verify the checksum.
- [ ] Open the DMG; drag Signal Scheduler to Applications, eject the disk image.
- [ ] Launch from Applications. Record the exact first-launch warning, if any.
- [ ] For an unidentified-developer warning, follow System Settings → Privacy & Security → Open Anyway.
      Do not disable Gatekeeper. If the warning says damaged or malware, report it.
- [ ] Confirm the custom icon appears in Finder and Dock.
- [ ] Confirm the app offers Connect Signal and does not request Homebrew, Java or .NET.

## Connection and delivery

- [ ] Connect Signal, scan the QR using Signal → Settings → Linked Devices → Link New Device, and approve.
- [ ] Confirm your sending account appears and Account ready is shown.
- [ ] Schedule synthetic text to your own number two minutes ahead. Keep the Mac awake and app open.
- [ ] Confirm Status: Sent and exactly one message received in Signal.
- [ ] Repeat with a photo and caption; repeat with Paste screenshot and no text.
- [ ] Cancel a pending test and confirm it never arrives.
- [ ] Enter an invalid Send at value (extra minute digit); confirm inline error and disabled scheduling.
- [ ] If available, verify a full username and full username link with a consenting test recipient.

## Update without losing data

This needs two separately numbered builds; use the earlier supplied build first.

- [ ] Create a pending synthetic message far enough in the future to finish the update.
- [ ] Quit the app; open the newer DMG; drag the app into Applications and choose Replace.
- [ ] Eject the DMG and launch from Applications; confirm the new Version/BuildNumber.
- [ ] Confirm the linked account, message history, pending text and attachment are preserved.
- [ ] Confirm the pending test arrives exactly once, or cancel it before its send time.
- [ ] Confirm only one installed copy exists in Applications. Do not retain renamed copies there.

## Report

For each unchecked item: not tested / failed, with a short explanation.
Include architecture, macOS version, both tested builds and screenshots with personal data removed.
Never share QR codes, private username links, account directories, queue files or Keychain data.
