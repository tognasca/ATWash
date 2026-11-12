# ATWashControl — implantação e homologação

## Situação

Os módulos de dispositivos, sincronização com conflito explícito, dashboard e leitura de assinatura foram implementados. **Não representam homologação final de produção**. A plataforma de cobrança SaaS, o provisionamento de empresas e a distribuição Android assinada ainda dependem de implantação e decisões comerciais.

## Segurança

- Configure `JWT_SECRET_KEY` forte e único, senha PostgreSQL distinta e `ATWASH_CORS_ORIGINS` apenas com domínios reais.
- Use HTTPS obrigatório no proxy reverso; não exponha PostgreSQL diretamente na Internet. A porta 5436 do Compose é publicada no host para desenvolvimento e deve ser restringida em produção.
- O dispositivo é registrado no login, vinculado ao usuário e empresa. Um gerente pode revogá-lo; JWTs de dispositivos revogados deixam de autenticar.
- Login offline após reiniciar o app ainda não está disponível; tokens duram 20 minutos. Nunca desative a validação de expiração para contornar isso.
- Revogação depende de conectividade: dados já armazenados no tablet permanecem localmente até serem limpos; não é apagamento remoto.

## Backup e restauração

1. Faça backup usando `powershell -File scripts/backup-postgres.ps1` na raiz do projeto.
2. Copie os arquivos para armazenamento criptografado fora do servidor e aplique retenção.
3. Teste restauração em banco isolado antes de usar em produção.
4. Para restaurar, interrompa a API, preserve backup adicional e execute `powershell -File scripts/restore-postgres.ps1 -BackupFile .\\backups\\arquivo.dump -ConfirmRestore`.
5. Valide migrações, quantidade de registros, usuários, pedidos e estoque após restaurar.

## Homologação obrigatória

- Execute a pipeline GitHub Actions e verifique API, PostgreSQL, frontend e APK.
- Em dois tablets, tente avançar a mesma comanda offline e sincronizar em ordem diferente; a segunda operação deve apresentar conflito ou reconhecer a etapa já aplicada.
- Teste criação offline de cliente, veículo e comanda, incluindo perda de rede após resposta HTTP e reenvio.
- Revogue um dispositivo e verifique 401 em chamadas autenticadas. Teste também suspensão de usuário e empresa.
- Valide isolamento entre empresas, controle de acesso, estorno, baixa de insumos, recuperação de backup e tempo de resposta.
- Android: configurar URL HTTPS de produção, assinatura keystore protegida, política de versionamento e teste de atualização mantendo os dados IndexedDB.

## SaaS

Dashboard operacional e consulta de planos/assinaturas estão disponíveis. Cadastro global de empresas, cobrança recorrente, webhook do provedor, faturas, suspensão por inadimplência e provisionamento self-service **não foram implementados**.


## Administração de plataforma (API, sem interface web global)

Defina `ATWASH_PLATFORM_ADMIN_KEY` com valor aleatório de no mínimo 32 bytes e armazene em gerenciador de segredos, separado do JWT e das credenciais de empresas. A chave é exigida no cabeçalho `X-ATWash-Platform-Key` e nunca deve ser incluída no JavaScript público, no aplicativo Android ou em repositórios. Restrinja `/api/platform` no proxy por IP/VPN e HTTPS. O backend exige HTTPS para hosts externos, mas o proxy também deve rejeitar HTTP público.

- `GET /api/platform/tenants` lista empresas e assinaturas.
- `GET/POST /api/platform/plans` e `PUT /api/platform/plans/{id}` gerenciam planos.
- `POST /api/platform/tenants` provisiona empresa, conta Owner e assinatura em uma transação.
- `PUT /api/platform/tenants/{id}/status?active=false` suspende a empresa.
- `PUT /api/platform/tenants/{id}/subscription` altera plano e status manualmente.

Assinaturas suspensas ou expiradas bloqueiam login e tokens já emitidos. O limite de dispositivos é verificado na criação de dispositivo; para garantia rígida sob cadastros concorrentes, ainda é necessário serializar essa operação. Não existe gateway de pagamento, emissão de fatura ou renovação automática. A administração global é por API protegida, não por painel web público.

## Plataformas e limites de homologação

- Web: navegador desktop e mobile com layout responsivo.
- Android: Capacitor compila APK debug; release assinada, distribuição e atualização precisam de keystore e domínio HTTPS definitivo.
- iOS: PWA no Safari; não existe build nativa para App Store.
- Offline: cliente, veículo, comanda e avanço de etapa são enfileirados; pagamentos e ajustes de estoque exigem internet. A sessão JWT expira em 20 minutos e não há autenticação offline segura após reinício. Não declare funcionamento integral offline até resolver isso.
- Segurança: revogação de dispositivo não apaga automaticamente dados armazenados localmente. Teste o cenário de dispositivo perdido e políticas de retenção.
- Produção: scripts de backup/restauração precisam de teste de recuperação em instalação real e agendamento externo; não equivalem a backups automáticos já ativos.
