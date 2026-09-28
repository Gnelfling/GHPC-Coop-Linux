param([Parameter(Mandatory = $true)][string]$GameBin)
$ErrorActionPreference = 'Stop'
$bin = (Resolve-Path -LiteralPath $GameBin).Path
$launcher = Join-Path $bin 'UserData\GhpcCoop\Updater\Launcher.ps1'
if (!(Test-Path -LiteralPath $launcher))
{
    throw 'Install the co-op launcher first.'
}
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
foreach ($entry in @(
        @(
            'GHPC Co-op.lnk', ' -Play'), @(
            'GHPC Co-op Manager.lnk', '')))
{
    $path = Join-Path $desktop $entry[0]
    if (Test-Path -LiteralPath $path)
    {
        Copy-Item -LiteralPath $path -Destination ($path + '.backup-' + [guid]::NewGuid().ToString('N'))
    }
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $link.Arguments = '-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $launcher + '"' + $entry[1]
    $link.WorkingDirectory = $bin;
    $link.IconLocation = (Join-Path $bin 'GHPC.exe') + ',0'
    $link.Description = 'GHPC co-op launcher: check updates, then play. Keep Steam running.'
    $link.Save()
}
Write-Output 'Created GHPC Co-op and GHPC Co-op Manager desktop shortcuts.'
