param([switch]$Package)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & dotnet restore Lutra.sln --locked-mode
    if ($LASTEXITCODE) { throw 'Restore failed.' }
    & dotnet build Lutra.sln -c Release --no-restore --nologo
    if ($LASTEXITCODE) { throw 'Build failed.' }
    & dotnet run --project tests/Lutra.Tests -c Release --no-build
    if ($LASTEXITCODE) { throw 'Core tests failed.' }
    & dotnet run --project tests/Lutra.App.Tests -c Release --no-build
    if ($LASTEXITCODE) { throw 'UI tests failed.' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Update-Installer.Tests.ps1
    if ($LASTEXITCODE) { throw 'Updater checks failed.' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Update-Transaction.Tests.ps1
    if ($LASTEXITCODE) { throw 'Updater transaction checks failed.' }
    if ($Package) {
        [xml]$properties = Get-Content -LiteralPath Directory.Build.props -Raw
        $version = [string]$properties.Project.PropertyGroup.Version
        $runtimeVersion = [string]$properties.Project.PropertyGroup.LutraRuntimeVersion
        if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$' -or $runtimeVersion -notmatch '^10\.0\.\d+$') { throw 'Invalid package or runtime version.' }
        $out = Join-Path $root ('artifacts/Lutra-' + $version + '-win-x64-' + (Get-Date -Format 'yyyyMMddHHmmss'))
        & dotnet publish src/Lutra.App -c Release -r win-x64 --self-contained true -o $out "-p:RuntimeFrameworkVersion=$runtimeVersion" -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE) { throw 'App publish failed.' }
        & dotnet publish src/Lutra.Service -c Release -r win-x64 --self-contained true -o $out "-p:RuntimeFrameworkVersion=$runtimeVersion" -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE) { throw 'Service publish failed.' }
        foreach ($application in @('Lutra', 'Lutra.Service')) {
            $actualVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $out "$application.dll")).ProductVersion.Split('+')[0]
            if ($actualVersion -cne $version) { throw "Packaged application version mismatch: $application" }
            $runtime = (Get-Content -LiteralPath (Join-Path $out "$application.runtimeconfig.json") -Raw | ConvertFrom-Json).runtimeOptions
            if ($runtime.tfm -ne 'net10.0' -or -not $runtime.includedFrameworks -or @($runtime.includedFrameworks | Where-Object { $_.version -ne $runtimeVersion }).Count) { throw "Packaged runtime mismatch: $application" }
        }
        New-Item -ItemType Directory -Path (Join-Path $out 'tools') -Force | Out-Null
        Copy-Item -LiteralPath tools/Install-Lutra.ps1,tools/Uninstall-Lutra.ps1,tools/Update-Lutra.ps1,tools/Recover-LutraUpdate.ps1 -Destination (Join-Path $out 'tools')
        Copy-Item -LiteralPath README.md,DESIGN.md,CONTRIBUTING.md,SECURITY.md,LICENSE,THIRD-PARTY-NOTICES.md -Destination $out
        Copy-Item -LiteralPath docs,licenses,assets -Destination $out -Recurse
        $packageFolders = (Get-Content src/Lutra.App/obj/project.assets.json -Raw | ConvertFrom-Json).packageFolders.PSObject.Properties.Name
        foreach ($runtime in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')) {
            $runtimeRoot = $packageFolders | ForEach-Object { Join-Path $_ "$runtime/$runtimeVersion" } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
            if (-not $runtimeRoot) { throw "Runtime package unavailable for notice collection: $runtime" }
            $licenseFiles = @(Get-ChildItem -LiteralPath $runtimeRoot -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\.TXT)?$' })
            if (-not $licenseFiles.Count) { throw "Runtime license missing: $runtime" }
            foreach ($file in $licenseFiles) {
                Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $out ('licenses/' + $runtime + '-' + $file.Name))
            }
        }
        Get-ChildItem -LiteralPath $out -Recurse -File | Get-FileHash -Algorithm SHA256 | Select-Object Hash,@{Name='File';Expression={$_.Path.Substring($out.Length+1)}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'sha256.json') -Encoding UTF8
        $zip = Join-Path $root ('artifacts/Lutra-' + $version + '-win-x64.zip')
        if (Test-Path -LiteralPath $zip) { throw "Package already exists: $zip. Move it aside before building again." }
        Compress-Archive -LiteralPath $out -DestinationPath $zip
        Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
        Write-Output "Package: $zip"
    }
}
finally { Pop-Location }

