[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Archive,
    [Parameter(Mandatory=$true)][ValidatePattern('^v?[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')][string]$Tag,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory=$true)][int]$ParentId,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{32}$')][string]$JobId
)
# This script must be run from the existing, administrator-protected installation.
# No code from a downloaded package runs until independent GitHub/hash/ZIP checks have completed.
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Lutra'))
$data = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'Lutra'))
$jobRoot = Join-Path $data ('updates\' + $JobId)
$prepared = $target + '.update-' + $JobId
$backup = $target + '.rollback-' + $JobId
$failed = $target + '.failed-' + $JobId
$swapped = $false
$stopped = $false
$lock = $null
$result = $null
$script:originalConfig = $null
$recoveryRegistered = $false
$terminal = $false
$taskName = 'Lutra Update Recovery ' + $JobId

function Write-DurableText([string]$Path, [string]$Text) {
    $temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    $stream = [IO.File]::Open($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($temporary,$Path,[NullString]::Value) }
    else { [IO.File]::Move($temporary,$Path) }
}
function Write-DurableJson([string]$Path, $Value) { Write-DurableText $Path ($Value | ConvertTo-Json -Depth 30) }
function Copy-DurableFile([string]$Source, [string]$Destination) {
    $bytes=[IO.File]::ReadAllBytes($Source)
    $stream=[IO.File]::Open($Destination,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
function Set-UpdatePhase([string]$Phase) {
    $journalPath = Join-Path $jobRoot 'journal.json'
    $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
    $journal.Phase = $Phase; $journal.Time = [DateTimeOffset]::UtcNow.ToString('O')
    Write-DurableJson $journalPath $journal
}
function Assert-OriginalBinaries([string]$Directory) {
    Assert-PlainPath $Directory
    $manifest = Get-Content -LiteralPath (Join-Path $jobRoot 'old-binaries.json') -Raw | ConvertFrom-Json
    if (@($manifest).Count -eq 0) { throw 'Original binary manifest is missing.' }
    foreach ($entry in $manifest) {
        $path = [IO.Path]::GetFullPath((Join-Path $Directory $entry.Path))
        if (-not $path.StartsWith($Directory + '\',[StringComparison]::OrdinalIgnoreCase) -or $entry.Hash -notmatch '\A[A-Fa-f0-9]{64}\z') { throw 'Invalid original binary manifest.' }
        Assert-PlainPath $path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $entry.Hash) { throw ('Original binary verification failed: ' + $entry.Path) }
    }
}
function Register-Recovery {
    $recoverySource = Join-Path $PSScriptRoot 'Recover-LutraUpdate.ps1'
    if (-not (Test-Path -LiteralPath $recoverySource -PathType Leaf)) { throw 'The installed recovery tool is missing.' }
    Write-DurableText (Join-Path $jobRoot 'Recover.ps1') ([IO.File]::ReadAllText($recoverySource))
    $tokens=$null; $errors=$null
    $ast=[System.Management.Automation.Language.Parser]::ParseFile($PSCommandPath,[ref]$tokens,[ref]$errors)
    $common = @()
    foreach ($name in @('Write-DurableText','Write-DurableJson','Copy-DurableFile','Set-UpdatePhase','Assert-OriginalBinaries','Assert-PlainPath','Read-State','Assert-FreshReading','Assert-Healthy','Wait-Healthy','Stop-Control')) {
        $definition = $ast.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$false) | Select-Object -First 1
        if (-not $definition) { throw ('Recovery helper is missing: ' + $name) }
        $common += $definition.Extent.Text
    }
    Write-DurableText (Join-Path $jobRoot 'Recovery.Common.ps1') ($common -join "`r`n")
    $manifest = @(Get-ChildItem -LiteralPath $target -File -Recurse -Force | ForEach-Object {
        Assert-PlainPath $_.FullName
        @{Path=$_.FullName.Substring($target.Length+1);Hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    Write-DurableJson (Join-Path $jobRoot 'old-binaries.json') $manifest
    Write-DurableJson (Join-Path $jobRoot 'journal.json') @{JobId=$JobId;Phase='prepared';Version=$version;ConfigurationHash=(Get-FileHash -LiteralPath (Join-Path $jobRoot 'configuration.json') -Algorithm SHA256).Hash;OwnerHash=(Get-FileHash -LiteralPath (Join-Path $jobRoot 'owner.sid') -Algorithm SHA256).Hash;Time=[DateTimeOffset]::UtcNow.ToString('O')}
    $systemDirectory = [Environment]::GetFolderPath('System')
    $powershell = Join-Path $systemDirectory 'WindowsPowerShell\v1.0\powershell.exe'
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $jobRoot 'Recover.ps1') + '" -JobId ' + $JobId
    $action = New-ScheduledTaskAction -Execute $powershell -Argument $arguments -WorkingDirectory $systemDirectory
    $triggers = @((New-ScheduledTaskTrigger -AtStartup),(New-ScheduledTaskTrigger -Once -At ([DateTime]::Now.AddMinutes(1)) -RepetitionInterval ([TimeSpan]::FromMinutes(1))))
    $principal = New-ScheduledTaskPrincipal -UserId 'S-1-5-18' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::FromMinutes(5))
    Register-ScheduledTask -TaskName $taskName -TaskPath '\' -Action $action -Trigger $triggers -Principal $principal -Settings $settings -Description 'Restores cooling control if a Lutra application update is interrupted.' | Out-Null
    $script:recoveryRegistered = $true
    $task = Get-ScheduledTask -TaskName $taskName -TaskPath '\'
    if ($task.Actions[0].Execute -ine $powershell -or $task.Actions[0].Arguments -cne $arguments -or $task.Principal.UserId -notin @('SYSTEM','S-1-5-18') -or $task.Triggers.Count -ne 2 -or $task.State -eq 'Disabled') { throw 'Recovery task verification failed.' }
}

function Compare-ReleaseVersion([string]$Left, [string]$Right) {
    $pattern = '\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z'
    $a = [regex]::Match($Left,$pattern); $b = [regex]::Match($Right,$pattern)
    if (-not $a.Success -or -not $b.Success) { throw 'An installed or proposed version is invalid.' }
    for ($i = 1; $i -le 3; $i++) {
        $x = $a.Groups[$i].Value; $y = $b.Groups[$i].Value
        $c = $x.Length.CompareTo($y.Length)
        if ($c -eq 0) { $c = [string]::CompareOrdinal($x,$y) }
        if ($c -ne 0) { return $c }
    }
    $ap = $a.Groups[4].Value; $bp = $b.Groups[4].Value
    if (-not $ap -or -not $bp) { return ([int](-not $ap)).CompareTo([int](-not $bp)) }
    $ax = $ap.Split('.'); $bx = $bp.Split('.')
    foreach ($identifier in @($ax) + @($bx)) { if ($identifier -match '^0[0-9]+$') { throw 'Numeric preview identifiers cannot have leading zeroes.' } }
    for ($i = 0; $i -lt [Math]::Min($ax.Length,$bx.Length); $i++) {
        $an = $ax[$i] -match '^[0-9]+$'; $bn = $bx[$i] -match '^[0-9]+$'
        if ($an -and $bn) { $c = $ax[$i].Length.CompareTo($bx[$i].Length); if ($c -eq 0) { $c = [string]::CompareOrdinal($ax[$i],$bx[$i]) } }
        elseif ($an -ne $bn) { if ($an) { $c = -1 } else { $c = 1 } }
        else { $c = [string]::CompareOrdinal($ax[$i],$bx[$i]) }
        if ($c -ne 0) { return $c }
    }
    return $ax.Length.CompareTo($bx.Length)
}
function Assert-PlainPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse points are not allowed: $current" }
        }
        $parent = [IO.Directory]::GetParent($current)
        if (-not $parent) { break }
        $current = $parent.FullName
    }
}
function Assert-NoPendingUpdate([string]$UpdatesDirectory, [string]$CurrentJobId) {
    # Hold update.lock while calling this: a pending recovery remains a transaction even between retries.
    Assert-PlainPath $UpdatesDirectory
    if (-not (Test-Path -LiteralPath $UpdatesDirectory)) { return }
    foreach ($job in @(Get-ChildItem -LiteralPath $UpdatesDirectory -Directory -Force)) {
        if ($job.Name -ieq $CurrentJobId) { continue }
        Assert-PlainPath $job.FullName
        $journalPath = Join-Path $job.FullName 'journal.json'
        if (-not (Test-Path -LiteralPath $journalPath)) { continue } # Preparation failed before recovery was armed.
        Assert-PlainPath $journalPath
        try { $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json }
        catch { throw ('An earlier update journal cannot be read. Resolve it before updating: ' + $journalPath) }
        if ($job.Name -notmatch '\A[a-fA-F0-9]{32}\z' -or $null -eq $journal -or $journal -is [Array] -or
            $journal.JobId -cne $job.Name -or $journal.Phase -cnotin @('prepared','armed','old-moved','new-active','recovering','committed','rolled-back','aborted')) {
            throw ('An earlier update journal is invalid. Resolve it before updating: ' + $journalPath)
        }
        if ($journal.Phase -in @('armed','old-moved','new-active','recovering')) {
            throw ('A previous Lutra update is awaiting recovery. Wait for recovery to finish before updating again. Job: ' + $job.Name)
        }
    }
}
function Read-State {
    $path = Join-Path $data 'state.json'
    $stream = [IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try { $reader = [IO.StreamReader]::new($stream); try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() } }
    finally { $stream.Dispose() }
}
function Assert-FreshReading($Reading, [DateTimeOffset]$Now) {
    if ($null -eq $Reading.Value -or [double]::IsNaN([double]$Reading.Value) -or [double]::IsInfinity([double]$Reading.Value)) { throw 'A required sensor has no finite reading.' }
    $time=[DateTimeOffset]$Reading.Time
    if (($Now-$time).TotalSeconds -gt 5 -or $time -gt $Now.AddSeconds(2)) { throw 'A required sensor reading is stale or future-dated.' }
}
function Assert-Healthy($State, [DateTimeOffset]$Since) {
    $now = [DateTimeOffset]::UtcNow
    $stamp = ([DateTimeOffset]$State.Time)
    if ($State.Mode -ne 'Running' -or ($now-$stamp).TotalSeconds -gt 5 -or $stamp -gt $now.AddSeconds(2) -or $stamp -lt $Since) { throw 'Cooling controller has no fresh running state.' }
    if (@($State.Errors).Count -gt 0 -or @($State.Alarms).Count -gt 0) { throw 'Resolve controller errors or active alarms before updating.' }
    if (($State.Configuration | ConvertTo-Json -Depth 30 -Compress) -cne ($script:originalConfig | ConvertTo-Json -Depth 30 -Compress)) { throw 'The active control configuration does not match the saved configuration.' }
    foreach ($curve in @($script:originalConfig.Curves | Where-Object { $_.Enabled })) {
        foreach ($sensor in @($curve.SensorId,$curve.SafetySensorId | Where-Object { $_ })) {
            $reading = @($State.Values | Where-Object { $_.Id -eq $sensor })
            if ($reading.Count -ne 1 -or $null -eq $reading[0].Value -or ($now-([DateTimeOffset]$reading[0].Time)).TotalSeconds -gt 5) { throw ('Required temperature is unavailable: ' + $curve.Name) }
            Assert-FreshReading $reading[0] $now
            if ($sensor -eq $curve.SafetySensorId -and [double]$reading[0].Value -ge [double]$curve.SafetyTemperature) { throw ('Safety temperature is active: ' + $curve.Name) }
        }
        $output = @($State.Catalog.Outputs | Where-Object { $_.Id -eq $curve.OutputId -and $_.CanControl })
        if ($output.Count -ne 1) { throw ('Configured output is unavailable: ' + $curve.Name) }
        # QUADRO has a readback for every PWM output and pump tachometer. Board firmware interfaces differ.
        if ($output[0].DeviceId.StartsWith('quadro/')) {
            $duty = @($State.Values | Where-Object { $_.Id -eq ($output[0].DeviceId + '/duty/' + $output[0].Channel) })
            if ($duty.Count -ne 1 -or $null -eq $duty[0].Value -or ($now-([DateTimeOffset]$duty[0].Time)).TotalSeconds -gt 5) { throw ('Output readback is unavailable: ' + $curve.Name) }
            Assert-FreshReading $duty[0] $now
            if ($curve.IsPump) {
                $speed = @($State.Values | Where-Object { $_.Id -eq ($output[0].DeviceId + '/rpm/' + $output[0].Channel) })
                if ($speed.Count -ne 1 -or $null -eq $speed[0].Value -or [double]$speed[0].Value -le 0 -or ($now-([DateTimeOffset]$speed[0].Time)).TotalSeconds -gt 5) { throw ('Pump tachometer is unavailable: ' + $curve.Name) }
                Assert-FreshReading $speed[0] $now
            }
        }
    }
}
function Wait-Healthy([DateTimeOffset]$Since) {
    $until = [DateTimeOffset]::UtcNow.AddSeconds(65)
    $consecutive = 0; $reason = 'Waiting for the controller.'
    while ([DateTimeOffset]::UtcNow -lt $until) {
        try {
            if ((Get-Service -Name LutraControl).Status -ne 'Running') { throw 'Cooling service is not running.' }
            Assert-Healthy (Read-State) $Since
            $consecutive++
            if ($consecutive -ge 5) { return }
        } catch { $reason = $_.Exception.Message; $consecutive = 0 }
        Start-Sleep -Milliseconds 1200
    }
    throw ('Cooling health verification failed: ' + $reason)
}
function Stop-Control {
    $service = Get-Service -Name LutraControl
    if ($service.Status -ne 'Stopped') { Stop-Service -Name LutraControl; $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(25)) }
    if (Get-Process -Name 'Lutra.Service' -ErrorAction SilentlyContinue) { throw 'The cooling process did not stop cleanly.' }
}
function Copy-Tree([string]$From, [string]$To) {
    Assert-PlainPath $From; Assert-PlainPath $To
    foreach ($entry in @(Get-ChildItem -LiteralPath $From -Recurse -Force)) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release directories cannot contain links.' }
    }
    New-Item -ItemType Directory -Path $To | Out-Null
    Get-ChildItem -LiteralPath $From -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $To -Recurse -Force }
}

try {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator approval is required to update the installed cooling service.' }
    if ([IO.Path]::GetFullPath($PSScriptRoot) -ne (Join-Path $target 'tools')) { throw 'Run the updater from the installed Program Files\Lutra\tools directory.' }
    Assert-PlainPath $target; Assert-PlainPath $data; Assert-PlainPath $jobRoot
    if (Test-Path -LiteralPath $jobRoot) { throw 'Update job already exists.' }
    New-Item -ItemType Directory -Path $jobRoot | Out-Null
    # Keep downloaded data and rollback metadata protected from modification across the UAC boundary.
    $acl = [Security.AccessControl.DirectorySecurity]::new(); $acl.SetAccessRuleProtection($true,$false)
    foreach ($sid in @('S-1-5-18','S-1-5-32-544')) { $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow')) }
    $owner = [Security.Principal.SecurityIdentifier]::new([IO.File]::ReadAllText((Join-Path $data 'owner.sid')).Trim())
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($owner,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'))
    Set-Acl -LiteralPath $jobRoot -AclObject $acl
    $lock = [IO.File]::Open((Join-Path $data 'update.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    Assert-NoPendingUpdate (Join-Path $data 'updates') $JobId
    $version = $Tag.TrimStart('v'); $assetName = 'Lutra-' + $version + '-win-x64.zip'
    $installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $target 'Lutra.dll')).ProductVersion
    if ((Compare-ReleaseVersion $version $installedVersion) -le 0) { throw 'The selected release is not newer than the installed application.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri ('https://api.github.com/repos/Resnais-Beefhair/lutra/releases/tags/' + [Uri]::EscapeDataString($Tag)) -Headers @{'User-Agent'='Lutra-Updater';'Accept'='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28'} -TimeoutSec 30
    $asset = @($release.assets | Where-Object { $_.name -ceq $assetName -and $_.state -ceq 'uploaded' })
    if ($release.draft -or $release.tag_name -cne $Tag -or $release.html_url -cne ('https://github.com/Resnais-Beefhair/lutra/releases/tag/' + $Tag) -or $asset.Count -ne 1) { throw 'Release identity verification failed.' }
    if ($asset[0].digest -notmatch '^sha256:[a-fA-F0-9]{64}$' -or $asset[0].digest.Substring(7) -ine $ExpectedSha256 -or $asset[0].browser_download_url -cne ('https://github.com/Resnais-Beefhair/lutra/releases/download/' + $Tag + '/' + $assetName)) { throw 'GitHub release digest or asset identity changed. Check for updates again.' }
    if ($asset[0].size -le 0 -or $asset[0].size -gt 268435456) { throw 'Release package size is invalid.' }
    $protectedZip = Join-Path $jobRoot 'release.zip'
    $inputStream = [IO.File]::Open([IO.Path]::GetFullPath($Archive),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try {
        if ($inputStream.Length -ne $asset[0].size) { throw 'Release package size changed.' }
        $outputStream = [IO.File]::Open($protectedZip,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
    } finally { $inputStream.Dispose() }
    if ((Get-FileHash -LiteralPath $protectedZip -Algorithm SHA256).Hash -ine $ExpectedSha256) { throw 'Protected package checksum verification failed.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($protectedZip)
    $extract = Join-Path $jobRoot 'package'
    New-Item -ItemType Directory -Path $extract | Out-Null
    try {
        if ($zip.Entries.Count -eq 0 -or $zip.Entries.Count -gt 5000) { throw 'Invalid ZIP entry count.' }
        $total = [long]0; $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('\','/'); $parts = $name.TrimEnd('/').Split('/')
            if (-not $name -or $name.StartsWith('/') -or $name.Length -gt 220 -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or ($entry.ExternalAttributes -band 0x400) -ne 0 -or -not $seen.Add($name.TrimEnd('/'))) { throw 'Unsafe ZIP entry.' }
            foreach ($part in $parts) {
                if (-not $part -or $part -in @('.','..') -or $part -match '[:<>"|?*\x00-\x1f]' -or $part.EndsWith('.') -or $part.EndsWith(' ') -or $part.Split('.')[0] -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') { throw 'Unsafe ZIP path.' }
            }
            $total += $entry.Length
            if ($entry.Length -gt 268435456 -or $total -gt 1073741824) { throw 'Expanded package is too large.' }
        }
        foreach ($entry in $zip.Entries) {
            $destination = [IO.Path]::GetFullPath((Join-Path $extract $entry.FullName))
            if (-not $destination.StartsWith($extract + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'ZIP entry escaped staging.' }
            if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) { [IO.Directory]::CreateDirectory($destination) | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$false)
        }
    } finally { $zip.Dispose() }
    $package = $extract
    if (-not (Test-Path -LiteralPath (Join-Path $package 'Lutra.exe'))) {
        $children = @(Get-ChildItem -LiteralPath $extract -Force)
        if ($children.Count -ne 1 -or -not $children[0].PSIsContainer) { throw 'Package must have a flat root or one enclosing directory.' }
        $package = $children[0].FullName
    }
    foreach ($required in @('Lutra.exe','Lutra.dll','Lutra.Service.exe','Lutra.Service.dll','tools\Update-Lutra.ps1','tools\Recover-LutraUpdate.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $package $required) -PathType Leaf)) { throw ('Required release file missing: ' + $required) }
    }
    $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $package 'Lutra.dll')).ProductVersion.Split('+')[0]
    if ($productVersion -cne $version) { throw 'Package application version does not match the GitHub tag.' }
    $serviceVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $package 'Lutra.Service.dll')).ProductVersion.Split('+')[0]
    if ($serviceVersion -cne $version) { throw 'Package service version does not match the GitHub tag.' }
    foreach ($name in @('AquaComputerService','Vesi.Service','FanControl')) { if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw ('A competing cooling controller is running: ' + $name) } }
    $service = Get-CimInstance Win32_Service -Filter "Name='LutraControl'"
    if (-not $service -or $service.State -ne 'Running' -or $service.PathName.Trim('"') -ine (Join-Path $target 'Lutra.Service.exe')) { throw 'The installed Lutra service is not running from the expected location.' }
    $script:originalConfig = Get-Content -LiteralPath (Join-Path $data 'configuration.json') -Raw | ConvertFrom-Json
    Assert-Healthy (Read-State) ([DateTimeOffset]::UtcNow.AddSeconds(-5))
    $configHash = (Get-FileHash -LiteralPath (Join-Path $data 'configuration.json') -Algorithm SHA256).Hash
    foreach ($name in @('configuration.json','owner.sid')) { Copy-DurableFile (Join-Path $data $name) (Join-Path $jobRoot $name) }
    if ((Test-Path -LiteralPath $prepared) -or (Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $failed)) { throw 'Update staging directory already exists.' }
    Copy-Tree $package $prepared
    $monitor = Get-Process -Id $ParentId -ErrorAction SilentlyContinue
    if ($monitor -and -not $monitor.WaitForExit(60000)) { throw 'Close the Lutra monitor before updating.' }
    if (Get-Process -Name Lutra -ErrorAction SilentlyContinue) { throw 'Another Lutra monitor is still running. Exit it before updating.' }
    if ((Get-FileHash -LiteralPath (Join-Path $data 'configuration.json') -Algorithm SHA256).Hash -cne $configHash) { throw 'Configuration changed during update preparation. Retry the update.' }
    Assert-Healthy (Read-State) ([DateTimeOffset]::UtcNow.AddSeconds(-5))
    Register-Recovery
    if ((Get-FileHash -LiteralPath (Join-Path $data 'configuration.json') -Algorithm SHA256).Hash -cne $configHash) { throw 'Configuration changed before update activation.' }
    Assert-Healthy (Read-State) ([DateTimeOffset]::UtcNow.AddSeconds(-5))
    Set-UpdatePhase 'armed'
    # All network, extraction and validation work is complete. The service interruption starts here.
    # QUADRO keeps native curves and uses its saved fallback for host-fed curves after ~15 seconds.
    $stopped = $true; Stop-Control
    Assert-PlainPath $target; Assert-PlainPath $prepared; Assert-PlainPath $backup
    [IO.Directory]::Move($target,$backup)
    $swapped = $true
    Set-UpdatePhase 'old-moved'
    [IO.Directory]::Move($prepared,$target)
    Set-UpdatePhase 'new-active'
    $started = [DateTimeOffset]::UtcNow
    Start-Service -Name LutraControl
    Wait-Healthy $started
    if ((Get-FileHash -LiteralPath (Join-Path $data 'configuration.json') -Algorithm SHA256).Hash -cne $configHash) { throw 'The update unexpectedly changed the saved configuration.' }
    Set-UpdatePhase 'committed'; $terminal = $true
    $result = @{ Success=$true; Version=$version; Message=('Lutra ' + $version + ' installed. Cooling control is running; settings and history were preserved.'); Backup=$backup; Time=[DateTimeOffset]::UtcNow.ToString('O') }
}
catch {
    $reason = $_.Exception.Message; $rollback = ''
    if ($swapped) {
        try {
            Stop-Control
            Assert-PlainPath $target; Assert-PlainPath $backup; Assert-PlainPath $failed
            if (Test-Path -LiteralPath $target) { [IO.Directory]::Move($target,$failed) }
            [IO.Directory]::Move($backup,$target)
            foreach ($name in @('configuration.json','owner.sid')) { Copy-DurableFile (Join-Path $jobRoot $name) (Join-Path $data $name) }
            $started = [DateTimeOffset]::UtcNow; Start-Service -Name LutraControl; Wait-Healthy $started
            Set-UpdatePhase 'rolled-back'; $terminal = $true
            $rollback = ' The previous installation was restored and cooling control was verified.'
        } catch { $rollback = ' AUTOMATIC RESTORE NEEDS ATTENTION: ' + $_.Exception.Message + '. QUADRO retains its device fallback. Previous binaries: ' + $backup }
    } elseif ($stopped) {
        try { $started = [DateTimeOffset]::UtcNow; Start-Service -Name LutraControl; Wait-Healthy $started; Set-UpdatePhase 'rolled-back'; $terminal = $true; $rollback = ' The existing controller was restarted and verified.' }
        catch { $rollback = ' Controller restart needs attention: ' + $_.Exception.Message }
    } else { if ($recoveryRegistered) { Set-UpdatePhase 'aborted' }; $terminal = $true; $rollback = ' The installed cooling service was not stopped or changed.' }
    if ($recoveryRegistered -and -not $terminal) { $rollback += ' Protected recovery will retry every minute and at Windows startup.' }
    $result = @{ Success=$false; RecoveryPending=($recoveryRegistered -and -not $terminal); Message=('Update failed: ' + $reason + $rollback); Backup=$backup; Time=[DateTimeOffset]::UtcNow.ToString('O') }
}
finally {
    if ($result -and (Test-Path -LiteralPath $jobRoot)) {
        Write-DurableJson (Join-Path $jobRoot 'result.json') $result
    }
    if ($recoveryRegistered -and $terminal) { try { Unregister-ScheduledTask -TaskName $taskName -TaskPath '\' -Confirm:$false } catch { } }
    if ($lock) { $lock.Dispose() }
}
if (-not $result.Success) { exit 1 }
