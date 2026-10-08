#!/usr/bin/env python3
"""Include cached package notices and upstream copyright texts in every app bundle."""
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent


def copy_legal_entries(archive, destination):
    copied = []
    with zipfile.ZipFile(archive) as source:
        for entry in source.namelist():
            path = Path(entry)
            if entry.endswith('/') or path.is_absolute() or '..' in path.parts:
                continue
            leaf = path.name.lower()
            if ('license' in leaf or 'notice' in leaf or leaf.startswith('copying')) and not leaf.endswith(('.class', '.dll', '.so', '.dylib')):
                target = destination / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(source.read(entry))
                copied.append(entry)
    return copied


def collect(resources):
    destination = resources / 'ThirdPartyLicenses'
    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir()
    supplemental = ROOT / 'packaging/licenses/source-notices/inventory.json'
    evidence = json.loads(supplemental.read_text())
    for item in evidence:
        binary = resources / 'signal-cli/lib' / item['binary']
        import hashlib
        if hashlib.sha256(binary.read_bytes()).hexdigest() != item['binary_sha256']:
            raise ValueError('Source notices no longer match bundled binary: ' + item['binary'])
    shutil.copytree(ROOT / 'packaging/licenses', destination / 'upstream')
    assets = json.loads((ROOT / 'obj/project.assets.json').read_text())
    cache = Path(next(iter(assets['packageFolders'])))
    records = []
    for name, library in sorted(assets['libraries'].items()):
        if library.get('type') != 'package':
            continue
        directory = cache / library['path']
        archive = next(directory.glob('*.nupkg'))
        target = destination / 'nuget' / name
        files = copy_legal_entries(archive, target)
        nuspec = next(directory.glob('*.nuspec'))
        target.mkdir(parents=True, exist_ok=True)
        shutil.copy2(nuspec, target / nuspec.name)
        metadata = next(item for item in ET.parse(nuspec).getroot() if item.tag.split('}')[-1] == 'metadata')
        values = {item.tag.split('}')[-1]: item.text for item in metadata if item.tag.split('}')[-1] in ('license', 'copyright')}
        records.append({'package': name, 'notices': files, **values})
    # Self-contained runtime packs are outside the ordinary NuGet libraries list.
    deps = json.loads((resources.parent / 'MacOS/SignalScheduler.runtimeconfig.json').read_text())
    frameworks = deps['runtimeOptions']['includedFrameworks']
    runtime = next(item['version'] for item in frameworks if item['name'] == 'Microsoft.NETCore.App')
    dependency_metadata = json.loads((resources.parent / 'MacOS/SignalScheduler.deps.json').read_text())
    rid = dependency_metadata['runtimeTarget']['name'].split('/')[-1]
    if rid not in ('osx-arm64', 'osx-x64'):
        raise ValueError('Unexpected macOS runtime identifier')
    runtime_directory = cache / ('microsoft.netcore.app.runtime.' + rid) / runtime
    for name in ('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT'):
        target = destination / 'dotnet' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(runtime_directory / name, target)
    for jar in sorted((resources / 'signal-cli/lib').glob('*.jar')):
        copy_legal_entries(jar, destination / 'maven' / jar.name)
    (destination / 'inventory.json').write_text(json.dumps({'dotnet': runtime, 'nuget': records}, indent=2) + '\n')
    return destination


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resources', type=Path, required=True)
    args = parser.parse_args()
    print('Included license materials:', collect(args.resources.resolve()))
