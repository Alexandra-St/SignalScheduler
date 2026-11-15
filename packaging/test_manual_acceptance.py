import importlib.util
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("manual_acceptance", Path(__file__).with_name("manual_acceptance.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ManualAcceptanceTests(unittest.TestCase):
    def _mock_manual_build(self, calls):
        def fake_run(command, *, cwd, check, **kwargs):
            calls.append((command, cwd, kwargs.get("env")))
            if command[0] == "dotnet" and "publish" in command:
                output = Path(command[command.index("--output") + 1])
                output.mkdir(parents=True, exist_ok=True)
                (output / "SignalScheduler.dll").write_text("synthetic test build")
            return subprocess.CompletedProcess(command, 0)
        return fake_run

    def test_launch_without_root_creates_a_generated_profile_and_launches_it(self):
        calls = []
        with patch.object(module.subprocess, "run", side_effect=self._mock_manual_build(calls)):
            module.launch(argparse.Namespace(root=None))

        self.assertEqual(len(calls), 2)
        self.assertIn("-p:ManualAcceptance=true", calls[0][0])
        command, root, env = calls[1]
        root = Path(root)
        profile = Path(env[module.PROFILE_ENV])
        self.assertEqual(command[0], "dotnet")
        self.assertTrue(Path(command[1]).is_file())
        self.assertEqual(module.check_root(root), root.resolve())
        self.assertTrue((root / module.MARKER).is_file())
        self.assertTrue((profile / module.MARKER).is_file())
        self.assertTrue((profile / "fake-signal-cli").is_file())
        self.assertEqual(env["SIGNALSCHEDULER_MANUAL_ACCEPTANCE"], "1")
        shutil.rmtree(root)

    def test_arbitrary_temp_directory_is_still_rejected(self):
        with tempfile.TemporaryDirectory(prefix="SignalScheduler-manual-acceptance-") as temporary:
            with self.assertRaises(SystemExit):
                module.check_root(Path(temporary))

    def test_new_profile_has_synthetic_cli_state_and_existing_acceptance_queue_seed(self):
        with tempfile.TemporaryDirectory(prefix="SignalScheduler-manual-acceptance-") as temporary:
            root = Path(temporary)
            (root / module.MARKER).write_text("build v1")
            profile = module.create_profile(root)
            self.assertEqual((profile / "identity-1.status").read_text(), "UNTRUSTED")
            self.assertEqual((profile / "identity-2.status").read_text(), "UNTRUSTED")
            seed_source = (module.PROJECT / "Infrastructure" / "ManualAcceptanceProfile.cs").read_text()
            seed_source = seed_source.split("public static void SeedQueueWhenEmpty", 1)[1].split(
                "// The manual build never launches", 1)[0]
            for text in ("Synthetic message for identity verification.",
                         "Second synthetic message for retry and cancellation checks."):
                self.assertIn(text, seed_source)
            self.assertEqual(seed_source.count("MessageStatus.RequiresVerification"), 2)
            self.assertEqual(seed_source.count('"+12025550100"'), 2)

    def test_launch_uses_only_temp_profile_not_production_data_directory(self):
        calls = []
        with patch.object(module.subprocess, "run", side_effect=self._mock_manual_build(calls)):
            module.launch(argparse.Namespace(root=None))
        root = Path(calls[1][1])
        profile = Path(calls[1][2][module.PROFILE_ENV])
        self.assertIn(Path(tempfile.gettempdir()).resolve(), root.resolve().parents)
        self.assertEqual(profile, root / "profile")
        self.assertNotIn("Application Support/SignalScheduler", str(profile))
        self.assertTrue((profile / "fake-signal-cli").is_file())
        paths_source = (module.PROJECT / "Infrastructure" / "MacApplicationPaths.cs").read_text()
        self.assertIn("#if MANUAL_ACCEPTANCE\n            return ManualAcceptanceProfile.DataDirectory;", paths_source)
        shutil.rmtree(root)

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
