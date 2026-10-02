param([string]$PackagePath)
$ErrorActionPreference = 'Stop'
$package = if ($PackagePath)
{
    (Resolve-Path -LiteralPath $PackagePath).Path
}
else
{
    Join-Path (Get-Location) '공유용\GHPC-Coop-0.8.0-Setup'
}
$fixture = Join-Path $env:TEMP ('GHPC-Setup-Test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'GHPC_Data\Managed'), (Join-Path $fixture 'Mods'), (Join-Path $fixture 'MelonLoader') -Force | Out-Null
Set-Content (Join-Path $fixture 'GHPC.exe') 'fixture only, never executable'
Copy-Item 'D:\SteamLibrary\steamapps\common\Gunner HEAT PC\Bin\GHPC_Data\Managed\Assembly-CSharp.dll' (Join-Path $fixture 'GHPC_Data\Managed\Assembly-CSharp.dll')
Set-Content (Join-Path $fixture 'version.dll') 'previous loader'
Set-Content (Join-Path $fixture 'MelonLoader\old.txt') 'old loader tree'
Set-Content (Join-Path $fixture 'Mods\Localization.sentinel') 'leave unchanged'
& (Join-Path $package 'Install.ps1') -GameBin $fixture -ValidateOnly
function Get-Process
{
    param($Name, $ErrorAction) 
    [pscustomobject]@{ProcessName = "GHPC" }
}
$blocked = $false
try
{ 
    & (Join-Path $package 'Install.ps1') -GameBin $fixture 
}
catch
{ 
    if ($_.Exception.Message -like 'Close GHPC*')
    {
        $blocked = $true
    }
    else
    {
        throw
    } 
}
if (!$blocked)
{
    throw 'Running-game protection not verified'
}
# Mock only the process check inside this fixture test; production setup has no bypass.
function Get-Process
{
    param($Name, $ErrorAction) 
    @()
}
& (Join-Path $package 'Install.ps1') -GameBin $fixture
if (!(Test-Path (Join-Path $fixture 'MelonLoader\net35\MelonLoader.dll')))
{
    throw 'Loader not installed'
}
if ((Get-FileHash (Join-Path $fixture 'Mods\GhpcCoopLab.dll')).Hash -ne (Get-FileHash (Join-Path $package 'GhpcCoopLab.dll')).Hash)
{
    throw 'Mod mismatch'
}
if ((Get-Content (Join-Path $fixture 'Mods\Localization.sentinel') -Raw).Trim() -ne 'leave unchanged')
{
    throw 'Other mod changed'
}
$backups = Get-ChildItem $fixture -Directory -Filter 'CoopBackup-*'
if (!(Test-Path (Join-Path $backups[0].FullName 'Previous\MelonLoader\old.txt')))
{
    throw 'Old loader backup missing'
}
$before = (Get-FileHash (Join-Path $fixture 'version.dll')).Hash
function Copy-Item
{
    param($LiteralPath, $Destination, [switch]$Recurse) 
    if ($LiteralPath -eq (Join-Path $package 'GhpcCoopLab.dll'))
    {
        throw 'Injected fixture copy failure'
    } 
    Microsoft.PowerShell.Management\Copy-Item -LiteralPath $LiteralPath -Destination $Destination -Recurse:$Recurse
}
$failed = $false
try
{ 
    & (Join-Path $package 'Install.ps1') -GameBin $fixture 
}
catch
{
    if ($_.Exception.Message -eq 'Injected fixture copy failure')
    {
        $failed = $true
    }
    else
    {
        throw
    }
}
if (!$failed)
{
    throw 'Failure injection did not run'
}
if ((Get-FileHash (Join-Path $fixture 'version.dll')).Hash -ne $before)
{
    throw 'Rollback loader mismatch'
}
if ((Get-FileHash (Join-Path $fixture 'Mods\GhpcCoopLab.dll')).Hash -ne (Get-FileHash (Join-Path $package 'GhpcCoopLab.dll')).Hash)
{
    throw 'Rollback mod mismatch'
}
Write-Output 'PASS package validation, running-game guard, bundled installation, backup, other-mod preservation, injected failure rollback.'
