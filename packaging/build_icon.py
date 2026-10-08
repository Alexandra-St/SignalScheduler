#!/usr/bin/env python3
"""Convert the supplied artwork into a complete macOS iconset without changing it."""
import argparse
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent


def build_icon(output):
    source = ROOT / 'Assets/app-icon.png'
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='SignalScheduler-icon-') as work:
        iconset = Path(work) / 'SignalScheduler.iconset'
        iconset.mkdir()
        for size in (16, 32, 128, 256, 512):
            for scale in (1, 2):
                filename = 'icon_' + str(size) + 'x' + str(size) + ('@2x' if scale == 2 else '') + '.png'
                subprocess.run(['/usr/bin/sips', '-z', str(size * scale), str(size * scale),
                                str(source), '--out', str(iconset / filename)], check=True, stdout=subprocess.DEVNULL)
        subprocess.run(['/usr/bin/iconutil', '-c', 'icns', str(iconset), '-o', str(output)], check=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    build_icon(parser.parse_args().output)
