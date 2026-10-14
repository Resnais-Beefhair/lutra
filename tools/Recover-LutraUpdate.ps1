[CmdletBinding()]
param([Parameter(Mandatory=$true)][ValidatePattern('\A[a-fA-F0-9]{32}\z')][string]$JobId)
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Lutra'))
$data = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'Lutra'))
$jobRoot = Join-Path $data ('updates\' + $JobId)
$backup = $target + '.rollback-' + $JobId
$taskName = 'Lutra Update Recovery ' + $JobId
$lock = $null
$result = $null
$terminal = $false
function Get-TerminalRecoveryResult([string]$Phase, $Previous, [string]$Version) {
    $success = $Phase -eq 'committed'
    if ($null -ne $Previous -and $Previous -isnot [Array] -and $Previous.Success -is [bool] -and
        $Previous.Success -eq $success -and -not $Previous.RecoveryPending -and
        $Previous.Message -is [string] -and -not [string]::IsNullOrWhiteSpace($Previous.Message)) { return $Previous }
    $message = if ($success) { 'The Lutra update completed and cooling control was verified.' }
        elseif ($Phase -eq 'rolled-back') { 'The interrupted update was rolled back and cooling control was verified.' }
        else { 'The update ended before replacing the installed controller.' }
    return @{Success=$success;RecoveryPending=$false;Version=$Version;Message=$message;Time=[DateTimeOffset]::UtcNow.ToString('O')}
}
# Bootstrap only from the administrator-protected job location; no paths or code come from the ZIP.
if ([IO.Path]::GetFullPath($PSScriptRoot) -ine $jobRoot) { throw 'Recovery must run from its protected update job.' }
$current = $jobRoot
while ($current) {
    if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Recovery path cannot contain links.' }
    $parent=[IO.Directory]::GetParent($current); if (-not $parent) { break }; $current=$parent.FullName
}
. (Join-Path $jobRoot 'Recovery.Common.ps1')
try { $lock=[IO.File]::Open((Join-Path $data 'update.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
catch [IO.IOException] { exit 0 } # A live updater/recovery owns the transaction. The scheduled task retries.
try {
    $journal = Get-Content -LiteralPath (Join-Path $jobRoot 'journal.json') -Raw | ConvertFrom-Json
    if ($journal.JobId -cne $JobId -or $journal.Phase -notin @('prepared','armed','old-moved','new-active','recovering','committed','rolled-back','aborted')) { throw 'Recovery journal is invalid.' }
    if ($journal.Phase -in @('committed','rolled-back','aborted','prepared')) {
        $terminal=$true
        $previous = $null
        if (Test-Path -LiteralPath (Join-Path $jobRoot 'result.json')) {
            try { $previous = Get-Content -LiteralPath (Join-Path $jobRoot 'result.json') -Raw | ConvertFrom-Json } catch { }
        }
        $result = Get-TerminalRecoveryResult $journal.Phase $previous $journal.Version
    } else {
        Set-UpdatePhase 'recovering'
        Assert-PlainPath $target; Assert-PlainPath $backup
        $script:originalConfig=Get-Content -LiteralPath (Join-Path $jobRoot 'configuration.json') -Raw | ConvertFrom-Json
        if ($script:originalConfig.Version -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $jobRoot 'configuration.json') -Algorithm SHA256).Hash -ine $journal.ConfigurationHash -or (Get-FileHash -LiteralPath (Join-Path $jobRoot 'owner.sid') -Algorithm SHA256).Hash -ine $journal.OwnerHash) { throw 'The saved recovery configuration is missing or damaged.' }
        [Security.Principal.SecurityIdentifier]::new([IO.File]::ReadAllText((Join-Path $jobRoot 'owner.sid')).Trim()) | Out-Null
        if (Test-Path -LiteralPath $backup) { Assert-OriginalBinaries $backup } else { Assert-OriginalBinaries $target }
        Stop-Control
        if (Test-Path -LiteralPath $backup) {
            Assert-OriginalBinaries $backup
            if (Test-Path -LiteralPath $target) {
                $failed=$target + '.failed-' + $JobId + '-' + [Guid]::NewGuid().ToString('N')
                Assert-PlainPath $failed
                [IO.Directory]::Move($target,$failed)
            }
            [IO.Directory]::Move($backup,$target)
        }
        # Handles a killed updater before the first rename or a killed recovery after restoring it.
        Assert-OriginalBinaries $target
        foreach ($name in @('configuration.json','owner.sid')) { Copy-DurableFile (Join-Path $jobRoot $name) (Join-Path $data $name) }
        $started=[DateTimeOffset]::UtcNow; Start-Service -Name LutraControl; Wait-Healthy $started
        Set-UpdatePhase 'rolled-back'; $terminal=$true
        $result=@{Success=$false;RecoveryPending=$false;Message='An interrupted update was recovered automatically. The previous installation was restored and cooling control was verified.';Time=[DateTimeOffset]::UtcNow.ToString('O')}
    }
} catch {
    $result=@{Success=$false;RecoveryPending=$true;Message=('Interrupted update recovery needs attention: '+$_.Exception.Message+'. Recovery will retry every minute and at Windows startup.');Time=[DateTimeOffset]::UtcNow.ToString('O')}
} finally {
    if ($result) { Write-DurableJson (Join-Path $jobRoot 'result.json') $result }
    if ($terminal) { try { Unregister-ScheduledTask -TaskName $taskName -TaskPath '\' -Confirm:$false } catch { } }
    if ($lock) { $lock.Dispose() }
}
if (-not $terminal) { exit 1 }
