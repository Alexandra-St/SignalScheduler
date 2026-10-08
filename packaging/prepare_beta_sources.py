#!/usr/bin/env python3
"""Collect version-matched source assets and inventory; never declares legal completeness."""
from concurrent.futures import ThreadPoolExecutor
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import tarfile
from urllib.parse import urlparse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'build/beta-materials'


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(chunk)
    return result.hexdigest()


def fetch(url, filename, expected=None):
    target = OUT / filename
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        temporary = target.with_suffix(target.suffix + '.part')
        try:
            request = urllib.request.Request(url, headers={'User-Agent': 'SignalScheduler-beta-materials'})
            with urllib.request.urlopen(request, timeout=60) as response, temporary.open('wb') as stream:
                shutil.copyfileobj(response, stream)
            temporary.replace(target)
        finally:
            temporary.unlink(missing_ok=True)
    checksum = digest(target)
    if expected and checksum != expected:
        raise ValueError('Source asset checksum mismatch: ' + filename)
    return {'url': url, 'file': filename, 'sha256': checksum}


def coordinate(jar):
    with zipfile.ZipFile(jar) as archive:
        for name in archive.namelist():
            if name.endswith('/pom.properties'):
                values = dict(line.split('=', 1) for line in archive.read(name).decode().splitlines() if '=' in line and not line.startswith('#'))
                return values['groupId'], values['artifactId'], values['version']
    match = re.fullmatch(r'(.+?)-(\d.*)\.jar', jar.name)
    if not match:
        raise ValueError('Unknown artifact name')
    artifact, version = match.groups()
    groups = {
        'bcprov-': 'org.bouncycastle', 'kotlinx-': 'org.jetbrains.kotlinx', 'kotlin-': 'org.jetbrains.kotlin',
        'arrow-': 'io.arrow-kt', 'okhttp': 'com.squareup.okhttp3', 'okio-': 'com.squareup.okio',
        'kotlinpoet': 'com.squareup', 'wire-': 'com.squareup.wire',
        'micronaut-sourcegen-': 'io.micronaut.sourcegen', 'micronaut-json-schema-': 'io.micronaut.jsonschema',
        'micronaut-serde-': 'io.micronaut.serde', 'micronaut-': 'io.micronaut',
        'reactor-': 'io.projectreactor', 'rxkotlin': 'io.reactivex.rxjava3', 'rxjava': 'io.reactivex.rxjava3',
        'reactive-streams': 'org.reactivestreams', 'asm': 'org.ow2.asm', 'checker-qual': 'org.checkerframework',
        'jspecify': 'org.jspecify', 'annotations': 'org.jetbrains', 'libsignal-client': 'org.signal',
        'signal-service-java': 'com.github.turasa', 'signal-network': 'com.github.turasa',
        'core-network': 'com.github.turasa', 'models-jvm': 'com.github.turasa',
        'serialization': 'com.github.turasa', 'util-jvm': 'com.github.turasa',
    }
    for prefix, group in groups.items():
        if artifact.startswith(prefix):
            return group, artifact, version
    raise ValueError('Unresolved Maven coordinate')


def pom_licenses(path, visited=None):
    """Resolve license inheritance using exact parent coordinates, keeping POM evidence."""
    visited = set() if visited is None else visited
    ns = {'m': 'http://maven.apache.org/POM/4.0.0'}
    root = ET.parse(path).getroot()
    licenses = [{child.tag.split('}')[-1]: child.text for child in item}
                for item in root.findall('m:licenses/m:license', ns)]
    if licenses:
        return licenses, []
    parent = root.find('m:parent', ns)
    if parent is None:
        return [], []
    group, artifact, version = [parent.findtext('m:' + key, namespaces=ns)
                              for key in ('groupId', 'artifactId', 'version')]
    coordinate = (group, artifact, version)
    if not all(coordinate) or coordinate in visited or any('${' in value for value in coordinate):
        raise ValueError('Unresolved Maven parent license inheritance')
    visited.add(coordinate)
    filename = 'maven-parents/' + group + '/' + artifact + '-' + version + '.pom'
    url = 'https://repo.maven.apache.org/maven2/' + group.replace('.', '/') + '/' + artifact + '/' + version + '/' + artifact + '-' + version + '.pom'
    asset = fetch(url, filename)
    inherited, evidence = pom_licenses(OUT / filename, visited)
    return inherited, [asset] + evidence


def collect_jar(jar):
    result = {'binary': jar.name, 'binary_sha256': digest(jar), 'assets': [], 'errors': []}
    if jar.name.startswith(('signal-cli-', 'libsignal-cli-')):
        result['source_component'] = 'signal-cli'
        return result
    try:
        group, artifact, version = coordinate(jar)
        result['coordinate'] = ':'.join((group, artifact, version))
        base = ('https://build-artifacts.signal.org/libraries/maven/' if group == 'org.signal' else 'https://repo.maven.apache.org/maven2/') + group.replace('.', '/') + '/' + artifact + '/' + version + '/' + artifact + '-' + version
        for suffix in ('.pom', '-sources.jar'):
            try:
                asset = fetch(base + suffix, 'maven/' + group + '/' + artifact + '-' + version + suffix)
                result['assets'].append(asset)
                if suffix == '.pom':
                    pom = ET.parse(OUT / asset['file'])
                    ns = {'m': 'http://maven.apache.org/POM/4.0.0'}
                    result['licenses'], result['license_parents'] = pom_licenses(OUT / asset['file'])
                    result['scm'] = pom.findtext('m:scm/m:url', namespaces=ns)
            except Exception as error:
                result['errors'].append(str(error) + ': ' + base + suffix)
    except Exception as error:
        result['errors'].append(str(error))
    return result


def collect_rust_sources():
    with tarfile.open(OUT / 'libsignal-source.tar.gz') as archive:
        member = next(item for item in archive.getmembers() if item.name.endswith('/Cargo.lock') and item.name.count('/') == 1)
        lock = archive.extractfile(member).read().decode()
    downloads = {}
    for block in lock.split('[[package]]')[1:]:
        fields = dict(re.findall(r'^(name|version|source|checksum) = "([^"\n]+)"', block, re.M))
        source = fields.get('source', '')
        if source.startswith('registry+'):
            name, version = fields['name'], fields['version']
            downloads[(name, version)] = ('https://static.crates.io/crates/' + name + '/' + name + '-' + version + '.crate', 'rust/' + name + '-' + version + '.crate', fields['checksum'])
        elif source.startswith('git+'):
            parsed = urlparse(source[4:])
            if parsed.hostname != 'github.com' or not parsed.fragment:
                raise ValueError('Unresolved Rust git source: ' + source)
            repository = parsed.path.strip('/').removesuffix('.git') if hasattr(str, 'removesuffix') else parsed.path.strip('/').replace('.git', '')
            downloads[(repository, parsed.fragment)] = ('https://codeload.github.com/' + repository + '/tar.gz/' + parsed.fragment, 'rust/' + repository.replace('/', '-') + '-' + parsed.fragment + '.tar.gz', None)
    results, errors = [], []
    with ThreadPoolExecutor(max_workers=8) as pool:
        work = [(args, pool.submit(fetch, *args)) for args in downloads.values()]
        for args, future in work:
            try:
                results.append(future.result())
            except Exception as error:
                errors.append({'url': args[0], 'error': str(error)})
    return results, errors


def collect_nuget_notices():
    assets = json.loads((ROOT / 'obj/project.assets.json').read_text())
    folder = Path(next(iter(assets['packageFolders'])))
    result = []
    for name, library in sorted(assets['libraries'].items()):
        if library.get('type') != 'package':
            continue
        directory = folder / library['path']
        archive = next(directory.glob('*.nupkg'), None)
        record = {'package': name, 'assets': []}
        if archive:
            record['archive_sha256'] = digest(archive)
            with zipfile.ZipFile(archive) as package:
                for entry in package.namelist():
                    leaf = Path(entry).name.lower()
                    if entry.endswith('.nuspec') or ('license' in leaf or 'notice' in leaf):
                        target = OUT / 'nuget-notices' / name / Path(entry).name
                        target.parent.mkdir(parents=True, exist_ok=True)
                        target.write_bytes(package.read(entry))
                        record['assets'].append(str(target.relative_to(OUT)))
            nuspec = next(directory.glob('*.nuspec'), None)
            if nuspec:
                xml = ET.parse(nuspec)
                metadata = next(child for child in xml.getroot() if child.tag.split('}')[-1] == 'metadata')
                record['metadata'] = {child.tag.split('}')[-1]: child.text for child in metadata if child.tag.split('}')[-1] in ('license', 'licenseUrl', 'projectUrl', 'copyright')}
        else:
            record['error'] = 'Missing cached NuGet archive'
        result.append(record)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'build/Signal Scheduler.app')
    args = parser.parse_args()
    app = args.app.resolve()
    OUT.mkdir(parents=True, exist_ok=True)
    assets, errors = [], []
    lock = json.loads((ROOT / 'packaging/dependencies.lock.json').read_text())
    urls = [
        ('https://codeload.github.com/AsamK/signal-cli/tar.gz/refs/tags/v' + lock['signal_cli']['version'], 'signal-cli-source.tar.gz', None),
        ('https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.4.1%2B1/OpenJDK25U-jdk-sources_25.0.4.1_1.tar.gz', 'temurin-source.tar.gz', 'cb9e50d2eb3de72ffd28466d7c511b4039b4bc4bd8f0d276d1e4cf81e30fdf09'),
        ('https://codeload.github.com/adoptium/temurin-build/tar.gz/e6ba7dec3d07654074559310376a3ae89da5f4ac', 'temurin-build-source.tar.gz', None),
        ('https://codeload.github.com/signalapp/libsignal/tar.gz/refs/tags/v0.103.0', 'libsignal-source.tar.gz', None),
        ('https://codeload.github.com/Turasa/libsignal-service-java/tar.gz/refs/tags/v2.15.3_unofficial_154', 'signal-service-source.tar.gz', None),
    ]
    with ThreadPoolExecutor(max_workers=6) as pool:
        futures = [pool.submit(fetch, *args) for args in urls]
        jars = list((app / 'Contents/Resources/signal-cli/lib').glob('*.jar'))
        if not jars:
            raise ValueError('Build the app before collecting beta materials.')
        dependencies = list(pool.map(collect_jar, sorted(jars)))
        for args, future in zip(urls, futures):
            try:
                assets.append(future.result())
            except Exception as error:
                errors.append({'url': args[0], 'error': str(error)})
    nuget = collect_nuget_notices()
    java = app / 'Contents/Resources/jre/Contents/Home'
    # Upstream legal files are read-only; rebuild this generated directory instead of overwriting.
    if (OUT / 'java-legal').exists():
        shutil.rmtree(OUT / 'java-legal')
    shutil.copytree(java / 'legal', OUT / 'java-legal', symlinks=True)
    shutil.copy2(java / 'NOTICE', OUT / 'java-NOTICE')
    assets.append(fetch('https://codeload.github.com/google/boringssl/tar.gz/e2a57cfb4d915b4ba820585aef9fdee7bca13fe5', 'boringssl-source.tar.gz'))
    provenance = json.loads((ROOT / 'packaging/native-sources.lock.json').read_text())
    release = dict(re.findall(r'^(\w+)=\"([^\"]*)\"', (java / 'release').read_text(), re.M))
    if not release['SOURCE'].endswith(provenance['jdk_commit'][:12]):
        raise ValueError('Exact JDK source provenance differs from bundled Java')
    if release['BUILD_SOURCE'] != 'git:e6ba7dec3d07654074559310376a3ae89da5f4ac':
        raise ValueError('Temurin build source provenance changed')
    assets.append(fetch(provenance['jdk_source']['url'], provenance['jdk_source']['file'], provenance['jdk_source']['sha256']))
    rust_assets, rust_errors = collect_rust_sources()
    errors.extend(rust_errors)
    shutil.copytree(ROOT / 'packaging/licenses', OUT / 'licenses', dirs_exist_ok=True)
    shutil.copy2(ROOT / 'THIRD_PARTY_NOTICES.md', OUT / 'THIRD_PARTY_NOTICES.md')
    shutil.copy2(ROOT / 'packaging/dependencies.lock.json', OUT / 'dependencies.lock.json')
    shutil.copy2(ROOT / 'packaging/native-sources.lock.json', OUT / 'native-provenance.json')
    shutil.copy2(app / 'Contents/Resources/jre/Contents/Home/release', OUT / 'temurin-build-provenance.txt')
    report = {'status': 'REVIEW_REQUIRED', 'assets': assets, 'rust_assets': rust_assets, 'nuget': nuget, 'dependencies': dependencies, 'errors': errors,
        'review_required': ['Verify native submodules/build prerequisites (including BoringSSL), inherited license choices and complete corresponding source coverage.',
            'Resolve any missing source assets and inherited Maven licenses.',
            'Review .NET/NuGet notices and Java legal materials.',
            'Publish the reviewed complete source materials alongside the exact matching binary.']}
    (OUT / 'manifest.json').write_text(json.dumps(report, indent=2) + '\n')
    (OUT / 'README.txt').write_text('Collected beta source materials; REVIEW REQUIRED.\nNot a declaration of complete corresponding source.\nSee manifest.json for exact binary hashes, downloaded source hashes and remaining gaps.\n')
    print('Collected', len(assets), 'primary source archives and inventoried', len(dependencies), 'JARs.')
    print('Missing dependency assets:', sum(bool(item['errors']) for item in dependencies), '; primary errors:', len(errors))
    print('Rust source assets:', len(rust_assets), '; failures:', len(rust_errors))
    print('Materials:', OUT)


if __name__ == '__main__':
    main()
