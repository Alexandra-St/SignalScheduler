import tempfile
import unittest
from pathlib import Path
import zipfile
from prepare_supplemental_notices import source_headers
from restore_native_sources import restore


class SupplementalNoticeTests(unittest.TestCase):
    def test_preserves_distinct_copyright_and_license_headers(self):
        with tempfile.TemporaryDirectory() as work:
            jar = Path(work) / 'source.jar'
            with zipfile.ZipFile(jar, 'w') as archive:
                archive.writestr('A.java', '// Copyright A\n// Redistribution and use permitted\npackage test;\nclass A {}')
                archive.writestr('B.java', '// Copyright B\npackage test;\nclass B {}')
                archive.writestr('C.java', '// Copyright A\n// Redistribution and use permitted\npackage test;\nclass C {}')
            with zipfile.ZipFile(jar) as archive:
                headers = source_headers(archive)
            self.assertEqual(len(headers), 2)
            self.assertEqual(headers['// Copyright A\n// Redistribution and use permitted'], ['A.java', 'C.java'])
            self.assertTrue(all('class ' not in text for text in headers))

    def test_native_restoration_refuses_existing_destination(self):
        with tempfile.TemporaryDirectory() as work:
            destination = Path(work)
            marker = destination / 'keep.txt'
            marker.write_text('keep')
            with self.assertRaisesRegex(ValueError, 'already exists'):
                restore(destination / 'missing', destination)
            self.assertEqual(marker.read_text(), 'keep')


if __name__ == '__main__':
    unittest.main()
