# Pure function and temporary journal tests. This never invokes the updater, SCM, tasks or real device state.
$ErrorActionPreference = 'Stop'
$tokens = $null; $errors = $null
$source = Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\Update-Lutra.ps1'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
foreach ($functionName in @('Compare-ReleaseVersion','Assert-FreshReading','Assert-Healthy','Assert-PlainPath','Assert-NoPendingUpdate')) {
    $definition = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName },$false) | Select-Object -First 1
    if (-not $definition) { throw ('Updater function missing: ' + $functionName) }
    Invoke-Expression $definition.Extent.Text
}
foreach ($case in @(
    @('0.5.0-preview.10','0.5.0-preview.2',1), @('0.5.0','0.5.0-preview.10',1),
    @('0.4.0','0.5.0',-1), @('0.5.0+abc','0.5.0+xyz',0), @('0.5.0-alpha.1','0.5.0-alpha.beta',-1)
)) {
    if ([Math]::Sign((Compare-ReleaseVersion $case[0] $case[1])) -ne $case[2]) { throw ('Wrong version precedence: ' + ($case -join ' ')) }
}
Write-Output 'PASS elevated updater version ordering'
$now = [DateTimeOffset]::UtcNow
$script:originalConfig = [pscustomobject]@{Version=1;Curves=@([pscustomobject]@{
    Id='pump'; Name='Pump'; Enabled=$true; IsPump=$true; SensorId='gpu'; SafetySensorId='water'; SafetyTemperature=45; OutputId='quadro/test/output/1'
})}
$baseline = [pscustomobject]@{
    Time=$now.ToString('O'); Mode='Running'; Errors=@(); Alarms=@(); Configuration=$script:originalConfig
    Catalog=[pscustomobject]@{ Outputs=@([pscustomobject]@{Id='quadro/test/output/1';DeviceId='quadro/test';Channel=1;CanControl=$true}) }
    Values=@(
        [pscustomobject]@{Id='gpu';Value=38;Time=$now.ToString('O')},
        [pscustomobject]@{Id='water';Value=35;Time=$now.ToString('O')},
        [pscustomobject]@{Id='quadro/test/duty/1';Value=20;Time=$now.ToString('O')},
        [pscustomobject]@{Id='quadro/test/rpm/1';Value=800;Time=$now.ToString('O')}
    )
}
Assert-Healthy $baseline $now.AddSeconds(-2)
Write-Output 'PASS elevated updater accepts healthy commissioned pump'
$mutations = @(
    { param($state) $state.Mode='Monitoring' },
    { param($state) $state.Time=$now.AddSeconds(-10).ToString('O') },
    { param($state) $state.Errors=@('Device disconnected') },
    { param($state) $state.Alarms=@('Water alarm') },
    { param($state) $state.Values[0].Value=$null },
    { param($state) $state.Values[0].Value=[double]::NaN },
    { param($state) $state.Values[1].Value=[double]::PositiveInfinity },
    { param($state) $state.Values[1].Time=$now.AddSeconds(10).ToString('O') },
    { param($state) $state.Values[1].Value=46 },
    { param($state) $state.Values[2].Time=$now.AddSeconds(-10).ToString('O') },
    { param($state) $state.Values[3].Value=0 },
    { param($state) $state.Catalog.Outputs=@() },
    { param($state) $state.Configuration.Version=2 }
)
foreach ($mutation in $mutations) {
    $state = $baseline | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    & $mutation $state
    $rejected = $false
    try { Assert-Healthy $state $now.AddSeconds(-2) } catch { $rejected=$true }
    if (-not $rejected) { throw ('Unsafe health state accepted: ' + $mutation.ToString()) }
}
Write-Output ('PASS elevated updater rejects ' + $mutations.Count + ' unsafe activation states')

$fixture = Join-Path $env:TEMP ('Lutra-update-journal-test-' + [Guid]::NewGuid().ToString('N'))
$ownJob = [Guid]::NewGuid().ToString('N'); $olderJob = [Guid]::NewGuid().ToString('N')
$ownDirectory = Join-Path $fixture $ownJob; $olderDirectory = Join-Path $fixture $olderJob
$ownJournal = Join-Path $ownDirectory 'journal.json'; $olderJournal = Join-Path $olderDirectory 'journal.json'
New-Item -ItemType Directory -Path $ownDirectory,$olderDirectory | Out-Null
try {
    [IO.File]::WriteAllText($ownJournal,'own journal is deliberately excluded')
    Assert-NoPendingUpdate $fixture $ownJob
    foreach ($phase in @('prepared','committed','rolled-back','aborted')) {
        @{JobId=$olderJob;Phase=$phase} | ConvertTo-Json | Set-Content -LiteralPath $olderJournal -Encoding UTF8
        Assert-NoPendingUpdate $fixture $ownJob
    }
    foreach ($phase in @('armed','old-moved','new-active','recovering')) {
        @{JobId=$olderJob;Phase=$phase} | ConvertTo-Json | Set-Content -LiteralPath $olderJournal -Encoding UTF8
        $rejected=$false; try { Assert-NoPendingUpdate $fixture $ownJob } catch { $rejected=$true }
        if (-not $rejected) { throw ('Pending update was accepted: ' + $phase) }
    }
    foreach ($malformed in @('{', 'null', '[]', '{"Phase":"unknown"}', ('{"JobId":"'+$ownJob+'","Phase":"committed"}'))) {
        [IO.File]::WriteAllText($olderJournal,$malformed)
        $rejected=$false; try { Assert-NoPendingUpdate $fixture $ownJob } catch { $rejected=$true }
        if (-not $rejected) { throw ('Malformed earlier journal was accepted: ' + $malformed) }
    }
    Write-Output 'PASS prior pending updates block a new transaction; terminal jobs are allowed; malformed journals fail closed'
} finally {
    foreach ($path in @($ownJournal,$olderJournal,$ownDirectory,$olderDirectory,$fixture)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
}

$recoverySource = Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\Recover-LutraUpdate.ps1'
$recoveryAst = [System.Management.Automation.Language.Parser]::ParseFile($recoverySource,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$definition = $recoveryAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-TerminalRecoveryResult' },$false) | Select-Object -First 1
if (-not $definition) { throw 'Terminal recovery result helper is missing.' }
Invoke-Expression $definition.Extent.Text
foreach ($phase in @('prepared','aborted','rolled-back','committed')) {
    $result = Get-TerminalRecoveryResult $phase ([pscustomobject]@{Success=$false;RecoveryPending=$true;Message='Earlier retry failed'}) '0.5.0'
    if ($result.RecoveryPending -or $result.Success -ne ($phase -eq 'committed')) { throw ('Stale pending result survived terminal phase: ' + $phase) }
}
$preserved = Get-TerminalRecoveryResult 'committed' ([pscustomobject]@{Success=$true;Message='Useful verified success details';Version='0.5.0'}) '0.5.0'
if ($preserved.Message -ne 'Useful verified success details' -or $preserved.Version -ne '0.5.0') { throw 'Valid terminal details were discarded.' }
$repaired = Get-TerminalRecoveryResult 'rolled-back' ([pscustomobject]@{Success=$true;Message='Wrong success'}) '0.5.0'
if ($repaired.Success -or $repaired.RecoveryPending) { throw 'Contradictory terminal result was retained.' }
Write-Output 'PASS terminal journals repair pending results and preserve valid completion details'
