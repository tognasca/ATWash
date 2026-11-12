param(
 [Parameter(Mandatory=$true)][string]$BackupFile,
 [switch]$ConfirmRestore,
 [string]$ComposeFile = "docker-compose.yml"
)
$ErrorActionPreference="Stop"
if (-not $ConfirmRestore) { throw "Restauração destrutiva: informe -ConfirmRestore após verificar o backup e interromper a API." }
if (-not (Test-Path $BackupFile)) { throw "Arquivo de backup não encontrado." }
$container=docker compose -f $ComposeFile ps -q db
if (-not $container) { throw "Banco PostgreSQL não está em execução." }
$database=(docker exec $container printenv POSTGRES_DB).Trim()
$username=(docker exec $container printenv POSTGRES_USER).Trim()
$remote="/tmp/atwash-restore.dump"
docker cp (Resolve-Path $BackupFile).Path "${container}:${remote}"
if ($LASTEXITCODE -ne 0) { throw "Falha ao enviar backup." }
try {
 docker exec $container pg_restore -U $username -d $database --clean --if-exists --no-owner --no-acl --single-transaction $remote
 if ($LASTEXITCODE -ne 0) { throw "Falha na restauração. Verifique logs e integridade do banco." }
 Write-Host "Restauração concluída. Reinicie a API e execute os testes."
} finally { docker exec $container rm -f $remote | Out-Null }
