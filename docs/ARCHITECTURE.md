# Architecture

Two projects, one rule: everything that can be headless is.

```
src/
  VirusTotalOneClick.Core/   net10.0-windows, no WPF — all logic, fully unit-testable
  VirusTotalOneClick/        WPF app — windows, viewmodels, composition, single-instance
tests/
  VirusTotalOneClick.Core.Tests/   headless xUnit
  VirusTotalOneClick.App.Tests/    viewmodels + STA window smoke tests
  packaging/                       Pester suite over the Inno Setup script
```

## Core

| Area | Types | Notes |
| --- | --- | --- |
| Upload planning | `UploadPlanner`, `UploadPlan`, `FileSelection` | Turns a raw selection (files, folders, mixed) into a concrete plan per the user's folder mode. |
| Upload execution | `UploadSession`, `FileUploadWorkflow`, `FolderZipUploadWorkflow` | Sequential uploads; retries when VirusTotal returns 429; reports per-item results. |
| VirusTotal client | `VirusTotalClient`, `VirusTotalOptions` | API v3. Files ≤ 32 MB post to `/files`; larger ones fetch a one-time upload URL; 650 MB hard ceiling. Maps HTTP failures to user-readable errors. |
| Folder scanning | `FolderScanner`, `ReviewRootLocator` | Async enumeration feeding the review window. |
| Zip building | `ZipArchiveBuilder`, `ZipPlan` | Builds review-selected zips; deduplicates internal name collisions. |
| Anonymizer | `AnonymizerService`, `AnonymizerOptions` | Renames submissions / strips zip-internal paths. Never mutates source files — it changes only what is sent. |
| Secrets | `DpapiFileSecretStore` (`ISecretStore`) | API key in `apikey.bin`, DPAPI current-user scope. Named for exactly what it does. |
| Settings | `AppSettingsStore`, `AppSettings` | JSON under `%APPDATA%\VirusTotalOneClick`; atomic saves, and unreadable files fall back to safe defaults (upload confirmation forced on) rather than crashing. |
| History | `UploadHistoryStore` (`IUploadHistoryStore`) | Append-only JSONL; tolerant of corrupt lines; multi-process appends are safe. `NullUploadHistoryStore` when history is off. |
| Shell integration | `ShellIntegrationService`, `ShellIntegrationInstaller`, shortcut services | Generates the registry entries (`HKCU\Software\Classes\*\shell\...`), Send To / Start Menu / startup `.lnk` files. Everything per-user; no HKLM. |
| CLI | `CommandLineParser`, `CommandLineOptions`, `AppMode` | Routes `upload <paths>`, `settings`, `history`, `install-shell`, `uninstall-shell`; no arguments means first-run setup or settings. |

## App

- **`AppBootstrapper` / `AppComposition`** — poor-man's DI: composes Core services with real filesystem/registry/HTTP implementations and picks the window for the parsed CLI mode.
- **Windows** — Setup, Settings, Folder Review, Upload Progress, History. Each is a `FluentWindow` (WPF-UI) with a viewmodel; shared visual language lives in `Themes/Tokens.xaml`.
- **Single instance** — `SingleInstanceCoordinator` owns a per-user named pipe. When Explorer launches one process per selected file, the first becomes primary and the rest forward their arguments and exit; `InvocationCoalescer` merges forwarded paths into the primary's queue within a debounce window, so a 12-file selection is one window with 12 rows.

## Data flow: right-click → report

```
Explorer verb: VirusTotalOneClick.exe upload --path "C:\path\file.exe"
  → CommandLineParser → AppMode.Upload
  → SingleInstanceCoordinator (primary? else forward + exit)
  → UploadPlanner → UploadProgressWindow (queue)
  → UploadSession → AnonymizerService → VirusTotalClient (HTTPS, api/v3)
  → per-item ResultUrl → UploadHistoryStore.Append → "Open result"
```

Folders differ only at the front: the same `upload` verb with a folder argument routes to the review window, `FolderScanner` fills the tree, and the user's checked set becomes the plan (individual files or one zip).

## Trust boundaries

- **Outbound network:** `virustotal.com` hosts only, HTTPS only — enforced in code, including the one-time upload URL the API hands back for large files, which is validated before the key is attached. The API key travels only in the `x-apikey` header.
- **At rest:** the key is DPAPI-encrypted per user. That protects against other accounts and offline disk theft — not against code already running as the same user.
- **Registry writes:** all shell integration is HKCU; installing and uninstalling never needs elevation.
