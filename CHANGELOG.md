# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-08-11

First release.

### Added

- **Upload to VirusTotal** in the Explorer right-click menu for files and folders, plus a Send To entry — multi-file selections merge into a single upload queue.
- Folder review window: browse a folder's contents and choose exactly which files to upload, individually or bundled as one zip archive.
- Upload queue with per-file status, progress, direct links to VirusTotal analysis reports, and automatic retry when the API rate-limits.
- Support for files up to VirusTotal's 650 MB limit; uploads over 32 MB route through VirusTotal's dedicated upload endpoint automatically.
- Guided first run in the Settings window with live API key validation; the key is stored DPAPI-encrypted for the current user.
- Anonymizer options: replace submitted file names with anonymous ones and hide folder structure inside uploaded zips.
- Optional local upload history (append-only JSONL) with a history window and one-click report access.
- Settings window covering upload behavior, folder handling, anonymizer, history, and each Explorer integration toggle independently.
- Native Windows 11 styling throughout: Mica backdrops, system accent color, full light and dark theme support.
- Per-user Inno Setup installer (no admin required) and a portable zip with `install-shell` / `uninstall-shell` commands.
- Release artifacts ship with SHA-256 checksums and GitHub build-provenance attestations.

[Unreleased]: https://github.com/ArteenHD/VirusTotalOneClick/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ArteenHD/VirusTotalOneClick/releases/tag/v1.0.0
