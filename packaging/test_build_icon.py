import struct
import tempfile
import unittest
from pathlib import Path

import build_icon


class BuildIconTests(unittest.TestCase):
    def test_generates_all_icns_representations_from_source_png(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'SignalScheduler.icns'
            build_icon.build_icon(output)
            data = output.read_bytes()

        self.assertEqual(data[:4], b'icns')
        self.assertEqual(struct.unpack('>I', data[4:8])[0], len(data))
        chunks = {}
        offset = 8
        while offset < len(data):
            name = data[offset:offset + 4].decode('ascii')
            size = struct.unpack('>I', data[offset + 4:offset + 8])[0]
            png = data[offset + 8:offset + size]
            self.assertEqual(png[:8], b'\x89PNG\r\n\x1a\n')
            chunks[name] = struct.unpack('>II', png[16:24])
            offset += size

        self.assertEqual(chunks, {
            'icp4': (16, 16), 'icp5': (32, 32), 'icp6': (64, 64),
            'ic07': (128, 128), 'ic08': (256, 256), 'ic09': (512, 512),
            'ic10': (1024, 1024),
        })


if __name__ == '__main__':
    unittest.main()
