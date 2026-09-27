function Resolve-GhpcGameBin {
 param([string]$Path)
 if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
 $Path=$Path.Trim().Trim('"')
 if (Test-Path -LiteralPath $Path -PathType Leaf) {
  if ([IO.Path]::GetFileName($Path) -ine 'GHPC.exe') { return $null }
  $Path=[IO.Path]::GetDirectoryName($Path)
 }
 foreach ($candidate in @($Path,(Join-Path $Path 'Bin'))) {
  if ((Test-Path -LiteralPath (Join-Path $candidate 'GHPC.exe') -PathType Leaf) -and
      (Test-Path -LiteralPath (Join-Path $candidate 'GHPC_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)) {
   return (Resolve-Path -LiteralPath $candidate).Path
  }
 }
 return $null
}
