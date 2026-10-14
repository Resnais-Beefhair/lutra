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
Write-Output 'Service removed. Motherboard controls returned to firmware when possible; QUADRO native curves persist and software curves fall back. Program files, settings and history were retained for recovery.'

