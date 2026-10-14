[CmdletBinding()]
param(
    [string]$Source = (Split-Path $PSScriptRoot -Parent),
    [Parameter(Mandatory=$true)][string]$OwnerSid,
    [string]$ConfigurationPath
)
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:ProgramFiles 'Lutra'
$data = Join-Path $env:ProgramData 'Lutra'
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this installer as administrator.' }
    $owner = [Security.Principal.SecurityIdentifier]::new($OwnerSid)
    $sourceRoot = (Resolve-Path -LiteralPath $Source).Path
    foreach ($name in @('Lutra.exe','Lutra.Service.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $name))) { throw "Release file missing: $name" }
    }
    foreach ($name in @('AquaComputerService','Vesi.Service','FanControl')) {
        if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw "Stop $name before installing Lutra control. The existing controller was not changed." }
    }
    $config = $null
    if ($ConfigurationPath) {
        $config = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
        if ($config.Version -ne 1) { throw 'Unsupported Lutra configuration.' }
    }
    $service = Get-Service -Name LutraControl -ErrorAction SilentlyContinue
    foreach ($monitor in @(Get-Process -Name Lutra -ErrorAction SilentlyContinue)) {
        if (-not $monitor.WaitForExit(6000)) { throw 'Exit Lutra from its notification-area menu, then run this installer again.' }
    }
    if ($service) { Stop-Service -Name LutraControl; $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(20)) }
    New-Item -ItemType Directory -Path $target,$data -Force | Out-Null
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true,$false)
    foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow')
        $acl.AddAccessRule($rule)
    }
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($owner,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'))
    Set-Acl -LiteralPath $data -AclObject $acl
    if ($sourceRoot.TrimEnd('\') -ne $target.TrimEnd('\')) {
        Get-ChildItem -LiteralPath $sourceRoot | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force }
    }
    [IO.File]::WriteAllText((Join-Path $data 'owner.sid'),$owner.Value)
    if (-not (Test-Path -LiteralPath (Join-Path $data 'configuration.json'))) {
        if ($config) { $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $data 'configuration.json') -Encoding UTF8 }
        else { [IO.File]::WriteAllText((Join-Path $data 'configuration.json'),'{"Version":1,"Language":"en","RetentionDays":7,"MotherboardEnabled":false,"Curves":[],"Alarms":[]}') }
    }
    $binary = '"' + (Join-Path $target 'Lutra.Service.exe') + '"'
    if (-not $service) { New-Service -Name LutraControl -BinaryPathName $binary -DisplayName 'Lutra cooling control' -StartupType Automatic | Out-Null }
    else { & sc.exe config LutraControl binPath= $binary start= auto | Out-Null; if ($LASTEXITCODE) { throw 'SCM configuration failed.' } }
    & sc.exe failure LutraControl reset= 86400 actions= restart/2000/restart/5000/restart/15000 | Out-Null
    if ($LASTEXITCODE) { throw 'SCM recovery configuration failed.' }
    & sc.exe failureflag LutraControl 1 | Out-Null
    if ($LASTEXITCODE) { throw 'SCM failure action flag failed.' }
    Start-Service -Name LutraControl
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Lutra.lnk'))
    $shortcut.TargetPath = Join-Path $target 'Lutra.exe'; $shortcut.WorkingDirectory = $target; $shortcut.IconLocation = $shortcut.TargetPath; $shortcut.Save()
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show('Lutra service installed. Open Lutra from the Start menu. Your configuration remains editable; verify RPM and fallback behavior before leaving it unattended.','Lutra') | Out-Null
}
catch {
    $message = $_.Exception.Message
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show($message,'Lutra installation failed','OK','Error') | Out-Null
    Write-Error $message
    exit 1
}

