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
