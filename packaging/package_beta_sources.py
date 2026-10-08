#!/usr/bin/env python3
"""Archive reviewed source materials and exact working app source with versioned names."""
import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parent.parent


def package():
    materials = ROOT / 'build/beta-materials'
    app = ROOT / 'build/Signal Scheduler.app'
    audit = json.loads((materials / 'audit.json').read_text())
    info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    version, build = info['CFBundleShortVersionString'], info['CFBundleVersion']
    if audit['status'] != 'SOURCE_AND_NOTICE_CHECKS_PASSED' or audit['version'] != version or audit['build'] != build:
        raise ValueError('Source audit must pass for this exact app before archiving')
    snapshot = materials / 'SignalScheduler-source'
    if snapshot.exists():
        shutil.rmtree(snapshot)
    snapshot.mkdir()
    files = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
    # Explicit source/tooling directories; never copy ignored builds, accounts or Git metadata.
    files += [str(path.relative_to(ROOT)) for directory in ('packaging', 'docs') for path in (ROOT / directory).rglob('*')
              if path.is_file() and '__pycache__' not in path.parts and path.suffix not in ('.pyc',)]
    for name in sorted(set(files)):
        if not name:
            continue
        original = ROOT / name
        if original.is_file():
            target = snapshot / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, target)
    shutil.copy2(ROOT / 'docs/SOURCE_REVIEW.md', materials / 'SOURCE_REVIEW.md')
    runtime = materials / 'runtime-notices'
    if runtime.exists():
        shutil.rmtree(runtime)
    shutil.copytree(app / 'Contents/Resources/ThirdPartyLicenses', runtime)
    (materials / 'README.txt').write_text('Signal Scheduler ' + version + ' build ' + build + ' source materials.\nTechnical source/notice coverage passed; see SOURCE_REVIEW.md for evidence and limits.\nSignalScheduler-source contains the working application source for this candidate.\nThe matching binary has not yet passed clean-Mac or manual-update acceptance.\nFor native source restoration, run SignalScheduler-source/packaging/restore_native_sources.py with --materials pointing to this directory.\nOriginal upstream build instructions and toolchain metadata are inside the source archives.\n')
    destination = ROOT / 'build/distribution'
    destination.mkdir(exist_ok=True)
    archive = destination / ('SignalScheduler-' + version + '-build.' + build + '-source-materials.tar.gz')
    with tarfile.open(archive, 'w:gz', compresslevel=1) as output:
        output.add(materials, arcname='SignalScheduler-source-materials')
    hasher = hashlib.sha256()
    with archive.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            hasher.update(chunk)
    archive.with_suffix(archive.suffix + '.sha256').write_text(hasher.hexdigest() + '  ' + archive.name + '\n')
    print('Prepared source archive:', archive)


if __name__ == '__main__':
    package()
