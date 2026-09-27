param([string]$Mission='GT01_combat_team')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$hostCopy=Join-Path $env:TEMP 'GHPC-Coop-Host-095'
$source=Join-Path $env:TEMP 'GHPC-Coop-Client-20260926'
$guests=@($source,(Join-Path $env:TEMP 'GHPC-Coop-Guest2-097'),(Join-Path $env:TEMP 'GHPC-Coop-Guest3-097'))
$folders=@($hostCopy)+$guests
$known=@($folders | ForEach-Object {Join-Path $_ 'GHPC.exe'})+([IO.Path]::GetFullPath((Join-Path $root '..\playtest\Bin\GHPC.exe')))
foreach($p in @(Get-Process GHPC -ErrorAction SilentlyContinue)){if($known -contains $p.Path){Stop-Process -Id $p.Id -Force; if(-not $p.WaitForExit(10000)){throw 'Test game did not stop'}}}
foreach($dest in $guests[1..2]){
 if(-not (Test-Path -LiteralPath $dest)){
  New-Item -ItemType Directory -Path $dest | Out-Null
  foreach($name in @('GHPC_Data','MonoBleedingEdge')){New-Item -ItemType Junction -Path (Join-Path $dest $name) -Target (Join-Path $source $name) | Out-Null}
  foreach($name in @('GHPC.exe','UnityPlayer.dll','UnityCrashHandler64.exe','version.dll','dobby.dll','steam_appid.txt')){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $dest}
  foreach($name in @('MelonLoader','Plugins','UserLibs')){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $dest -Recurse}
 }
}
$sourceConfig=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\GHPC\Data\config.json'
foreach($folder in $folders){
 $data=Join-Path $folder 'UserData\GhpcCoop'
 New-Item -ItemType Directory -Path $data -Force | Out-Null
 New-Item -ItemType Directory -Path (Join-Path $folder 'Mods') -Force | Out-Null
 $config=Get-Content -LiteralPath $sourceConfig -Raw | ConvertFrom-Json
 $v=$config.GHPC_CONFIG_SAVE.value
 $v.ScreenMode=3; $v.ResolutionX=960; $v.ResolutionY=540; $v.Vsync=$false; $v.FpsCap=30
 $v.ShadowDetail=1; $v.ShadowDistance=200; $v.AmbientOcclusion=0; $v.ScreenSpaceReflections=0
 $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $data 'test-config.json') -Encoding UTF8
 Copy-Item -LiteralPath (Join-Path $root 'dist\GhpcCoopLab.dll') -Destination (Join-Path $folder 'Mods\GhpcCoopLab.dll')
 if((Get-FileHash (Join-Path $folder 'Mods\GhpcCoopLab.dll')).Hash -ne (Get-FileHash (Join-Path $root 'dist\GhpcCoopLab.dll')).Hash){throw 'DLL mismatch'}
}
$common=@('--coop-testwindow','--coop-fourwindow','--coop-port=22395','-screen-fullscreen','0','-screen-width','960','-screen-height','540')
foreach($folder in $guests){
 $args=@('--coop-menujoin',('--coop-roomfile='+$hostCopy+'\UserData\GhpcCoop\room.txt'))+$common+@('-logFile','four-guest.log')
 $p=Start-Process -FilePath (Join-Path $folder 'GHPC.exe') -WorkingDirectory $folder -ArgumentList $args -WindowStyle Normal -PassThru
 [pscustomobject]@{Role='Guest';PID=$p.Id;Folder=$folder}
}
$args=@('--coop-autoload','--coop-probe',('--coop-mission='+$Mission))+$common+@('-logFile','four-host.log')
$p=Start-Process -FilePath (Join-Path $hostCopy 'GHPC.exe') -WorkingDirectory $hostCopy -ArgumentList $args -WindowStyle Normal -PassThru
[pscustomobject]@{Role='Host';PID=$p.Id;Folder=$hostCopy;Mission=$Mission}
