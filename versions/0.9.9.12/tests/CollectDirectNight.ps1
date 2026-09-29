param([switch]$Stop)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = Get-Content (Join-Path $root 'active-night-run.json') -Raw | ConvertFrom-Json
$now = Get-Date -Format HHmmss
foreach ($p in $run.Processes)
{
    $dest = Join-Path $run.Output ($p.Role + '-' + $now);
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    foreach ($rel in @(
            'MelonLoader/Latest.log', 'UserData/GhpcCoop/network-verification.txt', 'UserData/GhpcCoop/network-vehicles.tsv', 'UserData/GhpcCoop/shot-audit.tsv', 'UserData/GhpcCoop/replica-report.json', 'UserData/GhpcCoop/roster-diagnostic.txt', 'UserData/GhpcCoop/last-error.txt'))
    {
        $file = Join-Path $p.Folder $rel;
        if (Test-Path -LiteralPath $file)
        {
            Copy-Item -LiteralPath $file -Destination $dest
        }
    }
    $log = Join-Path $dest 'Latest.log';
    $lines = if (Test-Path $log)
    {
        Get-Content $log
    }
    else
    {
        @()
    }
    $report = Join-Path $dest 'network-verification.txt'
    [pscustomobject]@{Role = $p.Role;
        PID = $p.PID;
        ClaimEvents = @(
            $lines | Select-String 'ROOM claimed|REPLICA started').Count;
        Errors = @(
            $lines | Select-String 'COOP .*Exception|VERIFY FAIL|ROOM .*rejected|ROOM isolated').Count;
        Report = if (Test-Path $report)
        {
            Get-Content $report -Raw
        }
        else
        {
            'No verification report yet'
        };
        Saved = $dest
    }
    if ($Stop)
    {
        $proc = Get-Process -Id $p.PID -ErrorAction SilentlyContinue;
        if ($proc -and $proc.Path -eq (Join-Path $p.Folder 'GHPC.exe'))
        {
            Stop-Process -Id $p.PID -Force;
            if (-not $proc.WaitForExit(10000))
            {
                throw "Test process did not exit"
            }
        }
    }
}
