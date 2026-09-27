param([Parameter(Mandatory=$true)][string]$GameBin,[int]$Port=22222)
$ErrorActionPreference='Stop'
if($Port -lt 1 -or $Port -gt 65535){throw 'Invalid TCP port.'}
$exe=(Resolve-Path -LiteralPath (Join-Path $GameBin 'GHPC.exe')).Path
$hash=[Security.Cryptography.SHA256]::Create()
try{$id=([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($exe.ToLowerInvariant())))).Replace('-','').Substring(0,16)}finally{$hash.Dispose()}
$name="GHPC-Coop-$id-TCP-$Port"
try{
 $existing=Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue
 if($existing){
  $application=$existing | Get-NetFirewallApplicationFilter
  $filter=$existing | Get-NetFirewallPortFilter
  if($application.Program -ne $exe -or $filter.LocalPort -ne "$Port" -or $filter.Protocol -notin @('TCP',6)){throw 'An unrelated rule uses the same name; left unchanged.'}
  $existing | Set-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -Profile Any
 }else{New-NetFirewallRule -Name $name -DisplayName "GHPC Co-op TCP $Port ($id)" -Direction Inbound -Action Allow -Program $exe -Protocol TCP -LocalPort $Port -Profile Any | Out-Null}
 "Firewall: allowed TCP $Port for GHPC only."
}catch{"Firewall: FAILED - $($_.Exception.Message)"; 'Host needs manual firewall setup.'}
try{
 $configs=@(Get-NetIPConfiguration | Where-Object {$_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' -and $_.IPv4Address.IPAddress -notlike '169.254.*'})
 if($configs.Count -ne 1){throw 'Multiple or missing network routes. Check VPN/network selection; automatic router setup skipped.'}
 $local=@($configs[0].IPv4Address.IPAddress)[0]
 "Host network address: $local"
 $nat=New-Object -ComObject HNetCfg.NATUPnP
 $maps=$nat.StaticPortMappingCollection
 if($null -eq $maps){
  $ip=[Net.IPAddress]::Parse($local).GetAddressBytes()
  $private=($ip[0] -eq 10 -or ($ip[0] -eq 172 -and $ip[1] -ge 16 -and $ip[1] -le 31) -or ($ip[0] -eq 192 -and $ip[1] -eq 168) -or ($ip[0] -eq 100 -and $ip[1] -ge 64 -and $ip[1] -le 127))
  if($private){throw 'Router automatic port mapping unavailable. Manual forwarding or ISP assistance may be required.'}
  'Router: no UPnP mapping available; interface appears publicly addressed. Forwarding may not be necessary.'
 }else{
  $old=@($maps | Where-Object {$_.ExternalPort -eq $Port -and $_.Protocol -eq 'TCP'})
  if($old.Count -gt 0){
   if($old[0].InternalClient -ne $local -or $old[0].InternalPort -ne $Port -or !$old[0].Enabled){throw 'Port already mapped elsewhere or disabled; existing mapping left unchanged.'}
   "Router: existing TCP $Port mapping matches this PC."
  }else{
   $mapping=$maps.Add($Port,'TCP',$Port,$local,$true,'GHPC Co-op')
   if(!$mapping -or !$mapping.Enabled){throw 'Router did not confirm the port mapping.'}
   "Router: TCP $Port mapped to ${local}:$Port. Router reboot/DHCP changes may require running setup again."
  }
 }
}catch{"Router: NOT CONFIGURED - $($_.Exception.Message)"}
'Internet access is UNVERIFIED. Test with a player outside your home network.'
