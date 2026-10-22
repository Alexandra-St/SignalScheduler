import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("manual_acceptance", Path(__file__).with_name("manual_acceptance.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ManualAcceptanceTests(unittest.TestCase):
    def test_fake_cli_supports_only_synthetic_read_and_trust_operations(self):
        with tempfile.TemporaryDirectory(prefix="SignalScheduler-manual-acceptance-") as temporary:
            root = Path(temporary)
            (root / module.MARKER).write_text("build v1")
            profile = module.create_profile(root)
            cli = profile / "fake-signal-cli"

            version = subprocess.run([str(cli), "--version"], check=True, capture_output=True, text=True)
            self.assertEqual(version.stdout.strip(), "signal-cli 0.14.9")
            accounts = subprocess.run([str(cli), "--output", "json", "listAccounts"], check=True, capture_output=True, text=True)
            self.assertEqual(accounts.stdout.strip(), '[{"number":"+12025550100"}]')

            recipient = "+12025550101"
            identity = subprocess.run([str(cli), "--output", "json", "-a", module.ACCOUNT,
                                       "listIdentities", "-n", recipient], check=True, capture_output=True, text=True)
            self.assertIn('"trustLevel":"UNTRUSTED"', identity.stdout)
            safety = module.RECIPIENTS[recipient][1]
            subprocess.run([str(cli), "-a", module.ACCOUNT, "trust", "--verified-safety-number", safety, recipient], check=True)
            self.assertEqual((profile / module.RECIPIENTS[recipient][0]).read_text(), "TRUSTED_VERIFIED")

            for args in (["send", "--message-from-stdin", recipient], ["link", "-n", "Signal Scheduler"]):
                result = subprocess.run([str(cli), *args], capture_output=True, text=True)
                self.assertEqual(result.returncode, 64)
                self.assertEqual(result.stdout, "")

            self.assertEqual((profile / module.RECIPIENTS["+12025550102"][0]).read_text(), "UNTRUSTED")

    def test_wrong_safety_number_and_non_synthetic_recipient_are_rejected(self):
        with tempfile.TemporaryDirectory(prefix="SignalScheduler-manual-acceptance-") as temporary:
            root = Path(temporary)
            (root / module.MARKER).write_text("build v1")
            profile = module.create_profile(root)
            cli = profile / "fake-signal-cli"
            wrong = "00000 " * 11 + "00000"
            for recipient, safety in (("+12025550101", wrong), ("+12025550199", module.RECIPIENTS["+12025550101"][1])):
                result = subprocess.run([str(cli), "-a", module.ACCOUNT, "trust", "--verified-safety-number", safety,
                                         recipient], capture_output=True, text=True)
                self.assertEqual(result.returncode, 64)
            self.assertEqual((profile / module.RECIPIENTS["+12025550101"][0]).read_text(), "UNTRUSTED")


if __name__ == "__main__":
    unittest.main()
