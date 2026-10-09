# Isolated manual QR test

This is a reusable development-Mac test procedure, not an outstanding acceptance
task. The build 29 QR scenarios and separate clean VM linking results are recorded
in [BETA_ACCEPTANCE.md](BETA_ACCEPTANCE.md). The helper isolates Signal account
configuration, not the application's ordinary queue.

Create a launcher with `python3 packaging/create_qr_test_launcher.py --app '/Applications/Signal Scheduler.app'`.
The generated launcher and account config live in a new private temporary directory
(mode 0700), outside the repository. Keep that directory until the test is over.
The installed .app, its bundled launcher and production behavior are unchanged.
The helper does not select itself or modify app preferences automatically.

1. Ensure no real scheduled message is due during the test. The app's regular queue
   is not isolated by this helper; attempts to send through this launcher are rejected
   and may change a due message's status.
2. In Settings → signal-cli, choose **Choose executable…**. In the file picker press
   Command+Shift+G and paste the generated launcher path.
3. Expect the normal CLI version and no detected accounts. Click **Connect Signal**.
4. Scan and approve using Signal → Settings → Linked Devices → Link New Device on
   the phone. This creates a separate device called **Signal Scheduler QR Test**;
   it does not replace or delete the existing linked account.
5. Verify the account appears after approval. Also try cancellation and a fresh attempt
   before approval; never share or save the real QR/link URI in the repository.
6. Return to Settings and click **Use automatic detection**. Confirm the usual account
   is visible again. On the phone unlink **Signal Scheduler QR Test** when finished.
7. Close the test dialog and stop any linking attempt before deleting the generated
   temporary directory. Deletion alone does not revoke the device on the phone.

The helper accepts only the exact version, JSON account-list and link commands used
by the app. It prepends its fixed `--config` on every invocation, rejects extra args
(including alternate config flags), rejects message sends and refuses a missing or
symlink-replaced config directory. There is no fallback to normal Signal storage.
Temporary files may be removed by macOS; generate a new launcher if that happens.
