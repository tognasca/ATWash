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
$remote = "/tmp/atwash-$stamp.dump"
docker exec $container pg_dump -U $username -d $database -Fc --no-owner --no-acl -f $remote
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar backup." }
docker cp "${container}:${remote}" $path
if ($LASTEXITCODE -ne 0) { throw "Falha ao copiar backup." }
docker exec $container rm -f $remote | Out-Null
if ((Get-Item $path).Length -eq 0) { throw "Backup vazio." }
Write-Host "Backup gerado: $path"
