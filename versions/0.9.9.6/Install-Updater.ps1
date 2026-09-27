param([Parameter(Mandatory=$true)][string]$GameBin)
function Get-CoopPackageFileHash([string]$LiteralPath,[ValidateSet('SHA256','SHA512')][string]$Algorithm='SHA256') {
 $stream=$null;$hasher=$null
 try {
  $stream=[IO.File]::OpenRead($LiteralPath)
  if($Algorithm -eq 'SHA512'){$hasher=[Security.Cryptography.SHA512]::Create()}else{$hasher=[Security.Cryptography.SHA256]::Create()}
  [pscustomobject]@{Hash=[BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','')}
 } finally {if($hasher){$hasher.Dispose()};if($stream){$stream.Dispose()}}
}
$ErrorActionPreference='Stop'
$bin=(Resolve-Path -LiteralPath $GameBin).Path.TrimEnd('\')
if(Get-Process GHPC -ErrorAction SilentlyContinue){throw 'Close GHPC before installing the launcher.'}
$package=$PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $package 'coop-update.json') -Raw | ConvertFrom-Json
$mod=Join-Path $bin 'Mods\GhpcCoopLab.dll'
if((Get-CoopPackageFileHash -LiteralPath $mod).Hash -ne $manifest.modSha256){throw 'Install the packaged co-op mod before installing the launcher.'}
$dest=Join-Path $bin 'UserData\GhpcCoop\Updater'
foreach($relative in @('UserData','UserData\GhpcCoop','UserData\GhpcCoop\Updater')){
 $p=Join-Path $bin $relative
 if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Launcher destination must not be a linked directory.'}
}
New-Item -ItemType Directory -Path $dest -Force | Out-Null
foreach($name in @('Launcher.ps1','Update-Core.ps1')){
 $expected=(Get-Content -LiteralPath (Join-Path $package 'updater-sha256.json') -Raw | ConvertFrom-Json).$name
 if(!$expected -or (Get-CoopPackageFileHash -LiteralPath (Join-Path $package $name)).Hash -ne $expected){throw 'Launcher package integrity failed.'}
}
foreach($name in @('Launcher.ps1','Update-Core.ps1')){
 $target=Join-Path $dest $name
 if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination ($target+'.backup-'+[guid]::NewGuid().ToString('N'))}
 Copy-Item -LiteralPath (Join-Path $package $name) -Destination $target
}
. (Join-Path $dest 'Update-Core.ps1')
Write-CoopJson (Join-Path $dest 'installed.json') ([pscustomobject]@{version=$manifest.version;modSha256=$manifest.modSha256;gameAssemblySha256=$manifest.gameAssemblySha256;updatedUtc=[DateTime]::UtcNow.ToString('o')})
# An upgraded setup must retain the already-connected publisher repository.
if(!(Test-Path -LiteralPath (Join-Path $dest 'source.json'))){Copy-Item -LiteralPath (Join-Path $package 'update-source.json') -Destination (Join-Path $dest 'source.json')}
Write-Output 'GHPC Co-op Launcher installed. It checks for updates before starting the game.'
