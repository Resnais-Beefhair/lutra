#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$service = Get-Service -Name LutraControl -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name LutraControl
    $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(20))
    & sc.exe delete LutraControl
    if ($LASTEXITCODE) { throw 'SCM could not remove LutraControl.' }
}
$shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Lutra.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
$expected = '"' + (Join-Path $env:ProgramFiles 'Lutra\Lutra.exe') + '" --tray'
$runKeys = @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Run')
$ownerPath = Join-Path $env:ProgramData 'Lutra\owner.sid'
if (Test-Path -LiteralPath $ownerPath) {
    $ownerSid = (Get-Content -LiteralPath $ownerPath -Raw).Trim()
    if ($ownerSid -match '^S-1-[0-9-]+$') { $runKeys += "Registry::HKEY_USERS\$ownerSid\Software\Microsoft\Windows\CurrentVersion\Run" }
}
foreach ($runKey in $runKeys) {
    if ((Get-ItemPropertyValue -LiteralPath $runKey -Name Lutra -ErrorAction SilentlyContinue) -eq $expected) {
        Remove-ItemProperty -LiteralPath $runKey -Name Lutra
    }
}
Write-Output 'Service removed. Motherboard controls returned to firmware when possible; QUADRO native curves persist and software curves fall back. Program files, settings and history were retained for recovery.'

