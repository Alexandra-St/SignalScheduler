#!/usr/bin/env python3
"""Retain exact upstream source notices, including libsignal desktop acknowledgments."""
import hashlib
import json
from pathlib import Path
import re
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
MATERIALS = ROOT / 'build/beta-materials'
OUTPUT = ROOT / 'packaging/licenses/source-notices'


def source_headers(archive):
    """Preserve distinct leading copyright/license headers without inventing owners."""
    headers = {}
    for entry in archive.namelist():
        if not entry.endswith(('.java', '.kt')):
            continue
        content = archive.read(entry).decode('utf-8', errors='replace').replace('\r\n', '\n')
        header = re.split(r'^\s*(?:package|import)\s', content, maxsplit=1, flags=re.M)[0].strip()
        if len(header) > 20000:
            raise ValueError('Unrecognized source header: ' + entry)
        if re.search(r'copyright|licensed under|redistribution and use', header, re.I):
            headers.setdefault(header, []).append(entry)
    return headers


def prepare():
    manifest = json.loads((MATERIALS / 'manifest.json').read_text())
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records = []
    for dependency in manifest['dependencies']:
        sources = [item for item in dependency.get('assets', []) if item['file'].endswith('-sources.jar')]
        if not sources:
            continue
        asset = sources[0]
        source = MATERIALS / asset['file']
        if hashlib.sha256(source.read_bytes()).hexdigest() != asset['sha256']:
            raise ValueError('Altered source JAR: ' + source.name)
        with zipfile.ZipFile(source) as archive:
            headers = source_headers(archive)
            embedded = []
            for name in archive.namelist():
                leaf = Path(name).name.lower()
                if any(word in leaf for word in ('license', 'notice', 'copying')) and not name.endswith(('.java', '.kt', '/')):
                    relative = Path(dependency['binary']) / name
                    if relative.is_absolute() or '..' in relative.parts:
                        raise ValueError('Unsafe source notice path')
                    target = OUTPUT / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(archive.read(name))
                    embedded.append(str(relative))
        target = OUTPUT / (dependency['binary'] + '-source-headers.txt')
        target.write_text('\n\n'.join('Source files: ' + ', '.join(names) + '\n\n' + header for header, names in headers.items()) + '\n')
        licenses = dependency.get('licenses', [])
        names = [license.get('name', '') for license in licenses]
        if any('Apache' in name for name in names):
            chosen = 'Apache-2.0'; texts = ['Apache-2.0.txt']
        elif any('EPL' in name for name in names):
            chosen = 'EPL-2.0'; texts = ['EPL-2.0.html']
        elif dependency['binary'].startswith('argparse4j-'):
            chosen = 'MIT'; texts = ['argparse4j-LICENSE.txt']
        elif dependency['binary'].startswith('dbus-java-'):
            chosen = 'MIT'; texts = ['dbus-java-LICENSE']
        elif dependency['binary'].startswith('asm-'):
            chosen = 'BSD-3-Clause'; texts = ['source-notices/' + target.name]
        elif any('AGPL' in name for name in names):
            chosen = 'AGPL-3.0-only'; texts = ['source-notices/libsignal-LICENSE']
        elif any('GPLv3' in name for name in names):
            chosen = 'GPL-3.0'; texts = ['signal-cli-LICENSE.txt']
        elif any('MIT-0' in name for name in names):
            chosen = 'MIT-0'; texts = ['MIT-0.txt']
        elif embedded:
            chosen = 'upstream embedded terms'; texts = ['source-notices/' + name for name in embedded]
        else:
            raise ValueError('No full license text mapped: ' + dependency['binary'])
        records.append({'binary': dependency['binary'], 'binary_sha256': dependency['binary_sha256'],
                        'source': asset, 'selected_license': chosen, 'license_texts': texts,
                        'source_headers': 'source-notices/' + target.name, 'header_count': len(headers),
                        'embedded_source_notices': embedded})
    with tarfile.open(MATERIALS / 'libsignal-source.tar.gz') as archive:
        for name, target in [('LICENSE', 'libsignal-LICENSE'), ('acknowledgments/acknowledgments-desktop.md', 'libsignal-desktop-ACKNOWLEDGMENTS.md')]:
            member = next(item for item in archive if item.name.endswith('/' + name) and item.name.count('/') == name.count('/') + 1)
            (OUTPUT / target).write_bytes(archive.extractfile(member).read())
    for record in records:
        for name in record['license_texts']:
            if not (ROOT / 'packaging/licenses' / name).is_file():
                raise ValueError('Missing full license: ' + name)
    (OUTPUT / 'inventory.json').write_text(json.dumps(records, indent=2) + '\n')
    print('Retained source notice evidence for', len(records), 'external JARs and native libsignal acknowledgments.')


if __name__ == '__main__':
    prepare()
