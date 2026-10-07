#!/usr/bin/env python3
"""Fetch pinned official archives, validate them, and stage a private app runtime."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tarfile
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parent.parent


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def download(asset, cache):
    cache.mkdir(parents=True, exist_ok=True)
    target = cache / (asset["sha256"] + ".tar.gz")
    if target.exists():
        if sha256(target) != asset["sha256"]:
            raise ValueError("Cached dependency checksum mismatch; remove the corrupt cache file.")
        return target
    print("Downloading " + asset["url"].rsplit("/", 1)[-1], flush=True)
    request = urllib.request.Request(asset["url"], headers={"User-Agent": "SignalScheduler-build"})
    temporary = target.with_suffix(".part")
    try:
        with urllib.request.urlopen(request, timeout=60) as response, temporary.open("wb") as output:
            shutil.copyfileobj(response, output)
        if sha256(temporary) != asset["sha256"]:
            raise ValueError("Downloaded dependency checksum mismatch.")
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return target


def extract(archive, destination):
    # Validate the whole archive before writing any member, including link targets.
    with tarfile.open(archive, "r:gz") as source:
        members = source.getmembers()
        for member in members:
            relative = Path(member.name)
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Unsafe archive member path.")
            if not (member.isfile() or member.isdir() or member.issym() or member.islnk()):
                raise ValueError("Unsupported archive member type.")
            resolved = (destination / member.name).resolve()
            if os.path.commonpath([destination.resolve(), resolved]) != str(destination.resolve()):
                raise ValueError("Archive member escapes destination.")
            if member.issym() or member.islnk():
                link = Path(member.linkname)
                base = resolved.parent if member.issym() else destination
                if link.is_absolute() or os.path.commonpath([destination.resolve(), (base / link).resolve()]) != str(destination.resolve()):
                    raise ValueError("Archive link escapes destination.")
        # Links cannot precede files and redirect extraction through another directory.
        source.extractall(destination, members=[m for m in members if not (m.issym() or m.islnk())])
        source.extractall(destination, members=[m for m in members if m.issym() or m.islnk()])


def single_root(directory):
    roots = [item for item in directory.iterdir() if item.is_dir()]
    if len(roots) != 1:
        raise ValueError("Unexpected dependency archive layout.")
    return roots[0]


def bundle(resources, architecture, cache):
    lock = json.loads((ROOT / "packaging/dependencies.lock.json").read_text())
    cli_archive = download(lock["signal_cli"], cache)
    java_archive = download(lock["java"]["architectures"][architecture], cache)
    license_file = ROOT / "packaging/licenses/signal-cli-LICENSE.txt"
    if sha256(license_file) != lock["signal_cli"]["license_sha256"]:
        raise ValueError("signal-cli license checksum mismatch.")
    resources.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="bundle-", dir=resources.parent) as work:
        stage = Path(work)
        cli_unpack, java_unpack = stage / "cli-unpack", stage / "java-unpack"
        cli_unpack.mkdir()
        java_unpack.mkdir()
        extract(cli_archive, cli_unpack)
        extract(java_archive, java_unpack)
        cli = single_root(cli_unpack)
        java = single_root(java_unpack)
        if not (cli / "lib").is_dir() or not (java / "Contents/Home/bin/java").is_file():
            raise ValueError("Dependency archive is missing runtime files.")
        for name, directory in [("signal-cli", cli), ("jre", java)]:
            output = resources / name
            if output.exists():
                shutil.rmtree(output)
            shutil.copytree(directory, output, symlinks=True)
        shutil.copy2(license_file, resources / "signal-cli/LICENSE")
        shutil.copy2(ROOT / "packaging/signal-cli-launcher", resources / "signal-cli-launcher")
        (resources / "signal-cli-launcher").chmod(0o755)
        shutil.copy2(ROOT / "THIRD_PARTY_NOTICES.md", resources / "THIRD_PARTY_NOTICES.md")
        shutil.copy2(ROOT / "packaging/licenses/QRCoder-LICENSE.txt", resources / "QRCoder-LICENSE.txt")
        (resources / "dependencies.lock.json").write_text(json.dumps(lock, indent=2) + "\n")
    print("Bundled signal-cli " + lock["signal_cli"]["version"] + " and Java " + lock["java"]["version"], flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", required=True, type=Path)
    parser.add_argument("--architecture", choices=["arm64", "x64"], required=True)
    args = parser.parse_args()
    bundle(args.resources.resolve(), args.architecture, ROOT / "build/dependency-cache")
