#!/usr/bin/env python3
"""Build and launch a private, fake-only Signal Scheduler manual-acceptance profile."""
import argparse
import os
from pathlib import Path
import shlex
import shutil
import stat
import subprocess
import tempfile

PROJECT = Path(__file__).resolve().parents[1]
PROFILE_ENV = "SIGNALSCHEDULER_MANUAL_ACCEPTANCE_PROFILE"
MARKER = ".manual-acceptance-profile"
ACCOUNT = "+12025550100"
RECIPIENTS = {
    "+12025550101": ("identity-1.status", "12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345"),
    "+12025550102": ("identity-2.status", "54321 54321 54321 54321 54321 54321 54321 54321 54321 54321 54321 54321"),
}

FAKE_CLI = r'''#!/bin/sh
set -eu
umask 077
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
account='+12025550100'
if [ "$#" -eq 1 ] && [ "$1" = '--version' ]; then
    printf '%s\n' 'signal-cli 0.14.9'
    exit 0
fi
if [ "$#" -eq 3 ] && [ "$1" = '--output' ] && [ "$2" = 'json' ] && [ "$3" = 'listAccounts' ]; then
    printf '%s\n' '[{"number":"+12025550100"}]'
    exit 0
fi
if [ "$#" -eq 7 ] && [ "$1" = '--output' ] && [ "$2" = 'json' ] && [ "$3" = '-a' ] && [ "$4" = "$account" ] && [ "$5" = 'listIdentities' ] && [ "$6" = '-n' ]; then
    case "$7" in
      '+12025550101') state="$here/identity-1.status"; safety='12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345'; uuid='synthetic-recipient-1' ;;
      '+12025550102') state="$here/identity-2.status"; safety='54321 54321 54321 54321 54321 54321 54321 54321 54321 54321 54321 54321'; uuid='synthetic-recipient-2' ;;
      *) exit 64 ;;
    esac
    trust=$(cat "$state")
    printf '[{"uuid":"%s","fingerprint":"synthetic-only","trustLevel":"%s","safetyNumber":"%s"}]\n' "$uuid" "$trust" "$safety"
    exit 0
fi
if [ "$#" -eq 6 ] && [ "$1" = '-a' ] && [ "$2" = "$account" ] && [ "$3" = 'trust' ] && [ "$4" = '--verified-safety-number' ]; then
    case "$6:$5" in
      '+12025550101:12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345') state="$here/identity-1.status" ;;
      '+12025550102:54321 54321 54321 54321 54321 54321 54321 54321 54321 54321 54321 54321') state="$here/identity-2.status" ;;
      *) exit 64 ;;
    esac
    [ "$(cat "$state")" = 'UNTRUSTED' ] || exit 65
    printf '%s' 'TRUSTED_VERIFIED' > "$state"
    exit 0
fi
# Deliberately reject link, send, and every unlisted operation. This executable has no network calls.
printf '%s\n' 'Manual acceptance fake CLI: operation not allowed.' >&2
exit 64
'''


def create_profile(root: Path) -> Path:
    profile = root / "profile"
    profile.mkdir(mode=0o700)
    (profile / MARKER).write_text("Signal Scheduler manual acceptance profile v1\n")
    (profile / "fake-signal-cli").write_text(FAKE_CLI)
    (profile / "fake-signal-cli").chmod(0o700)
    for recipient, (filename, _) in RECIPIENTS.items():
        (profile / filename).write_text("UNTRUSTED")
        (profile / filename).chmod(0o600)
    return profile


def check_root(root: Path) -> Path:
    root = root.resolve(strict=True)
    temp = Path(tempfile.gettempdir()).resolve(strict=True)
    if temp not in root.parents or not (root / MARKER).is_file() or not (root / "profile" / "fake-signal-cli").is_file():
        raise SystemExit("Refusing: this is not a generated manual-acceptance directory under the system temporary folder.")
    return root


def build(_: argparse.Namespace) -> None:
    root = Path(tempfile.mkdtemp(prefix="SignalScheduler-manual-acceptance-"))
    root.chmod(0o700)
    profile = create_profile(root)
    (root / MARKER).write_text("Signal Scheduler manual acceptance build v1\n")
    app = root / "app"
    app.mkdir(mode=0o700)
    try:
        subprocess.run([
            "dotnet", "publish", str(PROJECT / "SignalScheduler.csproj"), "--configuration", "Release",
            "--no-restore", "-p:ManualAcceptance=true", "--output", str(app), "--verbosity", "quiet"
        ], cwd=PROJECT, check=True)
    except Exception:
        shutil.rmtree(root)
        raise
    launch = root / "launch.command"
    launch.write_text("#!/bin/sh\nset -eu\nexport SIGNALSCHEDULER_MANUAL_ACCEPTANCE=1\n"
                      f"export {PROFILE_ENV}={shlex.quote(str(profile))}\n"
                      f"exec dotnet {shlex.quote(str(app / 'SignalScheduler.dll'))} \"$@\"\n")
    launch.chmod(0o700)
    print(f"Build: {app}")
    print(f"Profile: {profile}")
    print(f"Launch: {launch}")
    print("The manual build uses a local test key and a fake CLI. Sends and device linking are blocked.")


def launch(args: argparse.Namespace) -> None:
    root = check_root(args.root)
    app = root / "app" / "SignalScheduler.dll"
    if not app.is_file():
        raise SystemExit("Build output is missing; run the build command first.")
    env = os.environ.copy()
    env["SIGNALSCHEDULER_MANUAL_ACCEPTANCE"] = "1"
    env[PROFILE_ENV] = str(root / "profile")
    subprocess.run(["dotnet", str(app)], cwd=root, env=env, check=False)


def set_identity(args: argparse.Namespace) -> None:
    root = check_root(args.root)
    if args.recipient not in RECIPIENTS:
        raise SystemExit("Recipient must be one of the synthetic test recipients.")
    filename, _ = RECIPIENTS[args.recipient]
    (root / "profile" / filename).write_text(args.state)
    print(f"Synthetic identity {args.recipient} set to {args.state}.")


def cleanup(args: argparse.Namespace) -> None:
    root = check_root(args.root)
    shutil.rmtree(root)
    print("Removed the isolated manual-acceptance build and synthetic profile.")


parser = argparse.ArgumentParser(description=__doc__)
commands = parser.add_subparsers(required=True)
build_parser = commands.add_parser("build", help="Create an isolated local build and fake profile")
build_parser.set_defaults(run=build)
launch_parser = commands.add_parser("launch", help="Launch an existing isolated build/profile")
launch_parser.add_argument("--root", required=True, type=Path)
launch_parser.set_defaults(run=launch)
identity_parser = commands.add_parser("set-identity", help="Change only a synthetic fixture trust state")
identity_parser.add_argument("--root", required=True, type=Path)
identity_parser.add_argument("--recipient", required=True, choices=RECIPIENTS)
identity_parser.add_argument("--state", required=True, choices=("UNTRUSTED", "TRUSTED_VERIFIED"))
identity_parser.set_defaults(run=set_identity)
cleanup_parser = commands.add_parser("cleanup", help="Remove only a generated temporary manual-acceptance directory")
cleanup_parser.add_argument("--root", required=True, type=Path)
cleanup_parser.set_defaults(run=cleanup)

if __name__ == "__main__":
    parsed = parser.parse_args()
    parsed.run(parsed)
