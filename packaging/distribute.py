#!/usr/bin/env python3
"""Create a local DMG, or sign and notarize an explicitly configured release."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
MACHO = {b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xfe\xed\xfa\xcf", b"\xfe\xed\xfa\xce", b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca"}


def run(*args, **kwargs):
    return subprocess.run([str(arg) for arg in args], check=True, **kwargs)


def native_file(path):
    if path.is_symlink() or not path.is_file():
        return False
    with path.open("rb") as stream:
        return stream.read(4) in MACHO


def sign(path, identity, entitlements=None):
    args = ["/usr/bin/codesign", "--force", "--sign", identity, "--timestamp", "--options", "runtime"]
    if entitlements:
        args += ["--entitlements", entitlements]
    run(*args, path)


def sign_jar(path, identity):
    # JNI libraries are extracted at runtime. Sign them before sealing the outer bundle.
    with zipfile.ZipFile(path) as source:
        entries = source.infolist()
        native = [entry for entry in entries if entry.filename.endswith((".dylib", ".jnilib"))]
        if not native:
            return
        if any(entry.filename.upper().startswith("META-INF/") and entry.filename.upper().endswith((".SF", ".RSA", ".DSA", ".EC")) for entry in entries):
            raise ValueError("Cannot rewrite a signed JAR; review the upstream artifact first.")
        with tempfile.TemporaryDirectory(prefix="sign-jni-") as work:
            directory = Path(work)
            replacements = {}
            for index, entry in enumerate(native):
                library = directory / (str(index) + ".dylib")
                library.write_bytes(source.read(entry))
                if not native_file(library):
                    raise ValueError("Expected a Mach-O JNI library.")
                sign(library, identity)
                replacements[entry.filename] = library.read_bytes()
            replacement = directory / "signed.jar"
            with zipfile.ZipFile(replacement, "w") as target:
                target.comment = source.comment
                for entry in entries:
                    target.writestr(entry, replacements.get(entry.filename, source.read(entry)))
            shutil.copy2(replacement, path)


def sign_bundle(app, identity):
    for jar in (app / "Contents/Resources/signal-cli/lib").glob("*.jar"):
        sign_jar(jar, identity)
    java = app / "Contents/Resources/jre/Contents/Home/bin/java"
    host = app / "Contents/MacOS/SignalScheduler"
    for binary in sorted(app.rglob("*"), key=lambda path: len(path.parts), reverse=True):
        if native_file(binary):
            entitlement = None
            if binary == java:
                entitlement = ROOT / "packaging/java.entitlements.plist"
            elif binary == host:
                entitlement = ROOT / "packaging/dotnet.entitlements.plist"
            sign(binary, identity, entitlement)
    sign(app / "Contents/Resources/jre", identity)
    sign(app, identity, ROOT / "packaging/dotnet.entitlements.plist")
    run("/usr/bin/codesign", "--verify", "--deep", "--strict", app)


def notarize(path, profile, logs):
    result = run("xcrun", "notarytool", "submit", path, "--keychain-profile", profile,
                 "--wait", "--output-format", "json", capture_output=True, text=True)
    report = json.loads(result.stdout)
    logs.write_text(json.dumps(report, indent=2) + "\n")
    if report.get("status") != "Accepted":
        if report.get("id"):
            run("xcrun", "notarytool", "log", report["id"], "--keychain-profile", profile, logs.with_suffix(".log.json"))
        raise ValueError("Apple did not accept notarization; inspect the build report.")


def make_dmg(app, output, local):
    with tempfile.TemporaryDirectory(prefix="SignalScheduler-dmg-") as work:
        stage = Path(work) / "image"
        stage.mkdir()
        run("/usr/bin/ditto", app, stage / app.name)
        (stage / "Applications").symlink_to("/Applications")
        text = "Drag Signal Scheduler to Applications, then eject the DMG and open the app from Applications.\n"
        text += "Updating: quit the app first and choose Replace. Keep your queue, Keychain data and linked account.\n"
        if local:
            text += "Local development build: not Developer ID signed or notarized.\n"
        (stage / "Install.txt").write_text(text)
        run("/usr/bin/hdiutil", "create", "-ov", "-format", "UDZO", "-fs", "HFS+",
            "-volname", "Signal Scheduler", "-srcfolder", stage, output)
    run("/usr/bin/hdiutil", "verify", output)


def verify_dmg(path):
    with tempfile.TemporaryDirectory(prefix="SignalScheduler-mounted-") as work:
        mount = Path(work) / "volume"
        mount.mkdir()
        run("/usr/bin/hdiutil", "attach", "-readonly", "-nobrowse", "-mountpoint", mount, path)
        try:
            app = mount / "Signal Scheduler.app"
            if not (mount / "Applications").is_symlink() or (mount / "Applications").readlink() != Path("/Applications"):
                raise ValueError("Missing Applications shortcut.")
            run("python3", ROOT / "packaging/verify_bundle.py", app)
        finally:
            run("/usr/bin/hdiutil", "detach", mount)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--release", action="store_true")
    parser.add_argument("--identity")
    parser.add_argument("--notary-profile")
    parser.add_argument("--bundle-id")
    args = parser.parse_args()
    if args.release:
        if not args.identity or not args.identity.startswith("Developer ID Application:") or not args.notary_profile or not args.bundle_id or args.bundle_id.startswith("local."):
            parser.error("Release requires Developer ID Application identity, Keychain notary profile, and a non-local bundle ID.")
        identities = run("/usr/bin/security", "find-identity", "-v", "-p", "codesigning", capture_output=True, text=True).stdout
        if args.identity not in identities:
            parser.error("Requested Developer ID identity is unavailable.")
    elif args.identity or args.notary_profile or args.bundle_id:
        parser.error("Signing options require --release.")
    source = ROOT / "build/Signal Scheduler.app"
    info = plistlib.loads((source / "Contents/Info.plist").read_bytes())
    architecture = run("/usr/bin/uname", "-m", capture_output=True, text=True).stdout.strip()
    destination = ROOT / "build/distribution"
    destination.mkdir(parents=True, exist_ok=True)
    suffix = "release" if args.release else "local"
    image = destination / ("SignalScheduler-" + info["CFBundleShortVersionString"] + "-build." + info["CFBundleVersion"] + "-" + architecture + "-" + suffix + ".dmg")
    with tempfile.TemporaryDirectory(prefix="SignalScheduler-release-") as work:
        app = Path(work) / source.name
        run("/usr/bin/ditto", source, app)
        if args.release:
            info["CFBundleIdentifier"] = args.bundle_id
            (app / "Contents/Info.plist").write_bytes(plistlib.dumps(info))
            sign_bundle(app, args.identity)
            archive = Path(work) / "application.zip"
            run("/usr/bin/ditto", "-c", "-k", "--keepParent", app, archive)
            notarize(archive, args.notary_profile, destination / "app-notarization.json")
            run("xcrun", "stapler", "staple", app)
            run("xcrun", "stapler", "validate", app)
            run("/usr/sbin/spctl", "--assess", "--type", "execute", app)
        make_dmg(app, image, local=not args.release)
    if args.release:
        run("/usr/bin/codesign", "--sign", args.identity, "--timestamp", image)
        notarize(image, args.notary_profile, destination / "dmg-notarization.json")
        run("xcrun", "stapler", "staple", image)
        run("xcrun", "stapler", "validate", image)
        run("/usr/sbin/spctl", "--assess", "--type", "open", "--context", "context:primary-signature", image)
    verify_dmg(image)
    hasher = hashlib.sha256()
    with image.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    digest = hasher.hexdigest()
    image.with_suffix(".sha256").write_text(digest + "  " + image.name + "\n")
    print("Created " + str(image))
    if not args.release:
        print("Local development DMG; not a notarized public release.")


if __name__ == "__main__":
    main()
