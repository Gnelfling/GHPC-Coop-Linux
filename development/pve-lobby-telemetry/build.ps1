param([Parameter(Mandatory = $true)][string]$GameBin, [string]$CacheDir, [string]$OutputDir, [switch]$RunTests)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build-repro.ps1') -GameBin $GameBin -CacheDir $CacheDir -OutputDir $OutputDir -RunTests:$RunTests
