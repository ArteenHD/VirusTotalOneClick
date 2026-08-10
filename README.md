<p align="center">
  <img src="src/VirusTotalOneClick/Assets/app-icon.png" width="112" alt="VirusTotal OneClick icon">
</p>

<h1 align="center">VirusTotal OneClick</h1>

<p align="center">Right-click any file in Windows Explorer and scan it with VirusTotal.</p>

<p align="center">
  <a href="https://github.com/ArteenHD/VirusTotalOneClick/releases/latest"><img src="https://img.shields.io/github/v/release/ArteenHD/VirusTotalOneClick?label=release" alt="Release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ArteenHD/VirusTotalOneClick" alt="License: MIT"></a>
  <a href="https://github.com/ArteenHD/VirusTotalOneClick/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/ArteenHD/VirusTotalOneClick/ci.yml?branch=main&label=ci" alt="CI"></a>
  <a href="https://github.com/ArteenHD/VirusTotalOneClick/releases"><img src="https://img.shields.io/github/downloads/ArteenHD/VirusTotalOneClick/total" alt="Downloads"></a>
</p>

![Right-click a file or folder, upload it to VirusTotal, and get a link to the report](assets/demo.gif)

## Features

- Folder review before anything is sent: pick exactly which files go, individually or zipped
- Multi-file selections coalesce into a single upload queue
- Optional filename anonymization and zip path scrubbing
- Local, append-only upload history with one-click report access
- Native Windows 11 look: Mica backdrops, system accent, full light/dark support
- Per-user install, self-contained, nothing running in the background
- Handles files up to VirusTotal's 650 MB ceiling; anything over the API's 32 MB direct-upload limit routes through VirusTotal's large-file endpoint
- Sequential uploads with automatic retry when VirusTotal rate-limits

## Install

1. Download **VirusTotalOneClickSetup.exe** from the [latest release](https://github.com/ArteenHD/VirusTotalOneClick/releases/latest).
2. Run it. The installer is per-user, so no administrator rights are needed.
3. Grab a free API key from [virustotal.com](https://www.virustotal.com/) (profile menu → **API Key**) and paste it into the setup window that opens after install. The key is validated before it is saved.
4. Right-click any file in Explorer and choose **Upload to VirusTotal**. The upload queue opens, uploads the file, and gives you a link to the analysis report.

> **SmartScreen warning:** releases are not code-signed, so Windows SmartScreen may warn on first run ("Windows protected your PC"). Click **More info → Run anyway**, or verify the download first.

<details>
<summary><strong>Verify your download</strong> (optional)</summary>

Every release asset (the installer and the portable zip) ships a SHA-256 checksum and a GitHub build-provenance attestation, so you can confirm the file you downloaded is exactly what the release workflow built:

```powershell
# Checksum: compare against VirusTotalOneClickSetup.exe.sha256 from the release
Get-FileHash .\VirusTotalOneClickSetup.exe -Algorithm SHA256

# Provenance: proves the file was built by this repo's release workflow on GitHub Actions
gh attestation verify .\VirusTotalOneClickSetup.exe --repo ArteenHD/VirusTotalOneClick
```

`gh attestation verify` requires [GitHub CLI](https://cli.github.com/) 2.49+, signed in via `gh auth login`. The same commands work for the zip.

</details>

### Portable

Prefer no installer? Download **VirusTotalOneClick-win-x64.zip** from the release and extract it anywhere. Everything lands in a `VirusTotalOneClick-win-x64` folder. From inside it, run:

```powershell
.\VirusTotalOneClick.exe install-shell    # add the Explorer integration
.\VirusTotalOneClick.exe uninstall-shell  # remove it again
```

The app is self-contained: no .NET runtime install required. It runs nothing in the background. No services, no scheduled tasks, no startup processes (unless you turn on "Open settings when Windows starts" yourself).

## Usage

Right-click any file or folder and choose **Upload to VirusTotal**. **Send To → Upload to VirusTotal** is the keyboard-friendly alternative. Past uploads are in **History**, and **Settings** is in your Start Menu.

## Options

- **Upload behavior:** upload immediately, or ask for confirmation first
- **Folders:** upload reviewed files individually, or as a single zip
- **Anonymizer:** replace submitted file names with anonymous ones, and hide folder structure inside uploaded zips. This reduces what metadata is sent; it does **not** make the upload private (see below)
- **History:** keep a local record of uploads, or turn it off entirely
- **Integration:** toggle the right-click menu, Send To entry, Start Menu shortcuts, and launch-at-startup independently

## Screenshots

| Upload queue | Folder review |
| --- | --- |
| ![Upload queue window](assets/upload-queue-dark.png) | ![Folder review window](assets/folder-review-dark.png) |

| Settings | History |
| --- | --- |
| ![Settings window](assets/settings-dark.png) | ![History window](assets/history-dark.png) |

The app follows your Windows theme and accent color, and every window works in light and dark mode.

## Security & Privacy

- **Your API key** is stored in `%APPDATA%\VirusTotalOneClick\apikey.bin`, encrypted with Windows DPAPI in current-user scope ([`DpapiFileSecretStore.cs`](src/VirusTotalOneClick.Core/DpapiFileSecretStore.cs)).
- **Network traffic** goes exclusively to VirusTotal (`www.virustotal.com` and its subdomains) over HTTPS. The large-file upload URL that VirusTotal returns is validated before your key is attached ([`VirusTotalClient.cs`](src/VirusTotalOneClick.Core/VirusTotalClient.cs)). No telemetry, no analytics, no update phone-home.
- **Upload history** is a local JSONL file (`%APPDATA%\VirusTotalOneClick\history.jsonl`) that never leaves your machine. It is optional and clearable from Settings.
- The uninstaller asks whether to remove your saved settings, history, and API key.

### About the VirusTotal public API

This app uses VirusTotal's **free public v3 API**, which is for non-commercial use and is limited to **4 requests per minute and 500 per day**. The app respects this by uploading sequentially and retrying when rate-limited.

More important: **files you upload become part of VirusTotal's shared corpus** and are available to security researchers and premium customers. This is public scanning, not a private malware check. Don't upload confidential documents, proprietary code, or anything containing personal data. The anonymizer options reduce the metadata sent, but the file content itself is always shared.

## FAQ

**Is this affiliated with VirusTotal?**
No. This is an independent open-source tool that talks to VirusTotal's public API. Not affiliated with, endorsed by, or sponsored by VirusTotal or Google.

**Why isn't it in the top level of the Windows 11 right-click menu?**
Windows 11 reserves the compact modern menu for packaged, signed apps, so the action lives one step down: right-click → **Show more options** (or select the file and press Shift+F10). If you prefer the full classic menu everywhere, Windows can be switched back with one registry key, then a sign-out or Explorer restart:

```powershell
# Classic menu on:
reg add "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" /ve /f
# Back to the Windows 11 default:
reg delete "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}" /f
```

**Is it safe to run?**
The full source is in this repository, releases are built by a public GitHub Actions workflow from tagged source, and every artifact ships with a checksum and a build-provenance attestation you can verify (see [Install](#install)). Don't take my word for it.

**Where is my API key stored, exactly?**
`%APPDATA%\VirusTotalOneClick\apikey.bin`, DPAPI-encrypted for your Windows account. Clearing the key in Settings deletes the file.

**Why does SmartScreen warn about the installer?**
Releases are not code-signed. Checksums and build-provenance attestations replace code signing (see [Install](#install)).

**Does it work on Windows 10?**
Windows 11 is the primary target. Windows 10 version 1809 (build 17763) or newer is best-effort: everything functions, and the newer window materials fall back gracefully to solid backgrounds. Older builds are not supported, and the installer refuses them.

**How do I update?**
Run the new installer over the old version. Your settings, history, and API key stay in place. Portable users: replace the folder's contents (run `uninstall-shell` first if you're also moving the folder, then `install-shell` from the new location).

**How do I fully remove the portable version?**
Run `.\VirusTotalOneClick.exe uninstall-shell`, delete the folder, and if you also want your settings, history, and API key gone, delete `%APPDATA%\VirusTotalOneClick`.

## Scope and maintenance

This is a small, focused utility maintained by one person in spare time. Bug reports and PRs are welcome and I'll respond as time allows, but there is no SLA, and the scope stays deliberately narrow: right-click → VirusTotal, done well. See [CONTRIBUTING.md](CONTRIBUTING.md) if you'd like to help.

## License

[MIT](LICENSE) © ArteenHD

Not affiliated with, endorsed by, or sponsored by VirusTotal or Google. VirusTotal is a trademark of Google LLC. Requires your own VirusTotal API key.
