param([Parameter(Mandatory = $true)][string]$Mission, [switch]$VerifyDamage)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$guest = 'C:\Users\user\AppData\Local\Temp\GHPC-Coop-Client-20260926'
$hostCopy = 'C:\Users\user\AppData\Local\Temp\GHPC-Coop-Host-095'
$paths = @(
    (Join-Path $guest 'GHPC.exe'), (Join-Path $hostCopy 'GHPC.exe'), (Join-Path $root '..\playtest\Bin\GHPC.exe')) | ForEach-Object {
    [IO.Path]::GetFullPath($_) }
foreach ($p in @(
        Get-Process GHPC -ErrorAction SilentlyContinue))
{
    if ($paths -contains $p.Path)
    {
        Stop-Process -Id $p.Id -Force;
        if (-not $p.WaitForExit(10000))
        {
            throw 'Test process did not exit'
        }
    }
}
foreach ($folder in @(
        $guest, $hostCopy))
{
    $testData = Join-Path $folder 'UserData\GhpcCoop'
    New-Item -ItemType Directory -Path $testData -Force | Out-Null
    $sourceConfig = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\GHPC\Data\config.json'
    if (-not (Test-Path -LiteralPath $sourceConfig))
    {
        throw 'Existing game config is required for this two-instance test'
    }
    Copy-Item -LiteralPath $sourceConfig -Destination (Join-Path $testData 'test-config.json')
    Copy-Item -LiteralPath (Join-Path $root 'dist\GhpcCoopLab.dll') -Destination (Join-Path $folder 'Mods\GhpcCoopLab.dll')
    if ((Get-FileHash (Join-Path $folder 'Mods\GhpcCoopLab.dll')).Hash -ne (Get-FileHash (Join-Path $root 'dist\GhpcCoopLab.dll')).Hash)
    {
        throw 'Deployed DLL mismatch'
    }
}
$common = @(
    '--coop-testwindow', '--coop-port=22395', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720')
if ($VerifyDamage)
{
    $common += '--coop-netverify'
}
$ga = @(
    '--coop-menujoin', ('--coop-roomfile=' + $hostCopy + '\UserData\GhpcCoop\room.txt')) + $common + @(
    '-logFile', ('verify-' + $Mission + '-guest.log'))
$ha = @(
    '--coop-autoload', '--coop-probe', ('--coop-mission=' + $Mission)) + $common + @(
    '-logFile', ('verify-' + $Mission + '-host.log'))
$g = Start-Process -FilePath $paths[0] -WorkingDirectory $guest -ArgumentList $ga -WindowStyle Normal -PassThru
$h = Start-Process -FilePath $paths[1] -WorkingDirectory $hostCopy -ArgumentList $ha -WindowStyle Normal -PassThru
[pscustomobject]@{Mission = $Mission;
    HostPID = $h.Id;
    GuestPID = $g.Id
}
