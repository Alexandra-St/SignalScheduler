# Source and license review — candidate 0.3.0 build 26

Status: technical source/notice coverage checks passed, 2026-10-08.

The original review was completed for 0.2.1 build 6. Dependencies and pinned
provenance are unchanged; the audit was rerun against 0.3.0 build 26 with zero
integrity or license-evidence errors (754 assets, 79 JARs, 77 notice mappings).

## Evidence

- 754 collected source/POM assets pass SHA-256 checks; all 79 JARs match the binary inventory.
- All 77 external JARs have an explicit mapping to full license text and retained source copyright headers or upstream license/notice files.
- All license/NOTICE entries from published JARs and source JARs are preserved.
- Exact upstream tags were inspected for supplemental root notices; retained texts include ZXing, Kotlin, coroutines and serialization notices. The tagged-file URLs and hashes are in packaging/licenses/root-notices/inventory.json.
- Apache-2.0 is selected where JavaParser offers Apache/LGPL alternatives; EPL-2.0 is selected for Logback's EPL/LGPL alternative. Jakarta's complete embedded terms remain included.
- libsignal's official desktop acknowledgments and AGPL license are included, covering its Rust/native dependencies.
- .NET runtime licenses, NuGet package license/copyright metadata and native Skia/HarfBuzz notices are retained.
- Every checked-in supplemental text is compared against the actual installed .app copy by audit_beta_materials.py; dependency changes cannot silently reuse a different JAR's notices.

## Native source correspondence

The JRE release metadata identifies JDK commit `520406d871955300957ef01e406ac2acd0f9b75c` and Temurin build commit `e6ba7dec3d07654074559310376a3ae89da5f4ac`. The same-version JDK tag is not the same commit, so its archive alone was insufficient. The exact JDK archive is now additionally pinned and included.

libsignal 0.103.0 provides Cargo.lock, Java/JNI build scripts, toolchain metadata and original build instructions. All 582 registry/Git source assets from its lock are included. The Boring fork commit is `1405df0a23a0335618f88ed328c76f67646b088f`; its Git submodule points to BoringSSL `e2a57cfb4d915b4ba820585aef9fdee7bca13fe5`, whose full source archive is included.

`restore_native_sources.py` successfully restored the exact JDK, Temurin build scripts, libsignal, SPQR and Boring trees, including the BoringSSL submodule, from these archives into an isolated temporary directory. Required build instructions and source files were checked.

## Reproduce

    python3 packaging/prepare_beta_sources.py
    python3 packaging/prepare_supplemental_notices.py
    bash build-mac.sh
    python3 packaging/audit_beta_materials.py
    python3 packaging/restore_native_sources.py /tmp/SignalScheduler-native-sources

Increment BuildNumber before producing changed app contents. The archived app source and both lock files must accompany the exact matching binary. Publish the full source-material archive beside the DMG, not just upstream links.

## Practical limits

This is a technical review of source correspondence and retained notices, not a legal opinion. Third-party binaries were not rebuilt or compared bit-for-bit. Compiler/toolchain installation and an offline rebuild were not exercised. Original upstream build scripts and instructions are supplied with the source trees. Clean-Mac acceptance and this candidate's real Finder Replace update remain pending; the historical build 4 → 6 Replace
check passed. Publication requires user approval.
