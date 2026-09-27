param([string]$GameBin, [switch]$ValidateOnly)
function Get-CoopPackageFileHash([string]$LiteralPath,[ValidateSet('SHA256','SHA512')][string]$Algorithm='SHA256') {
 $stream=$null;$hasher=$null
 try {
  $stream=[IO.File]::OpenRead($LiteralPath)
  if($Algorithm -eq 'SHA512'){$hasher=[Security.Cryptography.SHA512]::Create()}else{$hasher=[Security.Cryptography.SHA256]::Create()}
  [pscustomobject]@{Hash=[BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','')}
 } finally {if($hasher){$hasher.Dispose()};if($stream){$stream.Dispose()}}
}
$ErrorActionPreference = 'Stop'
$package = $PSScriptRoot
$loaderZip = Join-Path $package 'MelonLoader.x64.zip'
$expectedLoader = (Get-Content -LiteralPath (Join-Path $package 'MelonLoader.x64.sha512') -Raw).Trim()
if ((Get-CoopPackageFileHash -LiteralPath $loaderZip -Algorithm SHA512).Hash -ne $expectedLoader) { throw 'The bundled MelonLoader archive is damaged.' }
$expectedMod = (Get-Content -LiteralPath (Join-Path $package 'coop-sha256.txt') -Raw).Trim()
if ((Get-CoopPackageFileHash -LiteralPath (Join-Path $package 'GhpcCoopLab.dll') -Algorithm SHA256).Hash -ne $expectedMod) { throw 'The co-op mod is damaged.' }
if ([string]::IsNullOrWhiteSpace($GameBin)) { $GameBin = (Read-Host 'Enter the Bin folder containing GHPC.exe').Trim().Trim('"') }
$destination = (Resolve-Path -LiteralPath $GameBin).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $destination 'GHPC.exe') -PathType Leaf)) { throw 'Select the Bin directory containing GHPC.exe.' }
if ($destination -match '[^\x00-\x7F]') { throw 'Use an ASCII-only path for the game copy.' }
$assembly = Join-Path $destination 'GHPC_Data\Managed\Assembly-CSharp.dll'
$expected = (Get-Content -LiteralPath (Join-Path $package 'game-build-sha256.txt') -Raw).Trim()
if ((Get-CoopPackageFileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $expected) { throw 'Unsupported GHPC build. This package targets GHPC 20260814.1.' }
if ($ValidateOnly) { Write-Output 'Package integrity and game build verified. No files changed.'; return }
if (Get-Process GHPC -ErrorAction SilentlyContinue) { throw 'Close GHPC before installing. Setup will not close the game for you.' }
# Only these loader paths and the co-op DLL may be replaced. Other mods remain untouched.
$paths = @('MelonLoader','version.dll','dobby.dll','NOTICE.txt','Mods\GhpcCoopLab.dll')
foreach ($relative in $paths) {
    $target = [IO.Path]::GetFullPath((Join-Path $destination $relative))
    if (!$target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation target.' }
    if (Test-Path -LiteralPath $target) {
        if ((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Cannot replace a linked path: ' + $relative) }
    }
}
$mods = Join-Path $destination 'Mods'
if ((Test-Path -LiteralPath $mods) -and ((Get-Item -LiteralPath $mods -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Mods must be a normal directory.' }
$backup = Join-Path $destination ('CoopBackup-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup | Out-Null
$stage = Join-Path $backup 'NewLoader'
Expand-Archive -LiteralPath $loaderZip -DestinationPath $stage
if (!(Test-Path -LiteralPath (Join-Path $stage 'MelonLoader\net35\MelonLoader.dll'))) { throw 'Incomplete loader archive.' }
$installCopiesStarted = $false
$moved = New-Object 'System.Collections.Generic.List[string]'
try {
    foreach ($relative in $paths) {
        $target = Join-Path $destination $relative
        if (Test-Path -LiteralPath $target) {
            $saved = Join-Path $backup ('Previous\' + $relative)
            New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
            Move-Item -LiteralPath $target -Destination $saved
            $moved.Add($relative)
        }
    }
    $installCopiesStarted = $true
    foreach ($relative in @('MelonLoader','version.dll','dobby.dll','NOTICE.txt')) {
        Copy-Item -LiteralPath (Join-Path $stage $relative) -Destination (Join-Path $destination $relative) -Recurse
    }
    New-Item -ItemType Directory -Path $mods -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $package 'GhpcCoopLab.dll') -Destination (Join-Path $mods 'GhpcCoopLab.dll')
} catch {
    # Preserve any partial new files separately before restoring originals.
    foreach ($relative in $paths) {
        $target = Join-Path $destination $relative
        if (Test-Path -LiteralPath $target) {
            $failed = Join-Path $backup ('FailedInstall\' + $relative)
            New-Item -ItemType Directory -Path (Split-Path -Parent $failed) -Force | Out-Null
            # Do not move untouched originals when backup creation failed midway.
            if ($moved.Contains($relative) -or !(Test-Path -LiteralPath (Join-Path $backup ('Previous\' + $relative)))) {
                if ($installCopiesStarted) { Move-Item -LiteralPath $target -Destination $failed }
            }
        }
    }
    foreach ($relative in $moved) {
        $target = Join-Path $destination $relative
        if (!(Test-Path -LiteralPath $target)) { Move-Item -LiteralPath (Join-Path $backup ('Previous\' + $relative)) -Destination $target }
    }
    throw
}
Write-Output 'GHPC Co-op 0.9.9.5 and MelonLoader 0.6.1 x64 installed.'
Write-Output ('Backup: ' + $backup)
Write-Output 'Korean localization is not required. Existing localization and other mods were not changed.'



