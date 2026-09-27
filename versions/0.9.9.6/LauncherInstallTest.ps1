param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference='Stop'
$package=(Resolve-Path -LiteralPath $PackagePath).Path
$fixture=Join-Path $env:TEMP ('GHPC-Launcher-Install-Test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Mods') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'GHPC.exe'),'fixture only - not executable')
Copy-Item -LiteralPath (Join-Path $package 'GhpcCoopLab.dll') -Destination (Join-Path $fixture 'Mods\GhpcCoopLab.dll')
function Get-Process {param($Name,$ErrorAction) [pscustomobject]@{Name='GHPC'}}
$blocked=$false;try{& (Join-Path $package 'Install-Updater.ps1') -GameBin $fixture}catch{if($_.Exception.Message -like 'Close GHPC*'){$blocked=$true}else{throw}}
if(!$blocked){throw 'Launcher install must block while GHPC runs'}
function Get-Process {param($Name,$ErrorAction) @()}
& (Join-Path $package 'Install-Updater.ps1') -GameBin $fixture
$dest=Join-Path $fixture 'UserData\GhpcCoop\Updater'
$manifest=Get-Content -LiteralPath (Join-Path $package 'coop-update.json') -Raw | ConvertFrom-Json
$state=Get-Content -LiteralPath (Join-Path $dest 'installed.json') -Raw | ConvertFrom-Json
if($state.version -ne $manifest.version -or $state.modSha256 -ne $manifest.modSha256){throw 'Launcher initial version mismatch'}
foreach($name in @('Update-Core.ps1','Launcher.ps1')){if((Get-FileHash (Join-Path $dest $name)).Hash -ne (Get-FileHash (Join-Path $package $name)).Hash){throw 'Launcher copy mismatch'}}
$config=Join-Path $dest 'source.json'
[IO.File]::WriteAllText($config,'{"repository":"fixture/existing","channel":"stable"}')
& (Join-Path $package 'Install-Updater.ps1') -GameBin $fixture
if((Get-Content -LiteralPath $config -Raw | ConvertFrom-Json).repository -ne 'fixture/existing'){throw 'Existing update source overwritten'}
foreach($file in (Get-ChildItem -LiteralPath $package -Filter '*.ps1')){
 $tokens=$null;$parseErrors=$null
 $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$parseErrors)
 if($parseErrors.Count){throw ($file.Name+': '+($parseErrors.Message -join '; '))}
}
Write-Output 'PASS launcher running-game guard, installation, recorded version, source preservation and PowerShell 5.1 parsing.'
Write-Output ('GUI fixture: '+$fixture)
