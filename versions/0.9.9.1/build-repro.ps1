param([Parameter(Mandatory=$true)][string]$GameBin,[string]$CacheDir,[string]$OutputDir,[switch]$RunTests)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if(!$CacheDir){$CacheDir=Join-Path $root '.build-cache'}
if(!$OutputDir){$OutputDir=Join-Path $root 'dist'}
New-Item -ItemType Directory -Force -Path $CacheDir,$OutputDir | Out-Null
$lock=Get-Content -LiteralPath (Join-Path $root 'build-lock.json') -Raw | ConvertFrom-Json
function Check-Hash([string]$Path,[string]$Expected){if(!(Test-Path -LiteralPath $Path) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected){throw "Build input hash mismatch: $Path"}}
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
foreach($tool in $lock.tools){
 $archive=Join-Path $CacheDir ($tool.name+'.zip')
 if(!(Test-Path -LiteralPath $archive)){Invoke-WebRequest -UseBasicParsing -Uri $tool.url -OutFile $archive}
 Check-Hash $archive $tool.sha256
}
# Fresh extraction on each build avoids trusting modified cached executables.
$stage=Join-Path $CacheDir ('work-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage,(Join-Path $stage 'src'),(Join-Path $stage 'refs'),(Join-Path $stage 'bin') | Out-Null
foreach($tool in $lock.tools){Expand-Archive -LiteralPath (Join-Path $CacheDir ($tool.name+'.zip')) -DestinationPath (Join-Path $stage $tool.name)}
$utf8=New-Object System.Text.UTF8Encoding($false)
$sources=@()
foreach($name in $lock.sources){
 $source=Join-Path $root ('src\'+$name);$target=Join-Path $stage ('src\'+$name)
 # Canonical UTF-8/LF sources make Git checkout newline settings irrelevant.
 $text=[IO.File]::ReadAllText($source).Replace("`r`n","`n").Replace("`r","`n")
 [IO.File]::WriteAllText($target,$text,$utf8);$sources+=$target
}
$refs=@()
foreach($ref in $lock.references){
 $original=Join-Path $GameBin $ref.path;Check-Hash $original $ref.sha256
 $target=Join-Path $stage ('refs\'+$ref.name);Copy-Item -LiteralPath $original -Destination $target
 $refs+=('/r:'+$target)
}
$framework=Join-Path $stage 'framework\build\.NETFramework\v4.7.2'
foreach($name in @('mscorlib.dll','System.dll','System.Core.dll')){$refs+=('/r:'+(Join-Path $framework $name))}
$asset=Join-Path $root 'assets\menu-hero.png';Check-Hash $asset $lock.resource.sha256
Copy-Item -LiteralPath $asset -Destination (Join-Path $stage 'menu-hero.png')
$compiler=Join-Path $stage 'compiler\tasks\net472\csc.exe'
$output=Join-Path $stage 'bin\GhpcCoopLab.dll'
$args=@('/nologo','/noconfig','/nostdlib+','/target:library','/optimize+','/deterministic+','/debug-','/langversion:7.3','/platform:anycpu','/utf8output',('/pathmap:'+$stage+'=/_/ghpc'),('/out:'+$output),('/resource:'+(Join-Path $stage 'menu-hero.png')+',GhpcCoop.MenuHero.png'))
& $compiler @args @refs @sources
if($LASTEXITCODE -ne 0){throw 'Deterministic compilation failed'}
$destination=Join-Path $OutputDir 'GhpcCoopLab.dll';Copy-Item -LiteralPath $output -Destination $destination -Force
$hash=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDir 'GhpcCoopLab.dll.sha256'),$hash+"  GhpcCoopLab.dll`n",$utf8)
Write-Output "Built SHA-256: $hash"
if($lock.expectedDllSha256 -and $hash -ne $lock.expectedDllSha256){throw 'DLL differs from the published expected hash. Keep the output for diagnosis; do not install it as the release build.'}
# Build stage is retained for review; it can be removed manually after inspection.
if($RunTests){
 $testRefs=@('mscorlib.dll','System.dll','System.Core.dll') | ForEach-Object {'/r:'+(Join-Path $framework $_)}
 $common=@('Protocol.cs','ILink.cs','SteamRoomPolicy.cs','RoomSeats.cs') | ForEach-Object {Join-Path $root ('src\'+$_)}
 $test=Join-Path $stage 'bin\CoopSelfTest.exe'
 & $compiler /nologo /noconfig /nostdlib+ /target:exe ('/out:'+$test) @testRefs @common (Join-Path $root 'src\Transport.cs') (Join-Path $root 'src\MultiRoom.cs') (Join-Path $root 'tests\SelfTest.cs')
 if($LASTEXITCODE -ne 0){throw 'Protocol tests compilation failed'}
 Push-Location (Join-Path $stage 'bin')
 try{& $test;if($LASTEXITCODE -ne 0){throw 'Protocol tests failed'}}finally{Pop-Location}
 $test=Join-Path $stage 'bin\SteamAdapterTests.exe'
 & $compiler /nologo /noconfig /nostdlib+ /target:exe ('/out:'+$test) @testRefs @common (Join-Path $root 'src\SteamLink.cs') (Join-Path $root 'tests\SteamAdapterTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Steam fixture compilation failed'}
 & $test;if($LASTEXITCODE -ne 0){throw 'Steam fixture failed'}
}
