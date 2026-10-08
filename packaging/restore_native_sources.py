#!/usr/bin/env python3
"""Restore exact collected native source trees, including the omitted Git submodule."""
import argparse
import json
from pathlib import Path
import shutil
import tempfile
from bundle_dependencies import extract, sha256, single_root

ROOT = Path(__file__).resolve().parent.parent


def restore(materials, destination):
    if destination.exists():
        raise ValueError('Destination already exists; source trees must not be overwritten')
    manifest = json.loads((materials / 'manifest.json').read_text())
    provenance = json.loads((materials / 'native-provenance.json').read_text())
    assets = {item['file']: item for item in manifest['assets'] + manifest['rust_assets']}
    primary = {'libsignal': 'libsignal-source.tar.gz', 'jdk': provenance['jdk_source']['file'],
               'temurin-build': 'temurin-build-source.tar.gz', 'boringssl': 'boringssl-source.tar.gz'}
    for item in manifest['rust_assets']:
        if item['file'].endswith('.tar.gz'):
            filename = item['file']
            if 'signalapp-boring-' in filename:
                primary['boring'] = filename
            elif 'signalapp-SparsePostQuantumRatchet-' in filename:
                primary['spqr'] = filename
            else:
                raise ValueError('Unrecognized native Git source: ' + filename)
    if 'boring' not in primary or 'spqr' not in primary:
        raise ValueError('Missing native Git source')
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='native-source-restore-', dir=destination.parent) as work:
        staged = Path(work) / 'sources'
        staged.mkdir()
        for name, filename in primary.items():
            archive = materials / filename
            if sha256(archive) != assets[filename]['sha256']:
                raise ValueError('Native source checksum mismatch: ' + filename)
            unpacked = Path(work) / name
            unpacked.mkdir()
            extract(archive, unpacked)
            shutil.move(str(single_root(unpacked)), staged / name)
        submodule = staged / 'boring/boring-sys/deps/boringssl'
        if submodule.exists():
            if any(submodule.iterdir()):
                raise ValueError('BoringSSL submodule destination is not empty')
            submodule.rmdir()
        shutil.move(str(staged / 'boringssl'), submodule)
        for relative in ('libsignal/Cargo.lock', 'libsignal/java/build_jni.sh',
                         'libsignal/rust-toolchain', 'libsignal/acknowledgments/acknowledgments-desktop.md',
                         'jdk/doc/building.md', 'temurin-build/README.md',
                         'boring/boring-sys/deps/boringssl/CMakeLists.txt'):
            if not (staged / relative).is_file():
                raise ValueError('Missing native build material: ' + relative)
        shutil.copy2(materials / 'native-provenance.json', staged / 'provenance.json')
        staged.rename(destination)
    print('Restored exact native source trees and BoringSSL submodule:', destination)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--materials', type=Path, default=ROOT / 'build/beta-materials')
    args = parser.parse_args()
    restore(args.materials.resolve(), args.destination.resolve())
