import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('qr_test_launcher', Path(__file__).with_name('create_qr_test_launcher.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class QrTestLauncherTests(unittest.TestCase):
    def test_only_allowed_operations_use_the_same_private_config(self):
        with tempfile.TemporaryDirectory(prefix="QR test ' spaces ") as work:
            app = Path(work) / 'Synthetic.app'
            bundled = app / 'Contents/Resources/signal-cli-launcher'
            bundled.parent.mkdir(parents=True)
            bundled.write_text('#!/bin/sh\nprintf "%s\\n" "$@"\n')
            bundled.chmod(0o700)
            launcher, config = module.create_launcher(app, work)
            for arguments in (['--version'], ['--output', 'json', 'listAccounts'], ['link', '-n', 'Signal Scheduler']):
                result = subprocess.run([str(launcher)] + arguments, check=True, capture_output=True, text=True)
                lines = result.stdout.splitlines()
                self.assertEqual(lines[:2], ['--config', str(config)])
                self.assertEqual(lines[2:], arguments if arguments[0] != 'link' else ['link', '-n', 'Signal Scheduler QR Test'])
            self.assertEqual(config.stat().st_mode & 0o777, 0o700)
            self.assertEqual(launcher.parent.stat().st_mode & 0o777, 0o700)
            for arguments in (['--config', '/somewhere', '--version'], ['-c/somewhere', 'link'], ['send'], ['link', '-n', 'Other'], []):
                result = subprocess.run([str(launcher)] + arguments, capture_output=True, text=True)
                self.assertEqual(result.returncode, 64)
                self.assertEqual(result.stdout, '')
            config.rmdir()
            config.symlink_to(Path(work))
            result = subprocess.run([str(launcher), '--version'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 73)
            self.assertEqual(result.stdout, '')


if __name__ == '__main__':
    unittest.main()
