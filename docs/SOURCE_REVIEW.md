# Source and license review — candidate 0.2.1 build 5

## Completed technical checks

- 79 shipped JARs match the binary hashes in the source manifest.
- 753 primary, Rust, Maven source and parent-POM assets pass SHA-256 verification.
- All external JARs have license evidence in the POM inheritance chain or embedded text.
- Collection reports no missing download assets.
- Bouncy Castle's MIT text is embedded in its JAR, despite its POM omitting licenses.
- Extensionless LICENSE files, NuGet copyright metadata and .NET runtime notices are retained.
- .app includes ThirdPartyLicenses, Java legal files, signal-cli GPL and QRCoder MIT.

## Still open before public distribution

1. Many upstream JARs have no embedded license/NOTICE file. Reconcile the source
   archives and inherited POM declarations with full copyright/license texts and
   required NOTICE files. POM declarations alone do not close this check.
2. Verify native rebuild prerequisites and reconstruction instructions for the
   libsignal Rust sources, pinned Git dependencies and BoringSSL submodule;
   verify Temurin source/build provenance against the supplied official binary.
3. Review the combined source archive against this exact binary. The archive is
   a collected candidate, not a declaration of complete corresponding source.

Evidence: build/beta-materials/manifest.json and audit.json. Reproduce with:

    python3 packaging/prepare_beta_sources.py
    python3 packaging/audit_beta_materials.py

Do not publish a GitHub Release while these checks remain open. Developer ID and
notarization are not requirements of this review.
