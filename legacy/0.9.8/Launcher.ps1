param([switch]$Play)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Update-Core.ps1')
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$gameBin=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$form=New-Object Windows.Forms.Form
$form.Text='GHPC Co-op Launcher';$form.ClientSize=New-Object Drawing.Size(650,360)
$form.StartPosition='CenterScreen';$form.FormBorderStyle='FixedDialog';$form.MaximizeBox=$false
$form.Font=New-Object Drawing.Font('Segoe UI',10)
$title=New-Object Windows.Forms.Label
$title.Text='GHPC Unofficial Co-op';$title.Font=New-Object Drawing.Font('Segoe UI',20,[Drawing.FontStyle]::Bold)
$title.SetBounds(24,20,590,45);$form.Controls.Add($title)
$versionLabel=New-Object Windows.Forms.Label;$versionLabel.SetBounds(26,72,590,28);$form.Controls.Add($versionLabel)
$status=New-Object Windows.Forms.Label;$status.SetBounds(26,110,590,75);$form.Controls.Add($status)
$repoLabel=New-Object Windows.Forms.Label;$repoLabel.Text='Update repository (publisher GitHub URL or owner/repository)'
$repoLabel.SetBounds(26,190,590,25);$form.Controls.Add($repoLabel)
$repoBox=New-Object Windows.Forms.TextBox;$repoBox.SetBounds(26,218,440,29);$form.Controls.Add($repoBox)
$save=New-Object Windows.Forms.Button;$save.Text='Connect';$save.SetBounds(480,216,136,33);$form.Controls.Add($save)
$playButton=New-Object Windows.Forms.Button;$playButton.Text='Update and Play';$playButton.SetBounds(26,280,230,48);$form.Controls.Add($playButton)
$check=New-Object Windows.Forms.Button;$check.Text='Check Updates';$check.SetBounds(272,280,164,48);$form.Controls.Add($check)
$backupButton=New-Object Windows.Forms.Button;$backupButton.Text='Backups';$backupButton.SetBounds(452,280,164,48);$form.Controls.Add($backupButton)
$script:busy=$false
function Refresh-CoopLauncher {
 $paths=Get-CoopPaths $gameBin
 $state=Read-CoopJson $paths.State;$source=Read-CoopJson (Join-Path $PSScriptRoot 'source.json')
 $versionLabel.Text='Installed version: '+$state.version
 $repoBox.Text=$source.repository
 if(!$source.repository){$status.Text='Online updates are not connected yet. Connect the publisher repository once it is available.'}
 else{$status.Text='Updates are checked before launch. Keep Steam running.'}
}
function Start-CoopGame {
 Assert-CoopStopped
 $paths=Get-CoopPaths $gameBin
 if(Test-Path -LiteralPath $paths.Journal){throw 'An interrupted update must be recovered before launching. Click Check Updates.'}
 $state=Read-CoopJson $paths.State
 if((Get-CoopHash $paths.Dll) -ne $state.modSha256){throw 'Mod integrity check failed. Reinstall the complete setup package.'}
 if($state.gameAssemblySha256 -and (Get-CoopHash (Join-Path $gameBin 'GHPC_Data\Managed\Assembly-CSharp.dll')) -ne $state.gameAssemblySha256){throw 'GHPC was updated to a different build. A compatible co-op release is required.'}
 Start-Process -FilePath (Join-Path $gameBin 'GHPC.exe') -WorkingDirectory $gameBin -ArgumentList ('--melonloader.hideconsole --melonloader.basedir "'+$gameBin+'"') -WindowStyle Normal
 $script:busy=$false;$form.Close()
}
function Run-CoopLauncher([bool]$Launch) {
 if($script:busy){return};$script:busy=$true
 foreach($control in @($playButton,$check,$save,$repoBox,$backupButton)){$control.Enabled=$false}
 try {
  $result=Invoke-CoopUpdate $gameBin {param($message) $status.Text=$message;$form.Refresh();[Windows.Forms.Application]::DoEvents()}
  Refresh-CoopLauncher;$status.Text=$result.Message
  if($Launch){Start-CoopGame}
 } catch {
  $status.Text=$_.Exception.Message
  if($Launch -and !(Get-Process GHPC -ErrorAction SilentlyContinue)){
   $choice=[Windows.Forms.MessageBox]::Show(($status.Text+"`r`n`r`nLaunch the installed version instead? Other players must use the same compatible mod version."),'Update unavailable','YesNo','Warning')
   if($choice -eq 'Yes'){try{Start-CoopGame}catch{$status.Text=$_.Exception.Message}}
  }
 } finally {$script:busy=$false;foreach($control in @($playButton,$check,$save,$repoBox,$backupButton)){$control.Enabled=$true}}
}
$save.Add_Click({
 try {
  $repo=$repoBox.Text.Trim().TrimEnd('/')
  if($repo.StartsWith('https://github.com/',[StringComparison]::OrdinalIgnoreCase)){$repo=$repo.Substring(19)}
  Assert-CoopRepository $repo
  Write-CoopJson (Join-Path $PSScriptRoot 'source.json') ([pscustomobject]@{repository=$repo;channel='stable'})
  Refresh-CoopLauncher;$status.Text='Connected. Click Check Updates or Update and Play.'
 }catch{$status.Text=$_.Exception.Message}
})
$playButton.Add_Click({Run-CoopLauncher $true});$check.Add_Click({Run-CoopLauncher $false})
$backupButton.Add_Click({Start-Process explorer.exe -ArgumentList ('"'+$PSScriptRoot+'"')})
$form.Add_FormClosing({param($sender,$eventArgs) if($script:busy){$eventArgs.Cancel=$true}})
$form.Add_Shown({try{Refresh-CoopLauncher;if($Play){Run-CoopLauncher $true}}catch{$status.Text=$_.Exception.Message}})
[void]$form.ShowDialog();$form.Dispose()
