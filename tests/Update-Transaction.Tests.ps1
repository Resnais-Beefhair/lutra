# Isolated updater transaction tests: no elevation, network, real SCM or real hardware.
param([ValidateSet('success','rollback','first-rename','second-rename','kill-stopped','kill-old-moved','kill-new-active','kill-committed','recover')][string]$Scenario, [string]$FixturePath)
$ErrorActionPreference = 'Stop'
if (-not $Scenario) {
    foreach ($case in @('success','rollback','first-rename','second-rename','kill-stopped','kill-old-moved','kill-new-active','kill-committed')) {
        $casePath=Join-Path $env:TEMP ('Lutra-updater-fixture-' + [Guid]::NewGuid().ToString('N'))
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Scenario $case -FixturePath $casePath
        if ($case.StartsWith('kill-')) {
            if ($LASTEXITCODE -ne 99) { throw ('The intended updater kill did not happen: ' + $case) }
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Scenario recover -FixturePath $casePath
        }
        if ($LASTEXITCODE) { throw ('Updater transaction fixture failed: ' + $case) }
    }
    exit 0
}
$fixtureRoot = if($FixturePath) { [IO.Path]::GetFullPath($FixturePath) } else { Join-Path $env:TEMP ('Lutra-updater-fixture-' + [Guid]::NewGuid().ToString('N')) }
if (-not $fixtureRoot.StartsWith(([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\Lutra-updater-fixture-'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture must be inside its dedicated temporary directory.' }
$fixtureTarget = Join-Path $fixtureRoot 'ProgramFiles\Lutra'
$fixtureData = Join-Path $fixtureRoot 'ProgramData\Lutra'
$fixturePackage = Join-Path $fixtureRoot 'package'
if ($Scenario -ne 'recover') {
New-Item -ItemType Directory -Path (Join-Path $fixtureTarget 'tools'),$fixtureData,(Join-Path $fixturePackage 'tools'),(Join-Path $fixtureData 'history') | Out-Null
Add-Type -TypeDefinition 'using System.Reflection; [assembly:AssemblyFileVersion("0.0.1.0")] [assembly:AssemblyInformationalVersion("0.0.1")] public class OldLutraFixture { }' -OutputAssembly (Join-Path $fixtureTarget 'Lutra.dll')
Add-Type -TypeDefinition 'using System.Reflection; [assembly:AssemblyFileVersion("0.0.2.0")] [assembly:AssemblyInformationalVersion("0.0.2")] public class NewLutraFixture { }' -OutputAssembly (Join-Path $fixturePackage 'Lutra.dll')
Copy-Item -LiteralPath (Join-Path $fixturePackage 'Lutra.dll') -Destination (Join-Path $fixturePackage 'Lutra.Service.dll')
foreach ($name in @('Lutra.exe','Lutra.Service.exe')) {
    [IO.File]::WriteAllText((Join-Path $fixtureTarget $name),'old fixture, never executed')
    [IO.File]::WriteAllText((Join-Path $fixturePackage $name),'new fixture, never executed')
}
[IO.File]::WriteAllText((Join-Path $fixtureTarget 'old-only.txt'),'old')
[IO.File]::WriteAllText((Join-Path $fixturePackage 'new-only.txt'),'new')
[IO.File]::WriteAllText((Join-Path $fixturePackage 'tools\Update-Lutra.ps1'),'# downloaded updater is never executed')
[IO.File]::WriteAllText((Join-Path $fixturePackage 'tools\Recover-LutraUpdate.ps1'),'# downloaded recovery is never executed')
Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\Recover-LutraUpdate.ps1') -Destination (Join-Path $fixtureTarget 'tools\Recover-LutraUpdate.ps1')
[IO.File]::WriteAllText((Join-Path $fixtureData 'history\preserve.txt'),'history remains')
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
[IO.File]::WriteAllText((Join-Path $fixtureData 'owner.sid'),$sid)
$config = @{ Version=1; Curves=@(@{ Id='pump';Name='Pump';Enabled=$true;IsPump=$true;SensorId='gpu';SafetySensorId='water';SafetyTemperature=45;OutputId='quadro/test/output/1' }) }
$config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $fixtureData 'configuration.json') -Encoding UTF8
$configHash = (Get-FileHash -LiteralPath (Join-Path $fixtureData 'configuration.json') -Algorithm SHA256).Hash
$state = @{
    Time=[DateTimeOffset]::UtcNow.ToString('O');Mode='Running';Errors=@();Alarms=@();Configuration=$config
    Catalog=@{ Outputs=@(@{Id='quadro/test/output/1';DeviceId='quadro/test';Channel=1;CanControl=$true}) }
    Values=@(@{Id='gpu';Value=38},@{Id='water';Value=35},@{Id='quadro/test/duty/1';Value=20},@{Id='quadro/test/rpm/1';Value=800})
}
$state | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $fixtureData 'state.json') -Encoding UTF8
$fixtureZip = Join-Path $fixtureRoot 'Lutra-0.0.2-win-x64.zip'
Compress-Archive -LiteralPath $fixturePackage -DestinationPath $fixtureZip
} else {
    $context=Get-Content -LiteralPath (Join-Path $fixtureRoot 'fixture-context.json') -Raw | ConvertFrom-Json
    $configHash=$context.ConfigHash; $sid=$context.OwnerSid; $job=$context.JobId
    $fixtureZip=Join-Path $fixtureRoot 'Lutra-0.0.2-win-x64.zip'
}
$fixtureHash = (Get-FileHash -LiteralPath $fixtureZip -Algorithm SHA256).Hash
$global:fixtureMetadata = [pscustomobject]@{
    draft=$false;tag_name='v0.0.2';html_url='https://github.com/Resnais-Beefhair/lutra/releases/tag/v0.0.2'
    assets=@([pscustomobject]@{ name='Lutra-0.0.2-win-x64.zip';state='uploaded';digest=('sha256:'+$fixtureHash);size=(Get-Item -LiteralPath $fixtureZip).Length;browser_download_url='https://github.com/Resnais-Beefhair/lutra/releases/download/v0.0.2/Lutra-0.0.2-win-x64.zip' })
}
$global:fixtureServiceState = 'Running'; $global:fixtureStartCount = 0; $global:fixtureScenario = $Scenario
$global:fixtureServicePath = Join-Path $fixtureTarget 'Lutra.Service.exe'
if ($Scenario -eq 'recover') { $global:fixtureTaskRegistered=$true; $global:fixtureServiceState=if($context.Scenario -eq 'kill-committed') {'Running'} else {'Stopped'} }
function Invoke-RestMethod { param($Uri,$Headers,$TimeoutSec) return $global:fixtureMetadata }
function Set-Acl { param($LiteralPath,$AclObject) }
function Get-CimInstance { param($ClassName,$Filter) return [pscustomobject]@{State='Running';PathName=('"'+$global:fixtureServicePath+'"')} }
function Get-Process { param($Name,$Id,$ErrorAction) return }
function Get-Service {
    param($Name)
    $service = [pscustomobject]@{ Status=$global:fixtureServiceState }
    $service | Add-Member -MemberType ScriptMethod -Name WaitForStatus -Value { param($status,$timeout) }
    return $service
}
function Stop-Service { param($Name) $global:fixtureServiceState='Stopped' }
function Start-Service { param($Name) $global:fixtureServiceState='Running'; $global:fixtureStartCount++ }
function New-ScheduledTaskAction { param($Execute,$Argument,$WorkingDirectory) return [pscustomobject]@{Execute=$Execute;Arguments=$Argument;WorkingDirectory=$WorkingDirectory} }
function New-ScheduledTaskTrigger { param([switch]$AtStartup,[switch]$Once,$At,$RepetitionInterval) return [pscustomobject]@{AtStartup=$AtStartup.IsPresent;Once=$Once.IsPresent} }
function New-ScheduledTaskPrincipal { param($UserId,$LogonType,$RunLevel) return [pscustomobject]@{UserId=$UserId} }
function New-ScheduledTaskSettingsSet { param([switch]$StartWhenAvailable,[switch]$AllowStartIfOnBatteries,[switch]$DontStopIfGoingOnBatteries,$MultipleInstances,$ExecutionTimeLimit) return [pscustomobject]@{} }
function Register-ScheduledTask {
    param($TaskName,$TaskPath,$Action,$Trigger,$Principal,$Settings,$Description)
    $global:fixtureTaskRegistered=$true
    $global:fixtureTask=[pscustomobject]@{Actions=@($Action);Triggers=$Trigger;Principal=$Principal;State='Ready'}
}
function Get-ScheduledTask { param($TaskName,$TaskPath) return $global:fixtureTask }
function Unregister-ScheduledTask { param($TaskName,$TaskPath,$Confirm) $global:fixtureTaskRegistered=$false }

$realSource = Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\Update-Lutra.ps1'
$source = if($Scenario -eq 'recover') { [IO.File]::ReadAllText((Join-Path $fixtureData ('updates\'+$job+'\Recover.ps1'))) } else { [IO.File]::ReadAllText($realSource) }
function Replace-Exact([string]$Before,[string]$After) {
    if (-not $script:source.Contains($Before)) { throw ('Fixture seam changed: ' + $Before) }
    $script:source = $script:source.Replace($Before,$After)
}
# Rewrite only the privileged/environment boundaries. The real package checks, file copies,
# service handover ordering, directory swaps, backups, catch/rollback and result persistence run intact.
Replace-Exact "`$target = [IO.Path]::GetFullPath((Join-Path `$env:ProgramFiles 'Lutra'))" ("`$target = '"+$fixtureTarget.Replace("'","''")+"'")
Replace-Exact "`$data = [IO.Path]::GetFullPath((Join-Path `$env:ProgramData 'Lutra'))" ("`$data = '"+$fixtureData.Replace("'","''")+"'")
if ($Scenario -ne 'recover') {
Replace-Exact "if (-not `$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator approval is required to update the installed cooling service.' }" '# Administrator gate intentionally bypassed only in this temporary fixture.'
Replace-Exact '$until = [DateTimeOffset]::UtcNow.AddSeconds(65)' '$until = [DateTimeOffset]::UtcNow.AddSeconds(2)'
Replace-Exact 'Start-Sleep -Milliseconds 1200' 'Start-Sleep -Milliseconds 100'
if ($Scenario -eq 'first-rename') { Replace-Exact '[IO.Directory]::Move($target,$backup)' "throw 'Injected first rename failure'" }
if ($Scenario -eq 'second-rename') { Replace-Exact '[IO.Directory]::Move($prepared,$target)' "throw 'Injected second rename failure'" }
if ($Scenario -eq 'kill-stopped') { Replace-Exact '$stopped = $true; Stop-Control' '$stopped = $true; Stop-Control; [Environment]::Exit(99)' }
if ($Scenario -eq 'kill-old-moved') { Replace-Exact "Set-UpdatePhase 'old-moved'" "Set-UpdatePhase 'old-moved'; [Environment]::Exit(99)" }
if ($Scenario -eq 'kill-new-active') { Replace-Exact "Set-UpdatePhase 'new-active'" "Set-UpdatePhase 'new-active'; [Environment]::Exit(99)" }
if ($Scenario -eq 'kill-committed') { Replace-Exact "Set-UpdatePhase 'committed'; `$terminal = `$true" "Set-UpdatePhase 'committed'; `$terminal = `$true; [Environment]::Exit(99)" }
$tokens=$null; $errors=$null; $ast=[System.Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$errors)
$readState = $ast.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-State'},$false) | Select-Object -First 1
$fakeReadState = @'
function Read-State {
    $state = Get-Content -LiteralPath (Join-Path $data 'state.json') -Raw | ConvertFrom-Json
    $state.Time=[DateTimeOffset]::UtcNow.ToString('O')
    foreach($value in $state.Values) { $value | Add-Member -NotePropertyName Time -NotePropertyValue $state.Time -Force }
    if($global:fixtureScenario -eq 'rollback' -and $global:fixtureStartCount -eq 1) { $state.Mode='Monitoring' }
    return $state
}
'@
Replace-Exact $readState.Extent.Text $fakeReadState
$underTest = Join-Path $fixtureTarget 'tools\Update-under-test.ps1'
[IO.File]::WriteAllText($underTest,$source)
$job=[Guid]::NewGuid().ToString('N')
@{JobId=$job;ConfigHash=$configHash;OwnerSid=$sid;Scenario=$Scenario} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixtureRoot 'fixture-context.json') -Encoding UTF8
& $underTest -Archive $fixtureZip -Tag 'v0.0.2' -ExpectedSha256 $fixtureHash -ParentId 12345 -JobId $job
} else {
    $underTest=Join-Path $fixtureData ('updates\'+$job+'\Recover.ps1')
    [IO.File]::WriteAllText($underTest,$source)
    # Recovery cannot race a live update: the first attempt sees the held lock and performs no SCM action.
    $heldLock=[IO.File]::Open((Join-Path $fixtureData 'update.lock'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try { & $underTest -JobId $job; if ($global:fixtureStartCount -ne 0) { throw 'Recovery ignored the exclusive transaction lock.' } }
    finally { $heldLock.Dispose() }
    & $underTest -JobId $job
}
$result = Get-Content -LiteralPath (Join-Path $fixtureData ('updates\'+$job+'\result.json')) -Raw | ConvertFrom-Json
$expectSuccess = $Scenario -eq 'success' -or ($Scenario -eq 'recover' -and $context.Scenario -eq 'kill-committed')
if ([bool]$result.Success -ne $expectSuccess) { throw ('Unexpected update result: ' + $result.Message) }
$installed = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $fixtureTarget 'Lutra.dll')).ProductVersion
if ($expectSuccess) { $expected='0.0.2' } else { $expected='0.0.1' }
if ($installed -cne $expected) { throw ('Unexpected installed version: ' + $installed + '; ' + $result.Message) }
if ($global:fixtureServiceState -ne 'Running') { throw 'Controller was not left running.' }
if ($global:fixtureTaskRegistered) { throw 'Recovery task was not removed after a verified terminal state.' }
if ((Get-FileHash -LiteralPath (Join-Path $fixtureData 'configuration.json') -Algorithm SHA256).Hash -cne $configHash) { throw 'Configuration changed.' }
if ([IO.File]::ReadAllText((Join-Path $fixtureData 'history\preserve.txt')) -cne 'history remains') { throw 'History changed.' }
if ([IO.File]::ReadAllText((Join-Path $fixtureData 'owner.sid')) -cne $sid) { throw 'Owner changed.' }
if ((Test-Path -LiteralPath (Join-Path $fixtureTarget 'new-only.txt')) -ne $expectSuccess) { throw 'New installation marker is wrong.' }
if ((Test-Path -LiteralPath (Join-Path $fixtureTarget 'old-only.txt')) -eq $expectSuccess) { throw 'Old installation marker is wrong.' }
$caseLabel=if($Scenario -eq 'recover') {$context.Scenario + ' recovery + exclusive lock'} else {$Scenario}
Write-Output ('PASS isolated updater transaction: '+$caseLabel+'; '+$result.Message)
# Temporary fixtures are deliberately retained for inspection; no recursive cleanup runs here.
exit 0
