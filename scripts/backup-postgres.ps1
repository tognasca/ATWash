param(
 [string]$BackupDirectory = ".\\backups",
 [string]$ComposeFile = "docker-compose.yml"
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$path = Join-Path (Resolve-Path $BackupDirectory).Path "atwash-$stamp.dump"
$container = docker compose -f $ComposeFile ps -q db
if (-not $container) { throw "Banco PostgreSQL não está em execução." }
$database = docker exec $container printenv POSTGRES_DB
$username = docker exec $container printenv POSTGRES_USER
$database = $database.Trim(); $username = $username.Trim()
docker exec $container pg_dump -U $username -d $database -Fc --no-owner --no-acl | Set-Content -Path $path -AsByteStream
if ($LASTEXITCODE -ne 0) { Remove-Item $path -ErrorAction SilentlyContinue; throw "Falha ao gerar backup." }
Write-Host "Backup gerado: $path"
