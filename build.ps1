param([Parameter(Mandatory=$true)][string]$GameBin)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$managed = Join-Path $GameBin 'GHPC_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$common = @((Join-Path $root 'Protocol.cs'),(Join-Path $root 'Transport.cs'),(Join-Path $root 'SteamRoomPolicy.cs'),(Join-Path $root 'RoomSeats.cs'),(Join-Path $root 'MultiRoom.cs'))
$refs = @('netstandard.dll','FMODUnity.dll','com.rlabrecque.steamworks.net.dll','Eflatun.SceneReference.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.ParticleSystemModule.dll','UnityEngine.AudioModule.dll','Unity.TextMeshPro.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll') | ForEach-Object { '/r:' + (Join-Path $managed $_) }
$refs += '/r:' + (Join-Path $GameBin 'MelonLoader\net35\MelonLoader.dll')
$refs += '/r:' + (Join-Path $GameBin 'MelonLoader\net35\0Harmony.dll')
& $compiler /nologo /target:library /optimize+ ('/out:' + (Join-Path $output 'GhpcCoopLab.dll')) @refs @common (Join-Path $root 'CoopLabMod.cs') (Join-Path $root 'GameBridge.cs') (Join-Path $root 'CombatVisuals.cs') (Join-Path $root 'WeatherSync.cs') (Join-Path $root 'AmmoSync.cs') (Join-Path $root 'RoomJoin.cs') (Join-Path $root 'EquipmentSync.cs') (Join-Path $root 'SteamLink.cs') (Join-Path $root 'SteamMenu.cs') (Join-Path $root 'MissionChoices.cs') (Join-Path $root 'ObjectiveSync.cs') (Join-Path $root 'VehicleAudioSync.cs') (Join-Path $root 'ReplicaCrewAudio.cs') (Join-Path $root 'DamageSync.cs') (Join-Path $root 'DamageRegression.cs') (Join-Path $root 'SupportSync.cs') (Join-Path $root 'TracerSync.cs') (Join-Path $root 'SupportRegression.cs') (Join-Path $root 'RenderingSync.cs') (Join-Path $root 'MultiCoop.cs') (Join-Path $root 'CoopMenu.cs') ('/resource:' + (Join-Path $root 'menu-hero.png') + ',GhpcCoop.MenuHero.png')
if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed' }
& $compiler /nologo /target:exe /optimize+ ('/out:' + (Join-Path $output 'CoopSelfTest.exe')) @common (Join-Path $root 'SelfTest.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
Push-Location $output
try { & .\CoopSelfTest.exe; if ($LASTEXITCODE -ne 0) { throw 'Tests failed' } } finally { Pop-Location }





