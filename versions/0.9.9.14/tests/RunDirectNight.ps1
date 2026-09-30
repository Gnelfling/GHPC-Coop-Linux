param([ValidateSet(3, 4)][int]$Players = 3)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$mission = Get-Content (Join-Path $root 'night-missions.json') -Raw | ConvertFrom-Json | Where-Object Players -eq $Players
$folders = @(
    'GHPC-Coop-Host-095', 'GHPC-Coop-Client-20260926', 'GHPC-Coop-Guest2-097', 'GHPC-Coop-Guest3-097') | ForEach-Object {
    Join-Path $env:TEMP $_ }
$paths = @(
    $folders | ForEach-Object {
        Join-Path $_ 'GHPC.exe' })
$running = @(
    Get-Process GHPC -ErrorAction SilentlyContinue)
if (@(
        $running | Where-Object {
            $_.Path -notin $paths }).Count)
{
    throw 'Another GHPC game is running; leave it untouched.'
}
$stamp = Get-Date -Format yyyyMMdd-HHmmss
$out = Join-Path $root "night-results/$stamp-$Players-player"
New-Item -ItemType Directory -Path $out -Force | Out-Null
# Preserve the previous run before restarting only the four known test copies.
foreach ($p in $running)
{
    Stop-Process -Id $p.Id -Force;
    if (-not $p.WaitForExit(10000))
    {
        throw 'Test process did not stop'
    }
}
$configPath = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games/GHPC/Data/config.json'
$dll = Join-Path $root 'dist/GhpcCoopLab.dll';
$hash = (Get-FileHash -LiteralPath $dll).Hash
for ($i = 0; $i -lt $Players; $i++)
{
    $folder = $folders[$i];
    $data = Join-Path $folder 'UserData/GhpcCoop';
    $target = Join-Path $folder 'Mods/GhpcCoopLab.dll'
    if (-not(Test-Path -LiteralPath $paths[$i]))
    {
        throw "Missing test copy: $folder"
    }
    $backup = Join-Path $data "Backups/before-direct-$stamp";
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    if (Test-Path -LiteralPath $target)
    {
        Copy-Item -LiteralPath $target -Destination $backup
    }
    foreach ($file in @(
            'network-verification.txt', 'network-vehicles.tsv', 'replica-report.json', 'room.txt', 'last-error.txt', 'shot-audit.tsv'))
    { 
        $old = Join-Path $data $file;
        if (Test-Path -LiteralPath $old)
        {
            Move-Item -LiteralPath $old -Destination $backup
        }
    }
    $oldlog = Join-Path $folder 'MelonLoader/Latest.log';
    if (Test-Path -LiteralPath $oldlog)
    {
        Copy-Item -LiteralPath $oldlog -Destination $backup
    }
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json;
    $v = $config.GHPC_CONFIG_SAVE.value
    $v.ScreenMode = 3;
    $v.ResolutionX = 1024;
    $v.ResolutionY = 576;
    $v.Vsync = $false;
    $v.FpsCap = 15;
    $v.ShadowDetail = 1;
    $v.ShadowDistance = 100;
    $v.AmbientOcclusion = 0;
    $v.ScreenSpaceReflections = 0
    $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $data 'test-config.json') -Encoding utf8
    Copy-Item -LiteralPath $dll -Destination $target
    if ((Get-FileHash -LiteralPath $target).Hash -ne $hash)
    {
        throw 'Test DLL copy mismatch'
    }
}
$common = @(
    '--coop-direct', '--coop-testwindow', '--coop-fourwindow', '--coop-port=22395', '--coop-netverify', "--coop-testplayers=$Players", '-screen-fullscreen', '0', '-screen-width', '1024', '-screen-height', '576')
$processes = @()
# Guests wait for a newly written room descriptor; they share the host mission loader.
for ($i = 1; $i -lt $Players; $i++)
{
    $gameArgs = @(
        '--coop-menujoin', '--coop-autodrive', "--coop-name=Guest$i", ('--coop-roomfile=' + $folders[0] + '\UserData\GhpcCoop\room.txt')) + $common
    $p = Start-Process -FilePath $paths[$i] -WorkingDirectory $folders[$i] -ArgumentList $gameArgs -WindowStyle Normal -PassThru
    $processes += [pscustomobject]@{Role = "Guest$i";
        PID = $p.Id;
        Folder = $folders[$i]
    }
}
$gameArgs = @(
    '--coop-autoload', '--coop-probe', '--coop-name=Host', ('--coop-mission=' + $mission.Mission), ('--coop-faction=' + $mission.Faction)) + $common
$p = Start-Process -FilePath $paths[0] -WorkingDirectory $folders[0] -ArgumentList $gameArgs -WindowStyle Normal -PassThru
$processes += [pscustomobject]@{Role = 'Host';
    PID = $p.Id;
    Folder = $folders[0]
}
$record = [pscustomobject]@{Started = (Get-Date).ToString('o');
    Players = $Players;
    Mission = $mission.Mission;
    Faction = $mission.Faction;
    DLL = $hash;
    Output = $out;
    Processes = $processes
}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'run.json') -Encoding utf8
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'active-night-run.json') -Encoding utf8
$record | ConvertTo-Json -Depth 5
