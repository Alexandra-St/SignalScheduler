#!/usr/bin/env python3
"""Create a private, temporary QR-only launcher; never change the app or its preferences."""
import argparse
from pathlib import Path
import shlex
import tempfile


def create_launcher(app, temporary_root=None):
    app = Path(app).resolve(strict=True)
    executable = app / 'Contents/Resources/signal-cli-launcher'
    if not executable.is_file() or not executable.stat().st_mode & 0o111:
        raise ValueError('The selected app has no executable bundled signal-cli launcher.')
    session = Path(tempfile.mkdtemp(prefix='SignalScheduler-QR-test-', dir=temporary_root)).resolve()
    session.chmod(0o700)
    config = session / 'signal-config'
    config.mkdir(mode=0o700)
    launcher = session / 'signal-cli-qr-test'
    script = '''#!/bin/sh
set -eu
umask 077
session=SESSION
config=CONFIG
bundled=BUNDLED
# Refuse a replaced directory; never fall back to the real account location.
[ -d "$session" ] && [ ! -L "$session" ] && [ -d "$config" ] && [ ! -L "$config" ] || exit 73
if [ "$#" -eq 1 ] && [ "$1" = '--version' ]; then
    exec "$bundled" --config "$config" --version
elif [ "$#" -eq 3 ] && [ "$1" = '--output' ] && [ "$2" = 'json' ] && [ "$3" = 'listAccounts' ]; then
    exec "$bundled" --config "$config" --output json listAccounts
elif [ "$#" -eq 3 ] && [ "$1" = 'link' ] && [ "$2" = '-n' ] && [ "$3" = 'Signal Scheduler' ]; then
    exec "$bundled" --config "$config" link -n 'Signal Scheduler QR Test'
fi
# Strict allowlist prevents --config overrides and any message sending.
printf '%s\\n' 'QR test launcher: only version, account discovery and device linking are allowed.' >&2
exit 64
'''
    script = script.replace('SESSION', shlex.quote(str(session))).replace('CONFIG', shlex.quote(str(config))).replace('BUNDLED', shlex.quote(str(executable)))
    launcher.write_text(script)
    launcher.chmod(0o700)
    return launcher, config


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', required=True, type=Path)
    args = parser.parse_args()
    launcher, config = create_launcher(args.app)
    print('Launcher:', launcher)
    print('Isolated configuration:', config)
    print('Choose this launcher in Settings. Restore Use automatic detection after testing.')
