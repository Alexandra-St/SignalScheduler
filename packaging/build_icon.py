#!/usr/bin/env python3
"""Convert the supplied artwork into a complete macOS iconset without changing it."""
import argparse
import struct
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
REPRESENTATIONS = ((16, 'icp4'), (32, 'icp5'), (64, 'icp6'),
                   (128, 'ic07'), (256, 'ic08'), (512, 'ic09'), (1024, 'ic10'))


def build_icon(output):
    source = ROOT / 'Assets/app-icon.png'
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='SignalScheduler-icon-') as work:
        iconset = Path(work) / 'SignalScheduler.iconset'
        iconset.mkdir()
        chunks = []
        for size, chunk_type in REPRESENTATIONS:
            image = iconset / (str(size) + '.png')
            subprocess.run(['/usr/bin/sips', '-z', str(size), str(size), str(source),
                            '--out', str(image)], check=True, stdout=subprocess.DEVNULL)
            png = image.read_bytes()
            if png[:8] != b'\x89PNG\r\n\x1a\n':
                raise ValueError('sips did not produce a PNG image')
            chunks.append(chunk_type.encode('ascii') + struct.pack('>I', len(png) + 8) + png)
        payload = b''.join(chunks)
        output.write_bytes(b'icns' + struct.pack('>I', len(payload) + 8) + payload)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    build_icon(parser.parse_args().output)
