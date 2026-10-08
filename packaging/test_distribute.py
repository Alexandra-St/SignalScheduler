import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('distribute', Path(__file__).with_name('distribute.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class DistributionTests(unittest.TestCase):
    def test_installer_layout_is_saved_before_compression_and_legacy_text_is_absent(self):
        with tempfile.TemporaryDirectory() as work:
            app = Path(work) / 'Signal Scheduler.app'
            app.mkdir()
            commands = []
            def fake_run(*args, **kwargs):
                commands.append(tuple(str(arg) for arg in args))
                if str(args[0]) == '/usr/bin/osascript':
                    mount = Path(args[-1])
                    (mount / '.DS_Store').write_bytes(b'fixture')
                    self.assertFalse((mount.parent / 'image/Install.txt').exists())
                    self.assertTrue((mount.parent / 'image/Applications').is_symlink())
            with patch.object(module, 'run', side_effect=fake_run):
                module.make_dmg(app, Path(work) / 'output.dmg', local=True)
            operations = [command[1] if len(command) > 1 else '' for command in commands]
            self.assertLess(operations.index('detach'), operations.index('convert'))
            self.assertIn('-format', commands[-2])
            self.assertIn('UDZO', commands[-2])

    def test_missing_finder_layout_fails_and_detaches_writable_image(self):
        with tempfile.TemporaryDirectory() as work:
            app = Path(work) / 'Signal Scheduler.app'
            app.mkdir()
            with patch.object(module, 'run') as run:
                with self.assertRaisesRegex(ValueError, 'Finder did not save'):
                    module.make_dmg(app, Path(work) / 'output.dmg', local=True)
            self.assertEqual(str(run.call_args.args[1]), 'detach')

    def test_native_detection_does_not_follow_symlinks_or_treat_managed_dll_as_macho(self):
        with tempfile.TemporaryDirectory() as work:
            native = Path(work) / 'native'
            native.write_bytes(b'\xcf\xfa\xed\xfe' + b'synthetic')
            link = Path(work) / 'link'
            link.symlink_to(native)
            managed = Path(work) / 'managed.dll'
            managed.write_bytes(b'MZsynthetic')
            self.assertTrue(module.native_file(native))
            self.assertFalse(module.native_file(link))
            self.assertFalse(module.native_file(managed))

    def test_jni_rewrite_preserves_unrelated_entries_and_zip_metadata(self):
        with tempfile.TemporaryDirectory() as work:
            jar = Path(work) / 'native.jar'
            metadata = zipfile.ZipInfo('library.dylib', date_time=(2020, 1, 2, 0, 0, 0))
            metadata.compress_type = zipfile.ZIP_DEFLATED
            with zipfile.ZipFile(jar, 'w') as archive:
                archive.comment = b'preserved'
                archive.writestr(metadata, b'\xcf\xfa\xed\xfesynthetic')
                archive.writestr('code.class', b'unchanged bytecode')
            def synthetic_sign(path, identity):
                path.write_bytes(path.read_bytes() + b'signed')
            with patch.object(module, 'sign', side_effect=synthetic_sign):
                module.sign_jar(jar, 'synthetic identity')
            with zipfile.ZipFile(jar) as archive:
                self.assertEqual(archive.comment, b'preserved')
                self.assertEqual(archive.read('code.class'), b'unchanged bytecode')
                self.assertTrue(archive.read('library.dylib').endswith(b'signed'))
                self.assertEqual(archive.getinfo('library.dylib').date_time, metadata.date_time)
                self.assertEqual(archive.getinfo('library.dylib').compress_type, metadata.compress_type)

    def test_signed_jar_is_rejected_without_modification(self):
        with tempfile.TemporaryDirectory() as work:
            jar = Path(work) / 'signed.jar'
            with zipfile.ZipFile(jar, 'w') as archive:
                archive.writestr('library.dylib', b'\xcf\xfa\xed\xfesynthetic')
                archive.writestr('META-INF/PUBLISHER.SF', b'upstream signature')
            original = jar.read_bytes()
            with self.assertRaises(ValueError):
                module.sign_jar(jar, 'synthetic identity')
            self.assertEqual(jar.read_bytes(), original)

    def test_release_without_credentials_fails_before_packaging(self):
        result = subprocess.run(['python3', str(Path(__file__).with_name('distribute.py')), '--release'], capture_output=True, text=True)
        self.assertEqual(result.returncode, 2)
        self.assertIn('Release requires', result.stderr)


if __name__ == '__main__':
    unittest.main()
