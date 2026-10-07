#!/usr/bin/env python3
"""Create macOS bundle metadata from the application's single version source."""
import argparse
from pathlib import Path
import plistlib
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent


def metadata(version_file):
    root = ET.parse(version_file).getroot()
    version = root.findtext('./PropertyGroup/Version', '')
    build = root.findtext('./PropertyGroup/BuildNumber', '')
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version):
        raise ValueError('Version.props must contain a numeric major.minor.patch version.')
    if not re.fullmatch(r'[1-9][0-9]*', build) or int(build) > 65535:
        raise ValueError('BuildNumber must be between 1 and 65535.')
    return {'CFBundleExecutable': 'SignalScheduler', 'CFBundleIdentifier': 'local.SignalScheduler',
        'CFBundleName': 'Signal Scheduler', 'CFBundleVersion': build,
        'CFBundleShortVersionString': version, 'CFBundlePackageType': 'APPL',
        'NSHighResolutionCapable': True}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.write_bytes(plistlib.dumps(metadata(ROOT / 'Version.props')))
