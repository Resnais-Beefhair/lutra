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
    if ($Package) {
        $out = Join-Path $root ('artifacts/Lutra-0.4.0-preview.1-win-x64-' + (Get-Date -Format 'yyyyMMddHHmmss'))
        $runtimeVersion = '8.0.32'
        & dotnet publish src/Lutra.App -c Release -r win-x64 --self-contained true -o $out "-p:RuntimeFrameworkVersion=$runtimeVersion" -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE) { throw 'App publish failed.' }
        & dotnet publish src/Lutra.Service -c Release -r win-x64 --self-contained true -o $out "-p:RuntimeFrameworkVersion=$runtimeVersion" -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE) { throw 'Service publish failed.' }
        New-Item -ItemType Directory -Path (Join-Path $out 'tools') -Force | Out-Null
        Copy-Item -LiteralPath tools/Install-Lutra.ps1,tools/Uninstall-Lutra.ps1 -Destination (Join-Path $out 'tools')
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
        $zip = $out + '.zip'
        Compress-Archive -LiteralPath $out -DestinationPath $zip
        Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
        Write-Output "Package: $zip"
    }
}
finally { Pop-Location }

