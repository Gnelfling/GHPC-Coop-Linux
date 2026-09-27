# Windows PowerShell 5.1. Downloads data and a verified DLL, never remote scripts.
$script:UpdaterVersion=1
function Get-CoopHash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant() }
function Read-CoopJson([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Write-CoopJson([string]$Path,$Value) {
 $temp=$Path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try {
  [IO.File]::WriteAllText($temp,($Value | ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
  if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temp,$Path,[NullString]::Value)}else{[IO.File]::Move($temp,$Path)}
 } finally {if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
}
function Assert-CoopStopped {
 if(Get-Process GHPC -ErrorAction SilentlyContinue){throw 'Close GHPC before updating. Your game has not been stopped.'}
}
function Assert-CoopRepository([string]$Repository) {
 if($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$'){throw 'A valid GitHub owner/repository is required.'}
}
function Get-CoopPaths([string]$GameBin) {
 $bin=(Resolve-Path -LiteralPath $GameBin).Path.TrimEnd('\')
 if(!(Test-Path -LiteralPath (Join-Path $bin 'GHPC.exe') -PathType Leaf)){throw 'GHPC.exe was not found.'}
 $updaterDir=Join-Path $bin 'UserData\GhpcCoop\Updater'
 foreach($p in @('Mods','UserData','UserData\GhpcCoop','UserData\GhpcCoop\Updater','Mods\GhpcCoopLab.dll')){
  $path=Join-Path $bin $p
  if((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Update target must not be a linked file or directory.'}
 }
 [pscustomobject]@{Bin=$bin;Home=$updaterDir;Dll=(Join-Path $bin 'Mods\GhpcCoopLab.dll');State=(Join-Path $updaterDir 'installed.json');Journal=(Join-Path $updaterDir 'pending.json')}
}
function Receive-CoopFile([string]$Url,[string]$Path,[long]$Limit) {
 $uri=[uri]$Url
 if($uri.Scheme -ne 'https'){throw 'Only HTTPS downloads are allowed.'}
 [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
 $request=[Net.HttpWebRequest]::Create($uri)
 $request.UserAgent='GHPC-Coop-Updater/1';$request.Accept='application/vnd.github+json'
 $request.Timeout=15000;$request.ReadWriteTimeout=15000;$request.MaximumAutomaticRedirections=5
 $response=$null;$inputStream=$null;$outputStream=$null
 try {
  $response=$request.GetResponse()
  if($response.ResponseUri.Scheme -ne 'https'){throw 'Download redirected outside HTTPS.'}
  if($response.ContentLength -gt $Limit){throw 'Download exceeds the allowed size.'}
  $inputStream=$response.GetResponseStream();$outputStream=[IO.File]::Create($Path)
  $buffer=New-Object byte[] 65536;$total=0;$timer=[Diagnostics.Stopwatch]::StartNew()
  while(($read=$inputStream.Read($buffer,0,$buffer.Length)) -gt 0){
   $total+=$read;if($total -gt $Limit -or $timer.Elapsed.TotalSeconds -gt 60){throw 'Download size or time limit exceeded.'}
   $outputStream.Write($buffer,0,$read)
  }
 } finally {if($outputStream){$outputStream.Dispose()};if($inputStream){$inputStream.Dispose()};if($response){$response.Dispose()}}
}
function Get-CoopRelease([string]$Repository,[string]$Stage) {
 Assert-CoopRepository $Repository
 $file=Join-Path $Stage 'release.json'
 Receive-CoopFile "https://api.github.com/repos/$Repository/releases/latest" $file 2097152
 $release=Read-CoopJson $file
 if($release.draft -ne $false -or $release.prerelease -ne $false){throw 'Only published stable releases are accepted.'}
 if($release.tag_name -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$'){throw 'Release must use a numeric v-prefixed version tag.'}
 foreach($name in @('coop-update.json','GhpcCoopLab.dll')){
  $assets=@($release.assets | Where-Object {$_.name -ceq $name -and $_.state -eq 'uploaded'})
  if($assets.Count -ne 1){throw "Release is missing one unique $name asset."}
  $expected="https://github.com/$Repository/releases/download/$($release.tag_name)/$name"
  if($assets[0].browser_download_url -cne $expected){throw 'Release asset points outside the configured repository.'}
 }
 $release
}
function Assert-CoopManifest($Manifest,$Release) {
 if($Manifest.schema -ne 1 -or $Manifest.minUpdaterVersion -gt $script:UpdaterVersion -or $Manifest.minUpdaterVersion -lt 1){throw 'This release needs a newer launcher. Install the complete setup package.'}
 if($Manifest.version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$' -or ('v'+$Manifest.version) -cne $Release.tag_name){throw 'Release and manifest versions differ.'}
 $null=[version]$Manifest.version
 foreach($field in @('modSha256','gameAssemblySha256','loaderAssemblySha256')){if($Manifest.$field -notmatch '^[0-9a-fA-F]{64}$'){throw "Invalid $field in update manifest."}}
 if($Manifest.modBytes -lt 1024 -or $Manifest.modBytes -gt 33554432){throw 'Invalid mod download size.'}
 $asset=@($Release.assets | Where-Object {$_.name -ceq 'GhpcCoopLab.dll'})[0]
 if($asset.size -ne $Manifest.modBytes){throw 'Release asset size differs from manifest.'}
 if($asset.digest -and $asset.digest -ine ('sha256:'+$Manifest.modSha256)){throw 'GitHub asset digest differs from manifest.'}
}
function Assert-CoopCompatible($Paths,$Manifest) {
 if((Get-CoopHash (Join-Path $Paths.Bin 'GHPC_Data\Managed\Assembly-CSharp.dll')) -ne $Manifest.gameAssemblySha256){throw 'This update targets a different GHPC build. Installed mod was preserved.'}
 if((Get-CoopHash (Join-Path $Paths.Bin 'MelonLoader\net35\MelonLoader.dll')) -ne $Manifest.loaderAssemblySha256){throw 'This update needs a different MelonLoader. Install the complete setup package.'}
}
function Repair-CoopPending($Paths) {
 if(!(Test-Path -LiteralPath $Paths.Journal)){return}
 Assert-CoopStopped
 $j=Read-CoopJson $Paths.Journal
 if($j.backup -notmatch '^backup-[a-f0-9]{32}\.dll$' -or $j.previous.modSha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Invalid interrupted-update record. Restore your backup manually.'}
 $hash=Get-CoopHash $Paths.Dll
 if($hash -eq $j.next.modSha256){Write-CoopJson $Paths.State $j.next}
 elseif($hash -eq $j.previous.modSha256){Write-CoopJson $Paths.State $j.previous}
 else{throw 'Interrupted update contains an unknown DLL. No files changed; inspect the backup.'}
 Remove-Item -LiteralPath $Paths.Journal
}
function Install-CoopCandidate($Paths,$Manifest,[string]$Candidate) {
 Assert-CoopStopped
 Assert-CoopCompatible $Paths $Manifest
 if((Get-Item -LiteralPath $Candidate).Length -ne $Manifest.modBytes -or (Get-CoopHash $Candidate) -ne $Manifest.modSha256){throw 'Downloaded mod failed integrity verification. Installed mod was preserved.'}
 $previous=Read-CoopJson $Paths.State
 if((Get-CoopHash $Paths.Dll) -ne $previous.modSha256){throw 'Installed mod differs from its recorded version. Reinstall the complete setup to repair it.'}
 if([version]$Manifest.version -le [version]$previous.version){throw 'Automatic downgrade or same-version replacement is not allowed.'}
 $next=[pscustomobject]@{version=$Manifest.version;modSha256=$Manifest.modSha256;gameAssemblySha256=$Manifest.gameAssemblySha256;updatedUtc=[DateTime]::UtcNow.ToString('o')}
 $backupName='backup-'+[guid]::NewGuid().ToString('N')+'.dll';$backup=Join-Path $Paths.Home $backupName
 $stage=Join-Path (Split-Path $Paths.Dll -Parent) ('CoopUpdate-'+[guid]::NewGuid().ToString('N')+'.tmp')
 $replaced=$false
 try {
  Copy-Item -LiteralPath $Candidate -Destination $stage
  if((Get-CoopHash $stage) -ne $Manifest.modSha256){throw 'Staging integrity check failed.'}
  Write-CoopJson $Paths.Journal ([pscustomobject]@{previous=$previous;next=$next;backup=$backupName})
  Assert-CoopStopped
  [IO.File]::Replace($stage,$Paths.Dll,$backup);$replaced=$true
  if((Get-CoopHash $Paths.Dll) -ne $Manifest.modSha256){throw 'Installed DLL verification failed.'}
  Write-CoopJson $Paths.State $next
  Remove-Item -LiteralPath $Paths.Journal
 } catch {
  $problem=$_
  if($replaced){
   if((Get-CoopHash $backup) -ne $previous.modSha256){throw 'Backup integrity failure. Do not start the game; restore the setup package.'}
   Copy-Item -LiteralPath $backup -Destination $stage
   [IO.File]::Replace($stage,$Paths.Dll,[NullString]::Value)
   Write-CoopJson $Paths.State $previous
  }
  if(Test-Path -LiteralPath $Paths.Journal){Remove-Item -LiteralPath $Paths.Journal}
  throw $problem
 } finally {if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage}}
}
function Invoke-CoopUpdate([string]$GameBin,[scriptblock]$Status={param($s)}) {
 $paths=Get-CoopPaths $GameBin
 # An on-disk exclusive lock also covers different aliases to the same game folder.
 $lock=$null;$stage=$null
 try {
  $lock=[IO.File]::Open((Join-Path $paths.Home 'update.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
  Assert-CoopStopped;Repair-CoopPending $paths
  $config=Read-CoopJson (Join-Path $paths.Home 'source.json')
  if([string]::IsNullOrWhiteSpace($config.repository)){return [pscustomobject]@{State='Unconfigured';Message='Online updates are not connected yet. Your installed game can still be launched.'}}
  Assert-CoopRepository $config.repository
  $stage=Join-Path $paths.Home ('download-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $stage | Out-Null
  & $Status 'Checking the latest release...'
  $release=Get-CoopRelease $config.repository $stage
  $manifestAsset=@($release.assets | Where-Object {$_.name -ceq 'coop-update.json'})[0]
  $file=Join-Path $stage 'coop-update.json';Receive-CoopFile $manifestAsset.browser_download_url $file 65536
  $manifest=Read-CoopJson $file;Assert-CoopManifest $manifest $release
  $installed=Read-CoopJson $paths.State
  if((Get-CoopHash $paths.Dll) -ne $installed.modSha256){throw 'Installed mod has changed outside the launcher. Reinstall the complete setup package.'}
  if([version]$manifest.version -le [version]$installed.version){
   if($manifest.version -eq $installed.version -and $manifest.modSha256 -ne $installed.modSha256){throw 'The published version was replaced. A new version number is required.'}
   return [pscustomobject]@{State='Current';Message=('Installed version '+$installed.version+' is up to date.')}
  }
  Assert-CoopCompatible $paths $manifest
  & $Status ('Downloading version '+$manifest.version+'...')
  $asset=@($release.assets | Where-Object {$_.name -ceq 'GhpcCoopLab.dll'})[0]
  $candidate=Join-Path $stage 'GhpcCoopLab.dll';Receive-CoopFile $asset.browser_download_url $candidate 33554432
  & $Status 'Verifying and installing. The previous version will be backed up...'
  Install-CoopCandidate $paths $manifest $candidate
  [pscustomobject]@{State='Updated';Message=('Updated to '+$manifest.version+'. Previous version backed up.')}
 } finally {
  if($stage -and (Test-Path -LiteralPath $stage)){
   # Only delete the exact generated download directory inside this updater home.
   $full=[IO.Path]::GetFullPath($stage);$parent=[IO.Path]::GetFullPath($paths.Home).TrimEnd('\')+'\'
   if($full.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $full -Leaf) -match '^download-[a-f0-9]{32}$'){Remove-Item -LiteralPath $full -Recurse -Force}
  }
  if($lock){$lock.Dispose()}
 }
}
