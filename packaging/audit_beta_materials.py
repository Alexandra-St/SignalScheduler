#!/usr/bin/env python3
"""Check collected source evidence against an exact bundle; report gaps, never infer legal approval."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import re
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(chunk)
    return result.hexdigest()


def audit(app, materials):
    manifest = json.loads((materials / 'manifest.json').read_text())
    failures = []
    checked = 0
    records = manifest['assets'] + manifest['rust_assets']
    for dependency in manifest['dependencies']:
        records += dependency.get('assets', []) + dependency.get('license_parents', [])
        failures += dependency.get('errors', [])
    for record in records:
        path = materials / record['file']
        if not path.is_file() or digest(path) != record['sha256']:
            failures.append('Missing or altered source asset: ' + record['file'])
        checked += 1
    jars = app / 'Contents/Resources/signal-cli/lib'
    expected = {item['binary'] for item in manifest['dependencies']}
    actual = {path.name for path in jars.glob('*.jar')}
    if actual != expected:
        failures.append('Bundle JAR inventory differs from source manifest')
    no_embedded_notice = []
    no_license_evidence = []
    for dependency in manifest['dependencies']:
        jar = jars / dependency['binary']
        if not jar.is_file() or digest(jar) != dependency['binary_sha256']:
            failures.append('Binary differs from source inventory: ' + dependency['binary'])
            continue
        with zipfile.ZipFile(jar) as archive:
            legal = [name for name in archive.namelist() if any(word in Path(name).name.lower() for word in ('license', 'notice', 'copying')) and not name.endswith('.class')]
        if not legal:
            no_embedded_notice.append(dependency['binary'])
        if not legal and not dependency.get('licenses') and not dependency.get('source_component'):
            no_license_evidence.append(dependency['binary'])
    failures += manifest['errors']
    upstream = app / 'Contents/Resources/ThirdPartyLicenses/upstream'
    notice_records = json.loads((upstream / 'source-notices/inventory.json').read_text())
    external = expected - {item['binary'] for item in manifest['dependencies'] if item.get('source_component')}
    if {item['binary'] for item in notice_records} != external:
        failures.append('Supplemental notice inventory does not cover every external JAR')
    for item in notice_records:
        if digest(jars / item['binary']) != item['binary_sha256']:
            failures.append('Supplemental notices refer to a different binary: ' + item['binary'])
        for name in item['license_texts'] + [item['source_headers']]:
            installed = upstream / name
            checked_in = ROOT / 'packaging/licenses' / name
            if not installed.is_file() or digest(installed) != digest(checked_in):
                failures.append('Missing or changed packaged notice: ' + name)
    for original in (ROOT / 'packaging/licenses').rglob('*'):
        if original.is_file():
            installed = upstream / original.relative_to(ROOT / 'packaging/licenses')
            if not installed.is_file() or digest(installed) != digest(original):
                failures.append('Upstream notice was not retained: ' + str(original.name))
    provenance = json.loads((materials / 'native-provenance.json').read_text())
    release = (app / 'Contents/Resources/jre/Contents/Home/release').read_text()
    if 'SOURCE=".:git:' + provenance['jdk_commit'][:12] + '"' not in release:
        failures.append('Exact JDK source does not match binary provenance')
    boring = next(item for item in manifest['rust_assets'] if 'signalapp-boring-' in item['file'])
    if not boring['url'].endswith(provenance['boring_commit']):
        failures.append('Boring fork commit changed')
    boringssl = next(item for item in manifest['assets'] if item['file'] == 'boringssl-source.tar.gz')
    if not boringssl['url'].endswith(provenance['boringssl_commit']):
        failures.append('BoringSSL submodule commit changed')

    info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    return {'version': info['CFBundleShortVersionString'], 'build': info['CFBundleVersion'],
            'verified_source_assets': checked, 'verified_jars': len(actual),
            'integrity_errors': failures, 'missing_license_evidence': no_license_evidence,
            'jars_without_embedded_notices': no_embedded_notice,
            'supplemental_notice_jars': len(notice_records),
            'status': 'INTEGRITY_FAILED' if failures else 'SOURCE_AND_NOTICE_CHECKS_PASSED',
            'limits': ['Native source trees were restored and build instructions reviewed; third-party binaries were not rebuilt or compared bit-for-bit.', 'Clean-Mac acceptance and manual Replace update remain separate checks.']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'build/Signal Scheduler.app')
    args = parser.parse_args()
    materials = ROOT / 'build/beta-materials'
    report = audit(args.app.resolve(), materials)
    (materials / 'audit.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: value for key, value in report.items() if key not in ('jars_without_embedded_notices', 'remaining_review')}, indent=2))
    raise SystemExit(bool(report['integrity_errors'] or report['missing_license_evidence']))
