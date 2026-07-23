# Contributing

Thanks for your interest! This is a small utility with a deliberately narrow scope — right-click → VirusTotal. Fixes, polish, and improvements are welcome.

## Prerequisites

- Windows 11 (Windows 10 works for building; UI verification should happen on 11)
- .NET SDK matching [`global.json`](global.json) — `dotnet --version` should agree
- PowerShell 7+ (`pwsh`)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) — only if you're working on the installer

## Getting started

```powershell
git clone https://github.com/ArteenHD/VirusTotalOneClick.git
cd VirusTotalOneClick
pwsh scripts/setup-hooks.ps1   # installs the commit-message hook
pwsh scripts/validate.ps1      # the gate: restore, format check, build, tests
```

`scripts/validate.ps1` is the single source of truth for "does this build pass" — it runs a locked-mode NuGet restore, `dotnet format` verification, a build with warnings as errors, and both test suites. CI runs exactly the same script, so if it's green locally it's green in CI.

Installer changes have their own suite: `pwsh scripts/test-packaging.ps1` (Pester).

To try your build end-to-end: `pwsh scripts/install-local.ps1` publishes and installs the app for your user (and `uninstall-local.ps1` reverses it).

## Pull requests

- Keep PRs focused — one fix or improvement per PR.
- `scripts/validate.ps1` must pass.
- New behavior needs tests. Core logic is tested headlessly in `tests/VirusTotalOneClick.Core.Tests`; viewmodels and window smoke tests live in `tests/VirusTotalOneClick.App.Tests`.
- Commit messages are conventional: `type(scope): subject` — lowercase, imperative, ≤72 chars. Scopes in use: `core`, `app`, `ui`, `shell`, `vt`, `installer`, `ci`, `docs`, `tooling` (`deps` is reserved for automated dependency updates). The hook and CI both lint this.

## Developer notes (WPF / WPF-UI)

Hard-won rules this codebase follows — please keep to them:

- **Theme brushes via `DynamicResource`, always.** `StaticResource` freezes the light-theme value and breaks dark mode.
- **Icons are `ui:SymbolIcon` only** (WPF-UI's bundled Fluent System Icons, MIT). No Segoe Fluent Icons font glyphs — that font is not redistributable — and no PNG glyph sets.
- **The system accent is the accent.** Don't introduce brand-colored brushes into control chrome; brand identity lives in the app icon.
- **`ListView`/`GridView` need explicit theming.** The classic WPF `GridView` doesn't pick up WPF-UI's theme resources by itself — see the implicit `GridViewColumnHeader`/`ListViewItem` styles in [`App.xaml`](src/VirusTotalOneClick/App.xaml) before styling a new list.
- **WPF-UI is version-pinned** in the csproj (with a NuGet lock file). Don't bump it casually; controls have changed behavior between minor versions. A bump PR should include a pass over every window in light and dark.
- **Window tests are STA.** UI smoke tests run through the STA helper in the App test project; follow the existing test patterns when adding window coverage.
- **Regexes validating whole strings anchor with `\A...\z`**, not `^...$` — `$` tolerates a trailing newline.

## Reporting bugs

Use the bug report template and include your Windows and app versions plus steps to reproduce.

## Security issues

Please don't open public issues for vulnerabilities — see [SECURITY.md](SECURITY.md).
