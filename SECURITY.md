# Security

## Current boundaries

SignalScheduler uses AES-256-GCM to encrypt its local queue and macOS Keychain to store the key. The queue includes message content, image bytes, account and recipient identifiers, executable paths, scheduled times and statuses.

This protects data at rest, not a compromised or unlocked user session. Plaintext remains in process memory. Temporary image files exist during clipboard import and sending; permissions are user-restricted, but deletion does not guarantee secure erasure. Image metadata is retained. Account and recipient identifiers are subprocess arguments, and first-time Keychain creation passes the random encryption key as a process argument. The application has not undergone an independent security audit.

Signal authentication state is owned by `signal-cli`. It must not be copied into the source tree or repository. Never upload credentials, account directories, queue files, databases, logs, QR codes, linking URIs or screenshots of real conversations.

An uncertain send outcome requires manual review. Automatic retries are deliberately disabled because a failed command does not establish that a message was not sent.

## Reporting

Do not include exploit details or private data in a public issue. This repository is public; use GitHub's private vulnerability reporting if the maintainer has enabled it. If no private reporting channel is available, open an issue requesting one without disclosing vulnerability details or sensitive data.
