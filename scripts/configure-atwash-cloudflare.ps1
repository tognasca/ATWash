# Adds the ATWashControl hostname to an existing locally managed cloudflared YAML.
# Run in elevated PowerShell on the Windows machine hosting the existing tunnel.
[CmdletBinding(SupportsShouldProcess=$true)]
param(
 [string]$Cloudflared = 'C:\Cloudflared\bin\cloudflared.exe',
 [string]$Config = 'C:\Windows\System32\config\systemprofile\.cloudflared\config.yml',
 [string]$Hostname = 'atwash.ademartognasca.com.br',
 [string]$Origin = 'http://localhost:8087'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Cloudflared -PathType Leaf)) { throw "cloudflared nao encontrado: $Cloudflared" }
if (-not (Test-Path -LiteralPath $Config -PathType Leaf)) { throw "config.yml nao encontrado: $Config" }
if ($Hostname -notmatch '^[a-z0-9.-]+$') { throw 'Hostname invalido.' }
if ($Origin -notmatch '^http://localhost:[0-9]{2,5}$') { throw 'Origin deve ser http://localhost:PORTA.' }
$raw = Get-Content -LiteralPath $Config -Raw
if ($raw -match "(?m)^\s*-\s*hostname:\s*['\x22]?$([regex]::Escape($Hostname))['\x22]?\s*$") {
 Write-Host "Hostname ja configurado. Nenhuma alteracao feita."
 exit 0
}
# The last ingress entry must be a catch-all, otherwise a new rule might be unreachable.
$lines = [regex]::Split($raw, "\r?\n")
$catchall = -1
for ($i=0; $i -lt $lines.Count; $i++) {
 if ($lines[$i] -match '^([ \t]*)-\s*service:\s*http_status:404\s*(?:#.*)?$') { $catchall=$i; break }
}
if ($catchall -lt 0) { throw 'Regra final - service: http_status:404 nao encontrada. Configuracao preservada. Inspecione o YAML antes de editar.' }
$indent = [regex]::Match($lines[$catchall],'^([ \t]*)').Groups[1].Value
if ($indent.Contains("`t")) { throw 'Indentacao com tab nao suportada. Configuracao preservada.' }
$entry = @("${indent}- hostname: $Hostname","${indent}  service: $Origin")
$newLines = @()
if ($catchall -gt 0) { $newLines += $lines[0..($catchall-1)] }
$newLines += $entry
$newLines += $lines[$catchall..($lines.Count-1)]
$newText = $newLines -join "`n"
$backup = "$Config.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"
if ($PSCmdlet.ShouldProcess($Config,"Backup e inclusao de $Hostname")) {
 Copy-Item -LiteralPath $Config -Destination $backup -ErrorAction Stop
 $encoding = New-Object System.Text.UTF8Encoding($false)
 [System.IO.File]::WriteAllText($Config,$newText,$encoding)
 & $Cloudflared --config $Config tunnel ingress validate
 if ($LASTEXITCODE -ne 0) {
  Copy-Item -LiteralPath $backup -Destination $Config -Force
  throw "YAML invalido. Backup restaurado: $backup"
 }
 Write-Host "Configuracao validada. Backup: $backup"
 Write-Host "Reinicie apenas o servico cloudflared existente apos conferir DNS e origem."
 Write-Host "Configure o hostname publico no painel Cloudflare Zero Trust/Tunnel (ou rota DNS equivalente)."
}
