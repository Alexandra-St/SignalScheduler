import hashlib
import io
from pathlib import Path
import tarfile
import tempfile
import unittest

import bundle_dependencies as bundle


class BundleDependencyTests(unittest.TestCase):
    def archive(self, directory, name="root/file", link=None):
        path = directory / "test.tar.gz"
        with tarfile.open(path, "w:gz") as archive:
            member = tarfile.TarInfo(name)
            if link is not None:
                member.type = tarfile.SYMTYPE
                member.linkname = link
                archive.addfile(member)
            else:
                member.size = 4
                archive.addfile(member, io.BytesIO(b"test"))
        return path

    def test_extracts_regular_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            destination = root / "output"
            destination.mkdir()
            bundle.extract(self.archive(root), destination)
            self.assertEqual(b"test", (destination / "root/file").read_bytes())

    def test_rejects_escape_paths_before_extracting(self):
        for member in ["../escaped", "/absolute-file"]:
            with self.subTest(member=member), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                destination = root / "output"
                destination.mkdir()
                with self.assertRaises(ValueError):
                    bundle.extract(self.archive(root, member), destination)
                self.assertEqual([], list(destination.iterdir()))

    def test_rejects_escaping_symlinks(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            destination = root / "output"
            destination.mkdir()
            with self.assertRaises(ValueError):
                bundle.extract(self.archive(root, "root/link", "../../escaped"), destination)

    def test_rejects_existing_symlink_parent(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            destination = root / "output"
            destination.mkdir()
            (destination / "root").symlink_to(root)
            with self.assertRaises(ValueError):
                bundle.extract(self.archive(root), destination)
            self.assertFalse((root / "file").exists())

    def test_refuses_corrupt_cached_archive(self):
        with tempfile.TemporaryDirectory() as temporary:
            cache = Path(temporary)
            expected = hashlib.sha256(b"expected archive").hexdigest()
            (cache / (expected + ".tar.gz")).write_bytes(b"corrupt archive")
            with self.assertRaises(ValueError):
                bundle.download({"sha256": expected, "url": "https://unused.example/archive"}, cache)

    def test_reuses_verified_cache_without_network(self):
        with tempfile.TemporaryDirectory() as temporary:
            cache = Path(temporary)
            data = b"verified archive"
            expected = hashlib.sha256(data).hexdigest()
            archive = cache / (expected + ".tar.gz")
            archive.write_bytes(data)
            self.assertEqual(archive, bundle.download({"sha256": expected}, cache))


if __name__ == "__main__":
    unittest.main()
