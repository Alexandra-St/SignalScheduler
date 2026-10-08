import tempfile
import unittest
from pathlib import Path
import zipfile
from collect_runtime_notices import copy_legal_entries


class NoticeExtractionTests(unittest.TestCase):
    def test_preserves_extensionless_notices_and_paths_without_extracting_code(self):
        with tempfile.TemporaryDirectory() as work:
            root = Path(work)
            archive = root / 'fixture.jar'
            with zipfile.ZipFile(archive, 'w') as source:
                source.writestr('META-INF/LICENSE', 'copyright and terms')
                source.writestr('META-INF/NOTICE.txt', 'notice')
                source.writestr('other/LICENSE', 'other terms')
                source.writestr('org/License.class', 'binary')
                source.writestr('../LICENSE', 'escape')
            copied = copy_legal_entries(archive, root / 'out')
            self.assertEqual(set(copied), {'META-INF/LICENSE', 'META-INF/NOTICE.txt', 'other/LICENSE'})
            self.assertEqual((root / 'out/other/LICENSE').read_text(), 'other terms')
            self.assertFalse((root / 'LICENSE').exists())


if __name__ == '__main__':
    unittest.main()
