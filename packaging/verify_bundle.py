#!/usr/bin/env python3
"""Smoke-test a relocated app with no Homebrew, system Java, or real account access."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile


def verify(app):
    resources = app / "Contents/Resources"
    lock = json.loads((resources / "dependencies.lock.json").read_text())
    subprocess.run(["/usr/bin/codesign", "--verify", "--deep", "--strict", str(app)], check=True)
    with tempfile.TemporaryDirectory(prefix="SignalScheduler-bundle-check-") as directory:
        work = Path(directory)
        moved = work / "Relocated Signal Scheduler.app"
        # ditto preserves macOS code-signing extended attributes, including managed DLLs.
        subprocess.run(["/usr/bin/ditto", str(app), str(moved)], check=True)
        launcher = moved / "Contents/Resources/signal-cli-launcher"
        environment = {"PATH": "/usr/bin:/bin", "TMPDIR": str(work),
            "JAVA_HOME": "/no-system-java", "JAVA_TOOL_OPTIONS": "invalid-inherited-option",
            "JDK_JAVA_OPTIONS": "invalid-inherited-option", "_JAVA_OPTIONS": "invalid-inherited-option"}
        version = subprocess.run([str(launcher), "--version"], env=environment,
            capture_output=True, text=True, check=True, timeout=30)
        if version.stdout.strip() != "signal-cli " + lock["signal_cli"]["version"]:
            raise ValueError("Bundled signal-cli version does not match the lock file.")
        config = work / "empty-signal-config"
        result = subprocess.run([str(launcher), "--config", str(config), "--output", "json", "listAccounts"],
            env=environment, capture_output=True, text=True, check=True, timeout=30)
        if json.loads(result.stdout) != []:
            raise ValueError("Isolated account discovery did not return an empty list.")
    print("Bundle checks passed: signature, relocation, private Java, isolated account discovery.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("app", type=Path)
    verify(parser.parse_args().app.resolve())
