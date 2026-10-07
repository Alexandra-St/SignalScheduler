# SignalScheduler project rules

## Versions and updates

- `Version.props` is the single source for application Version and BuildNumber.
  Do not duplicate literal versions in assembly metadata, Info.plist or DMG names.
- Before producing a new build for testing or distribution, increment BuildNumber.
  Never reuse a version/build pair for changed application contents. Keep BuildNumber
  increasing across releases. This is a required manual step; the build script does
  not increment it automatically.
- Before releasing an update, increment Version as well (patch for a bug fix,
  minor for compatible new features). Historical acceptance records retain the
  version actually tested; do not rewrite them to the current version.
- Bundle name, installation name and bundle identifier remain stable across updates.
- Update instructions must say: quit the app, drag the new app from the DMG into
  Applications, choose Replace, eject the DMG, then launch from Applications.
  Never require deleting the queue, Keychain key or linked account for an update.
- Rebuild final release artifacts from approved source; verify version/build metadata
  and artifact names. Do not publish an older DMG after changing application source.

## Authorization and beta scope

- Commit, push, tag and publication require explicit user approval for each action.
- Apple Developer membership, Developer ID and notarization are not requirements
  for this beta. Revisit them only if the user explicitly asks.
- Third-party license/source review is independent of Apple signing.
- Never commit account data, real QR/link URIs, queues, private keys or credentials.
