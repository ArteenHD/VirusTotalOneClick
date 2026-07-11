#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }

BeforeAll {
    $script:RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $script:Scripts = Join-Path $script:RepoRoot 'scripts'

    function New-FakePublishFolder {
        param([string] $Root, [switch] $WithSymbols)
        New-Item -ItemType Directory -Path $Root -Force | Out-Null
        Set-Content -Path (Join-Path $Root 'VirusTotalOneClick.exe') -Value 'fake-exe-payload' -Encoding ascii
        Set-Content -Path (Join-Path $Root 'readme-fragment.txt') -Value 'payload' -Encoding ascii
        if ($WithSymbols) {
            Set-Content -Path (Join-Path $Root 'VirusTotalOneClick.pdb') -Value 'symbols' -Encoding ascii
        }
        return $Root
    }

    function Invoke-Package {
        param([string] $PublishDir, [string] $OutDir, [switch] $IncludeSymbols)
        & (Join-Path $script:Scripts 'package-windows.ps1') `
            -SkipPublish `
            -PublishPath $PublishDir `
            -PackagePath $OutDir `
            -IncludeSymbols:$IncludeSymbols
    }
}

Describe 'package-windows.ps1' {
    It 'produces a zip whose root folder contains the exe and both local scripts' {
        $publish = New-FakePublishFolder -Root (Join-Path $TestDrive 'publish1')
        $out = Join-Path $TestDrive 'pkg1'
        Invoke-Package -PublishDir $publish -OutDir $out

        $zip = Join-Path $out 'VirusTotalOneClick-win-x64.zip'
        $zip | Should -Exist
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $entries = $archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
        }
        finally { $archive.Dispose() }
        $entries | Should -Contain 'VirusTotalOneClick-win-x64/VirusTotalOneClick.exe'
        $entries | Should -Contain 'VirusTotalOneClick-win-x64/install-local.ps1'
        $entries | Should -Contain 'VirusTotalOneClick-win-x64/uninstall-local.ps1'
    }

    It 'writes a sha256 checksum file matching the zip, computed as lowercase hex + two spaces + filename' {
        $publish = New-FakePublishFolder -Root (Join-Path $TestDrive 'publish2')
        $out = Join-Path $TestDrive 'pkg2'
        Invoke-Package -PublishDir $publish -OutDir $out

        $zip = Join-Path $out 'VirusTotalOneClick-win-x64.zip'
        $checksum = "$zip.sha256"
        $checksum | Should -Exist
        $line = (Get-Content -Path $checksum -Raw).Trim()
        $line | Should -Match '^[0-9a-f]{64}  VirusTotalOneClick-win-x64\.zip$'

        $stream = [System.IO.File]::OpenRead($zip)
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $expected = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose() }
        }
        finally { $stream.Dispose() }
        ($line -split '\s+')[0] | Should -Be $expected
    }

    It 'strips pdb files from the package by default' {
        $publish = New-FakePublishFolder -Root (Join-Path $TestDrive 'publish3') -WithSymbols
        $out = Join-Path $TestDrive 'pkg3'
        Invoke-Package -PublishDir $publish -OutDir $out

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $out 'VirusTotalOneClick-win-x64.zip'))
        try {
            $pdbs = $archive.Entries | Where-Object { $_.FullName.EndsWith('.pdb') }
        }
        finally { $archive.Dispose() }
        $pdbs | Should -BeNullOrEmpty
    }

    It 'keeps pdb files and still verifies when -IncludeSymbols is passed' {
        $publish = New-FakePublishFolder -Root (Join-Path $TestDrive 'publish4') -WithSymbols
        $out = Join-Path $TestDrive 'pkg4'
        { Invoke-Package -PublishDir $publish -OutDir $out -IncludeSymbols } | Should -Not -Throw

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $out 'VirusTotalOneClick-win-x64.zip'))
        try {
            $pdbs = $archive.Entries | Where-Object { $_.FullName.EndsWith('.pdb') }
        }
        finally { $archive.Dispose() }
        $pdbs | Should -Not -BeNullOrEmpty
    }
}

Describe 'verify-package.ps1' {
    BeforeEach {
        $script:publish = New-FakePublishFolder -Root (Join-Path $TestDrive "vp-publish-$([guid]::NewGuid().ToString('N'))")
        $script:out = Join-Path $TestDrive "vp-out-$([guid]::NewGuid().ToString('N'))"
        Invoke-Package -PublishDir $script:publish -OutDir $script:out
        $script:zip = Join-Path $script:out 'VirusTotalOneClick-win-x64.zip'
        $script:checksum = "$script:zip.sha256"
    }

    It 'passes a well-formed package and prints the verified line' {
        $output = & (Join-Path $script:Scripts 'verify-package.ps1') -PackagePath $script:zip -ChecksumPath $script:checksum
        $LASTEXITCODE | Should -Be 0
        ($output | Out-String) | Should -Match 'Package verified'
    }

    It 'fails when the checksum does not match' {
        Set-Content -Path $script:checksum -Value ("{0}  VirusTotalOneClick-win-x64.zip" -f ('0' * 64)) -Encoding ascii
        { & (Join-Path $script:Scripts 'verify-package.ps1') -PackagePath $script:zip -ChecksumPath $script:checksum } |
            Should -Throw '*checksum mismatch*'
    }

    It 'fails when a required root entry is missing' {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::Open($script:zip, 'Update')
        try {
            ($archive.Entries | Where-Object { $_.FullName -like '*install-local.ps1' } | Select-Object -First 1).Delete()
        }
        finally { $archive.Dispose() }
        $stream = [System.IO.File]::OpenRead($script:zip)
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $hash = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose() }
        }
        finally { $stream.Dispose() }
        Set-Content -Path $script:checksum -Value "$hash  VirusTotalOneClick-win-x64.zip" -Encoding ascii

        { & (Join-Path $script:Scripts 'verify-package.ps1') -PackagePath $script:zip -ChecksumPath $script:checksum } |
            Should -Throw '*missing required entry*'
    }

    It 'fails when a pdb is present unless -AllowSymbols' {
        $withSymbols = New-FakePublishFolder -Root (Join-Path $TestDrive 'vp-sym-publish') -WithSymbols
        $symOut = Join-Path $TestDrive 'vp-sym-out'
        Invoke-Package -PublishDir $withSymbols -OutDir $symOut -IncludeSymbols
        $symZip = Join-Path $symOut 'VirusTotalOneClick-win-x64.zip'

        { & (Join-Path $script:Scripts 'verify-package.ps1') -PackagePath $symZip -ChecksumPath "$symZip.sha256" } |
            Should -Throw '*symbol*'
        { & (Join-Path $script:Scripts 'verify-package.ps1') -PackagePath $symZip -ChecksumPath "$symZip.sha256" -AllowSymbols } |
            Should -Not -Throw
    }
}

Describe 'installer definition' {
    BeforeAll {
        $script:Iss = Get-Content -Raw (Join-Path $script:RepoRoot 'installer\VirusTotalOneClick.iss')
    }

    It 'installs per-user without elevation' {
        $script:Iss | Should -Match 'PrivilegesRequired=lowest'
        $script:Iss | Should -Match '\{localappdata\}\\Programs\\VirusTotalOneClick'
    }

    It 'delegates shell integration to the app verbs' {
        $script:Iss | Should -Match 'Parameters:\s*"install-shell"'
        $script:Iss | Should -Match 'Parameters:\s*"uninstall-shell"'
    }

    It 'never prompts for data removal in silent uninstalls' {
        $script:Iss | Should -Match 'UninstallSilent'
    }
}
