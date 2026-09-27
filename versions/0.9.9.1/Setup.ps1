param([string]$GameBin,[switch]$Elevated)
$ErrorActionPreference='Stop'
. "$PSScriptRoot\Find-GameBin.ps1"
try {
 if([string]::IsNullOrWhiteSpace($GameBin)) {
  $libraries=New-Object 'System.Collections.Generic.List[string]'
  foreach($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
   $r=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
   foreach($root in @($r.SteamPath,$r.InstallPath)) {if($root -and (Test-Path -LiteralPath $root)) {$libraries.Add($root)}}
  }
  foreach($root in @($libraries.ToArray())) {
   $vdf=Join-Path $root 'steamapps\libraryfolders.vdf'
   if(Test-Path -LiteralPath $vdf) {foreach($m in [regex]::Matches([IO.File]::ReadAllText($vdf),'"path"\s+"([^"]+)"')) {$libraries.Add($m.Groups[1].Value.Replace('\\','\'))}}
  }
  $found=@(foreach($lib in ($libraries | Select-Object -Unique)) {
   $manifest=Join-Path $lib 'steamapps\appmanifest_1705180.acf'
   if(Test-Path -LiteralPath $manifest) {
    $m=[regex]::Match([IO.File]::ReadAllText($manifest),'"installdir"\s+"([^"]+)"')
    if($m.Success){$candidate=Resolve-GhpcGameBin (Join-Path $lib ('steamapps\common\'+$m.Groups[1].Value));if($candidate){$candidate}}
   }
  })
  $found=@($found | Select-Object -Unique)
  if($found.Count -eq 1){$GameBin=$found[0]}else{
   Add-Type -AssemblyName System.Windows.Forms
   $picker=New-Object System.Windows.Forms.OpenFileDialog
   $picker.Title='Select GHPC.exe inside your installed game folder (Gunner HEAT PC / Bin)'
   $picker.Filter='GHPC game executable (GHPC.exe)|GHPC.exe'
   $picker.CheckFileExists=$true
   if($found.Count -gt 0){$picker.InitialDirectory=$found[0]}
   try {
    do {
     if($picker.ShowDialog() -ne 'OK'){throw 'Installation cancelled. No files changed.'}
     $GameBin=Resolve-GhpcGameBin $picker.FileName
     if(!$GameBin){[void][System.Windows.Forms.MessageBox]::Show('Select the installed game GHPC.exe next to GHPC_Data. Do not select the downloaded setup folder.','GHPC game not found')}
    } while(!$GameBin)
   } finally {$picker.Dispose()}
  }
 }
 $GameBin=Resolve-GhpcGameBin $GameBin
 if(!$GameBin){throw 'Game not found. Select GHPC.exe in the installed game Bin folder.'}
 & "$PSScriptRoot\Install.ps1" -GameBin $GameBin -ValidateOnly
 if(Get-Process GHPC -ErrorAction SilentlyContinue){throw 'Close GHPC before installing. Setup will not close the game for you.'}
 $admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
 if(!$admin){
  if($Elevated){throw 'Administrator access was not granted.'}
  $args='-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -GameBin "'+$GameBin+'" -Elevated'
  $child=Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $args -Wait -PassThru
  if($child.ExitCode -ne 0){throw 'Setup did not complete. Check setup-result.txt in the package folder.'}
  Get-Content -LiteralPath "$PSScriptRoot\setup-result.txt"
  exit 0
 }
 & "$PSScriptRoot\Install.ps1" -GameBin $GameBin
 & "$PSScriptRoot\Install-Updater.ps1" -GameBin $GameBin
 & "$PSScriptRoot\Create-Launcher.ps1" -GameBin $GameBin
 'Steam co-op preview installed. No firewall or router changes made. Sign in to Steam before playing.' | Tee-Object -FilePath "$PSScriptRoot\setup-result.txt" -Append
}catch{
 $_.Exception.Message | Set-Content -LiteralPath "$PSScriptRoot\setup-result.txt"
 Write-Error $_
 exit 1
}
