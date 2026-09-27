$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Update-Core.ps1')
$fixture=Join-Path $env:TEMP ('GHPC-Updater-Test-'+[guid]::NewGuid().ToString('N'))
$bin=Join-Path $fixture 'game';$server=Join-Path $fixture 'server'
foreach($dir in @($server,(Join-Path $bin 'Mods'),(Join-Path $bin 'GHPC_Data\Managed'),(Join-Path $bin 'MelonLoader\net35'),(Join-Path $bin 'UserData\GhpcCoop\Updater'))){New-Item -ItemType Directory -Path $dir -Force | Out-Null}
[IO.File]::WriteAllText((Join-Path $bin 'GHPC.exe'),'fixture - not executable')
[IO.File]::WriteAllText((Join-Path $bin 'GHPC_Data\Managed\Assembly-CSharp.dll'),'game fixture')
[IO.File]::WriteAllText((Join-Path $bin 'MelonLoader\net35\MelonLoader.dll'),'loader fixture')
[IO.File]::WriteAllText((Join-Path $bin 'Mods\Localization.txt'),'keep')
$paths=Get-CoopPaths $bin;$repo='fixture/coop'
$oldBytes=[Text.Encoding]::ASCII.GetBytes(('OLD-DLL-'*256));$newBytes=[Text.Encoding]::ASCII.GetBytes(('NEW-DLL-'*256))
[IO.File]::WriteAllBytes((Join-Path $server 'GhpcCoopLab.dll'),$newBytes)
$manifest=[pscustomobject]@{schema=1;minUpdaterVersion=1;version='1.1.0';modSha256=(Get-CoopHash (Join-Path $server 'GhpcCoopLab.dll'));modBytes=$newBytes.Length;gameAssemblySha256=(Get-CoopHash (Join-Path $bin 'GHPC_Data\Managed\Assembly-CSharp.dll'));loaderAssemblySha256=(Get-CoopHash (Join-Path $bin 'MelonLoader\net35\MelonLoader.dll'))}
$release=[pscustomobject]@{tag_name='v1.1.0';draft=$false;prerelease=$false;assets=@(
 [pscustomobject]@{name='coop-update.json';state='uploaded';size=600;browser_download_url='https://github.com/fixture/coop/releases/download/v1.1.0/coop-update.json'},
 [pscustomobject]@{name='GhpcCoopLab.dll';state='uploaded';size=$newBytes.Length;digest=('sha256:'+$manifest.modSha256);browser_download_url='https://github.com/fixture/coop/releases/download/v1.1.0/GhpcCoopLab.dll'})}
$script:running=$false;$script:offline=$false;$script:checks=0
function Get-Process {param($Name,$ErrorAction) if($script:running){[pscustomobject]@{Name='GHPC'}}}
function Receive-CoopFile([string]$Url,[string]$Path,[long]$Limit) {
 if($script:offline){throw 'Fixture offline'}
 if($Url -eq 'https://api.github.com/repos/fixture/coop/releases/latest'){Write-CoopJson $Path $release}
 elseif($Url.EndsWith('/coop-update.json')){Write-CoopJson $Path $manifest}
 elseif($Url.EndsWith('/GhpcCoopLab.dll')){Copy-Item -LiteralPath (Join-Path $server 'GhpcCoopLab.dll') -Destination $Path}
 else{throw ('Unexpected network destination: '+$Url)}
}
function Check([bool]$Ok,[string]$Name){if(!$Ok){throw "FAIL: $Name"};$script:checks++;Write-Output "PASS: $Name"}
function Reject([scriptblock]$Action,[string]$Name){$threw=$false;try{& $Action | Out-Null}catch{$threw=$true};Check $threw $Name}
function Reset-Fixture {
 $script:running=$false;$script:offline=$false
 [IO.File]::WriteAllBytes($paths.Dll,$oldBytes)
 Write-CoopJson $paths.State ([pscustomobject]@{version='1.0.0';modSha256=(Get-CoopHash $paths.Dll);gameAssemblySha256=$manifest.gameAssemblySha256})
 Write-CoopJson (Join-Path $paths.Home 'source.json') ([pscustomobject]@{repository=$repo})
 if(Test-Path -LiteralPath $paths.Journal){Remove-Item -LiteralPath $paths.Journal}
}
Reset-Fixture
Reject {Assert-CoopRepository '../bad'} 'reject path traversal repository'
Reject {Assert-CoopRepository 'https://other.invalid/repo'} 'reject arbitrary URL'
$result=Invoke-CoopUpdate $bin
Check ($result.State -eq 'Updated' -and (Get-CoopHash $paths.Dll) -eq $manifest.modSha256) 'complete update replaces only verified mod'
Check ((Read-CoopJson $paths.State).version -eq '1.1.0') 'installed version committed'
Check ((Get-Content (Join-Path $bin 'Mods\Localization.txt') -Raw) -eq 'keep') 'other mods preserved'
Check (@(Get-ChildItem $paths.Home -Filter 'backup-*.dll').Count -eq 1) 'previous DLL backup retained'
Check ((Invoke-CoopUpdate $bin).State -eq 'Current') 'same version no replacement'
$manifest.version='1.0.0';$release.tag_name='v1.0.0';foreach($asset in $release.assets){$asset.browser_download_url=$asset.browser_download_url.Replace('v1.1.0','v1.0.0')}
Check ((Invoke-CoopUpdate $bin).State -eq 'Current') 'older release never downgrades installed version'
$manifest.version='1.1.0';$release.tag_name='v1.1.0';foreach($asset in $release.assets){$asset.browser_download_url=$asset.browser_download_url.Replace('v1.0.0','v1.1.0')}
Reset-Fixture;$oldHash=Get-CoopHash $paths.Dll
$script:running=$true;Reject {Invoke-CoopUpdate $bin} 'running game blocks updates';$script:running=$false
Check ((Get-CoopHash $paths.Dll) -eq $oldHash) 'running game leaves DLL untouched'
$script:offline=$true;Reject {Invoke-CoopUpdate $bin} 'offline error reported';$script:offline=$false
Check ((Get-CoopHash $paths.Dll) -eq $oldHash) 'offline preserves installed mod'
$release.prerelease=$true;Reject {Invoke-CoopUpdate $bin} 'prerelease rejected';$release.prerelease=$false
$release.draft=$true;Reject {Invoke-CoopUpdate $bin} 'draft rejected';$release.draft=$false
$url=$release.assets[1].browser_download_url;$release.assets[1].browser_download_url='https://evil.invalid/GhpcCoopLab.dll'
Reject {Invoke-CoopUpdate $bin} 'asset outside repository rejected';$release.assets[1].browser_download_url=$url
$manifest.minUpdaterVersion=2;Reject {Invoke-CoopUpdate $bin} 'newer updater requirement blocks apply';$manifest.minUpdaterVersion=1
$hash=$manifest.gameAssemblySha256;$manifest.gameAssemblySha256=('A'*64);Reject {Invoke-CoopUpdate $bin} 'wrong game build rejected';$manifest.gameAssemblySha256=$hash
$hash=$manifest.loaderAssemblySha256;$manifest.loaderAssemblySha256=('B'*64);Reject {Invoke-CoopUpdate $bin} 'wrong loader rejected';$manifest.loaderAssemblySha256=$hash
[IO.File]::WriteAllBytes((Join-Path $server 'GhpcCoopLab.dll'),$oldBytes);Reject {Invoke-CoopUpdate $bin} 'corrupted download rejected';[IO.File]::WriteAllBytes((Join-Path $server 'GhpcCoopLab.dll'),$newBytes)
Check ((Get-CoopHash $paths.Dll) -eq $oldHash) 'invalid releases preserve existing DLL'
$fileLock=[IO.File]::Open((Join-Path $paths.Home 'update.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{Reject {Invoke-CoopUpdate $bin} 'concurrent updater rejected'}finally{$fileLock.Dispose()}
[IO.File]::WriteAllText($paths.Dll,'user-modified');Reject {Invoke-CoopUpdate $bin} 'externally modified DLL preserved';Reset-Fixture
$script:originalWrite=${function:Write-CoopJson};$script:failed=$false
function Write-CoopJson([string]$Path,$Value){if($Path -eq $paths.State -and $Value.version -eq '1.1.0' -and !$script:failed){$script:failed=$true;throw 'Injected state commit failure'};& $script:originalWrite $Path $Value}
Reject {Invoke-CoopUpdate $bin} 'commit failure triggers rollback'
Check ((Get-CoopHash $paths.Dll) -eq $oldHash -and (Read-CoopJson $paths.State).version -eq '1.0.0') 'DLL and version restored after commit failure'
Set-Item Function:Write-CoopJson $script:originalWrite
$prev=Read-CoopJson $paths.State;$backupName='backup-'+[guid]::NewGuid().ToString('N')+'.dll'
Copy-Item -LiteralPath $paths.Dll -Destination (Join-Path $paths.Home $backupName)
$next=[pscustomobject]@{version='1.1.0';modSha256=$manifest.modSha256;gameAssemblySha256=$manifest.gameAssemblySha256}
Write-CoopJson $paths.Journal ([pscustomobject]@{previous=$prev;next=$next;backup=$backupName})
Copy-Item -LiteralPath (Join-Path $server 'GhpcCoopLab.dll') -Destination $paths.Dll
Repair-CoopPending $paths
Check ((Read-CoopJson $paths.State).version -eq '1.1.0' -and !(Test-Path $paths.Journal)) 'power-loss recovery after atomic DLL swap'
Reset-Fixture;Write-CoopJson $paths.Journal ([pscustomobject]@{previous=$prev;next=$next;backup=$backupName});Repair-CoopPending $paths
Check ((Read-CoopJson $paths.State).version -eq '1.0.0') 'interruption before swap retains old version'
Write-CoopJson (Join-Path $paths.Home 'source.json') ([pscustomobject]@{repository=''})
Check ((Invoke-CoopUpdate $bin).State -eq 'Unconfigured') 'missing repository honestly reports not connected'
Check (@(Get-ChildItem $paths.Home -Directory -Filter 'download-*').Count -eq 0) 'temporary downloads cleaned after success and failure'
Write-Output "ALL $script:checks UPDATER CHECKS PASSED. Fixture network only; live GitHub release not configured."
