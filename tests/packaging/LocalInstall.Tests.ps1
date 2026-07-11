#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }

BeforeAll {
    $script:RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $script:Scripts = Join-Path $script:RepoRoot 'scripts'
}

Describe 'install-local.ps1' {
    It 'resolves the exe next to itself so the extracted package is relocatable' {
        $staged = Join-Path $TestDrive 'staged'
        New-Item -ItemType Directory -Path $staged -Force | Out-Null
        Set-Content -Path (Join-Path $staged 'VirusTotalOneClick.exe') -Value 'fake' -Encoding ascii
        Copy-Item (Join-Path $script:Scripts 'install-local.ps1') $staged

        $target = Join-Path $TestDrive 'installed'
        & (Join-Path $staged 'install-local.ps1') -InstallPath $target -SkipShellIntegration

        (Join-Path $target 'VirusTotalOneClick.exe') | Should -Exist
    }

    It 'throws when the source has no exe' {
        $empty = Join-Path $TestDrive 'empty-src'
        New-Item -ItemType Directory -Path $empty -Force | Out-Null
        { & (Join-Path $script:Scripts 'install-local.ps1') -SourcePath $empty -InstallPath (Join-Path $TestDrive 'nowhere') -SkipShellIntegration } |
            Should -Throw '*Cannot find VirusTotalOneClick.exe*'
    }

    It 'fails the install when the shell-integration child process exits non-zero' {
        # A renamed cmd.exe stands in for the app: it rejects the install-shell
        # argument and exits non-zero, which the script must surface as a failure.
        $staged = Join-Path $TestDrive 'failing-src'
        New-Item -ItemType Directory -Path $staged -Force | Out-Null
        Copy-Item (Join-Path $env:SystemRoot 'System32\cmd.exe') (Join-Path $staged 'VirusTotalOneClick.exe')

        $target = Join-Path $TestDrive 'failing-install'
        { & (Join-Path $script:Scripts 'install-local.ps1') -SourcePath $staged -InstallPath $target -ShellIntegrationTimeoutSeconds 3 } |
            Should -Throw '*Shell integration install failed*'
    }
}

Describe 'uninstall-local.ps1' {
    It 'refuses to remove a folder outside the default per-user install location' {
        $decoy = Join-Path $TestDrive 'decoy\VirusTotalOneClick'
        New-Item -ItemType Directory -Path $decoy -Force | Out-Null
        { & (Join-Path $script:Scripts 'uninstall-local.ps1') -InstallPath $decoy -SkipShellIntegration } |
            Should -Throw '*Refusing to remove*'
        $decoy | Should -Exist
    }

    It 'refuses to remove app data outside the expected app-data location' {
        $install = Join-Path $TestDrive 'decoy2\VirusTotalOneClick'
        New-Item -ItemType Directory -Path $install -Force | Out-Null
        $decoyData = Join-Path $TestDrive 'decoy-data\VirusTotalOneClick'
        New-Item -ItemType Directory -Path $decoyData -Force | Out-Null
        { & (Join-Path $script:Scripts 'uninstall-local.ps1') -InstallPath $install -SkipShellIntegration -RemoveAppData -AppDataPath $decoyData -KeepAppFiles } |
            Should -Throw '*Refusing to remove*'
        $decoyData | Should -Exist
    }

    It 'fails when the shell-integration child process exits non-zero' {
        $install = Join-Path $TestDrive 'un-failing\VirusTotalOneClick'
        New-Item -ItemType Directory -Path $install -Force | Out-Null
        Copy-Item (Join-Path $env:SystemRoot 'System32\cmd.exe') (Join-Path $install 'VirusTotalOneClick.exe')
        { & (Join-Path $script:Scripts 'uninstall-local.ps1') -InstallPath $install -KeepAppFiles -ShellIntegrationTimeoutSeconds 3 } |
            Should -Throw '*Shell integration uninstall failed*'
    }
}
