# Builds the patched GhpcCoopLab.dll and starts two GHPC copies on this PC:
# one host (auto-loads a mission and opens a room) and one guest (auto-joins).
#
# Game data is NOT duplicated. Each test copy holds its own small files
# (GHPC.exe, MelonLoader, Mods, UserData) and directory junctions to the
# real game folders, so it takes little disk space. Nothing in the real
# game folder is modified, and this script never deletes anything.
param(
    [string]$GameBin,
    [string]$Mission = 'TR01_showcase',
    [switch]$SkipBuild,
    [switch]$AutoDrive
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$testRoot = Join-Path $env:LOCALAPPDATA 'GHPC-Coop-Test'
$hostDir = Join-Path $testRoot 'Host'
$guestDir = Join-Path $testRoot 'Guest'

function Test-GameBin([string]$Path)
{
    return $Path -and
        (Test-Path -LiteralPath (Join-Path $Path 'GHPC.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Path 'GHPC_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)
}

function Find-GameBin
{
    $libraries = @()
    $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if ($steam)
    {
        $libraries += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf)
        {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"'))
            {
                $libraries += $match.Groups[1].Value.Replace('\\', '\')
            }
        }
    }
    $libraries += @('C:\Program Files (x86)\Steam', 'D:\SteamLibrary', 'E:\SteamLibrary')
    foreach ($library in $libraries)
    {
        $candidate = Join-Path $library 'steamapps\common\Gunner HEAT PC\Bin'
        if (Test-GameBin $candidate)
        {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

if (!$GameBin)
{
    $GameBin = Find-GameBin
}
if (!(Test-GameBin $GameBin))
{
    throw 'GHPC Bin folder not found. Run again with -GameBin "<...>\Gunner HEAT PC\Bin".'
}
if (!(Test-Path -LiteralPath (Join-Path $GameBin 'MelonLoader')))
{
    throw 'MelonLoader is not installed in the game folder. Install GHPC-Coop once with Install.cmd first.'
}
Write-Host "Game folder: $GameBin"

# 1. Build
$dll = Join-Path $root 'dist\GhpcCoopLab.dll'
if (!$SkipBuild)
{
    Write-Host 'Building patched DLL...'
    & (Join-Path $root 'build.ps1') -GameBin $GameBin -CacheDir (Join-Path $testRoot 'build-cache') -OutputDir (Join-Path $root 'dist')
}
if (!(Test-Path -LiteralPath $dll))
{
    throw "Built DLL not found: $dll"
}

# 2. Stop earlier test instances (only processes started from the test copies)
$testExes = @((Join-Path $hostDir 'GHPC.exe'), (Join-Path $guestDir 'GHPC.exe'))
foreach ($p in @(Get-Process GHPC -ErrorAction SilentlyContinue))
{
    if ($p.Path -and ($testExes -contains $p.Path))
    {
        Stop-Process -Id $p.Id -Force
        $p.WaitForExit(10000) | Out-Null
    }
}

# 3. Prepare the two copies
$copyDirs = @('MelonLoader', 'Mods', 'Plugins', 'UserData', 'UserLibs')
$config = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\GHPC\Data\config.json'
foreach ($dir in @($hostDir, $guestDir))
{
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $GameBin -Force)
    {
        $target = Join-Path $dir $item.Name
        if ($item.PSIsContainer)
        {
            if (Test-Path -LiteralPath $target)
            {
                continue
            }
            if ($copyDirs -contains $item.Name)
            {
                Copy-Item -LiteralPath $item.FullName -Destination $target -Recurse
            }
            else
            {
                New-Item -ItemType Junction -Path $target -Target $item.FullName | Out-Null
            }
        }
        else
        {
            Copy-Item -LiteralPath $item.FullName -Destination $target -Force
        }
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $dir 'Mods') | Out-Null
    Copy-Item -LiteralPath $dll -Destination (Join-Path $dir 'Mods\GhpcCoopLab.dll') -Force
    # Without this file Steam may restart the game from the real folder instead of the copy.
    $appId = Join-Path $dir 'steam_appid.txt'
    if (!(Test-Path -LiteralPath $appId))
    {
        Set-Content -LiteralPath $appId -Value '1705180' -Encoding Ascii
    }
    $data = Join-Path $dir 'UserData\GhpcCoop'
    New-Item -ItemType Directory -Force -Path $data | Out-Null
    if (Test-Path -LiteralPath $config)
    {
        Copy-Item -LiteralPath $config -Destination (Join-Path $data 'test-config.json') -Force
    }
    $room = Join-Path $data 'room.txt'
    if (Test-Path -LiteralPath $room)
    {
        # A stale room file would send the guest to an old room id.
        Rename-Item -LiteralPath $room -NewName ('room-old-' + [DateTime]::Now.ToString('yyyyMMddHHmmss') + '.txt')
    }
}

# 4. Launch (guest waits at the menu for the host's room file)
$common = @('--coop-testwindow', '--coop-port=22395', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720')
$guestArgs = @('--coop-menujoin', ('--coop-roomfile="' + (Join-Path $hostDir 'UserData\GhpcCoop\room.txt') + '"')) + $common
if ($AutoDrive)
{
    $guestArgs += '--coop-autodrive'
}
$hostArgs = @('--coop-autoload', ('--coop-mission=' + $Mission)) + $common
$g = Start-Process -FilePath (Join-Path $guestDir 'GHPC.exe') -WorkingDirectory $guestDir -ArgumentList $guestArgs -PassThru
$h = Start-Process -FilePath (Join-Path $hostDir 'GHPC.exe') -WorkingDirectory $hostDir -ArgumentList $hostArgs -PassThru

Write-Host ''
Write-Host "Host  PID $($h.Id)  log: $(Join-Path $hostDir 'MelonLoader\Latest.log')"
Write-Host "Guest PID $($g.Id)  log: $(Join-Path $guestDir 'MelonLoader\Latest.log')"
Write-Host 'The host loads the mission after about 35 seconds; the guest joins automatically.'
Write-Host 'Drive for at least 30 seconds, then close both games and send the two Latest.log files.'
