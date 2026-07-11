# Security Policy

## Supported versions

Only the [latest release](https://github.com/ArteenHD/VirusTotalOneClick/releases/latest) receives security fixes.

## Reporting a vulnerability

Please report vulnerabilities privately via [GitHub's private vulnerability reporting](https://github.com/ArteenHD/VirusTotalOneClick/security/advisories/new) — **not** as a public issue.

Include what you can: affected version, reproduction steps, and impact. This project is maintained by one person in spare time; I aim to acknowledge reports within 14 days and will coordinate a fix and disclosure timeline with you from there.

## Scope

In scope: this application — the installer, the Explorer integration, API key storage, upload handling, and anything else in this repository.

Out of scope: the VirusTotal service itself (report to [Google's VRP](https://bughunters.google.com/)), and the general limitation that DPAPI current-user encryption cannot protect the API key from malware already running as the same user — that boundary is documented in the README and is inherent to the design.
