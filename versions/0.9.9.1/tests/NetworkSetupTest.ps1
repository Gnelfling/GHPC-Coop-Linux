$ErrorActionPreference='Stop'
$script=Join-Path $PSScriptRoot '..\Configure-Network.ps1'
$fixture=Join-Path $env:TEMP ('GHPC-Network-Test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Set-Content (Join-Path $fixture 'GHPC.exe') 'test fixture'
$global:ghpcNetworkTest_rule=$null;$global:ghpcNetworkTest_firewallFail=$false;$global:ghpcNetworkTest_created=0;$global:ghpcNetworkTest_added=0
function Get-NetFirewallRule {param($Name,$ErrorAction) $global:ghpcNetworkTest_rule}
function New-NetFirewallRule {param($Name,$DisplayName,$Direction,$Action,$Program,$Protocol,$LocalPort,$Profile) if($global:ghpcNetworkTest_firewallFail){throw 'test denied'};if($Program -ne (Join-Path $fixture 'GHPC.exe') -or $LocalPort -ne 22222 -or $Protocol -ne 'TCP'){throw 'Overbroad firewall rule'};$global:ghpcNetworkTest_created++}
function Get-NetIPConfiguration { $global:ghpcNetworkTest_configs }
function New-Object {param($ComObject) if($ComObject -ne 'HNetCfg.NATUPnP'){throw 'Unexpected object'};[pscustomobject]@{StaticPortMappingCollection=$global:ghpcNetworkTest_maps}}
function Config($ip){[pscustomobject]@{IPv4DefaultGateway='gateway';NetAdapter=[pscustomobject]@{Status='Up'};IPv4Address=[pscustomobject]@{IPAddress=$ip}}}
function Check($ok,$message){if(!$ok){throw $message};"PASS $message"}
$global:ghpcNetworkTest_configs=@(Config '192.168.1.20');$global:ghpcNetworkTest_maps=$null
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'NOT CONFIGURED' -and $result -match 'Manual forwarding' -and $global:ghpcNetworkTest_created -eq 1) 'private network without UPnP is not reported ready'
$global:ghpcNetworkTest_configs=@(Config '121.144.216.154')
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'publicly addressed' -and $result -match 'UNVERIFIED') 'public interface does not claim internet verification'
$global:ghpcNetworkTest_configs=@((Config '192.168.1.20'),(Config '10.5.0.2'))
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'Multiple or missing') 'ambiguous VPN routes skipped'
$global:ghpcNetworkTest_configs=@(Config '192.168.1.20')

$global:ghpcNetworkTest_maps=[System.Collections.ArrayList]@([pscustomobject]@{ExternalPort=22222;Protocol='TCP';InternalPort=22222;InternalClient='192.168.1.99';Enabled=$true})
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'left unchanged') 'another device mapping preserved'
$global:ghpcNetworkTest_maps=[System.Collections.ArrayList]@([pscustomobject]@{ExternalPort=22222;Protocol='TCP';InternalPort=22222;InternalClient='192.168.1.20';Enabled=$true})
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'existing TCP 22222 mapping matches') 'existing matching mapping reused'
$global:ghpcNetworkTest_maps=$null;$global:ghpcNetworkTest_firewallFail=$true
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'Firewall: FAILED' -and $result -match 'manual firewall') 'firewall failure remains visible'
$global:ghpcNetworkTest_firewallFail=$false
$global:ghpcNetworkTest_maps=[pscustomobject]@{ExternalPort=-1;Protocol='TCP'}
$global:ghpcNetworkTest_maps | Add-Member ScriptMethod Add {param($external,$protocol,$internal,$client,$enabled,$description) if($external -ne 22222 -or $internal -ne 22222 -or $protocol -ne 'TCP' -or $client -ne '192.168.1.20'){throw 'Incorrect mapping'};$global:ghpcNetworkTest_added++;[pscustomobject]@{Enabled=$true}}
$result=(& $script -GameBin $fixture) -join "`n"
Check ($result -match 'mapped to 192.168.1.20:22222' -and $global:ghpcNetworkTest_added -eq 1) 'automatic mapping uses the selected PC and exact TCP port'
