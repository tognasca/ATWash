# ATWashControl — publicação comercial (checklist de homologação)

## 1. Preparar ambiente
- Faça backup verificado do PostgreSQL e confirme restauração antes de publicar.
- Não use credenciais de demonstração. Mantenha `ATWASH_SEED_DEMO=false` e `ATWASH_BOOTSTRAP_ADMIN=false` após provisionar a conta inicial.
- Gere segredos exclusivos e fortes para `POSTGRES_PASSWORD` e `JWT_SECRET_KEY`. **Não troque o JWT_SECRET_KEY de uma instalação em uso sem planejar a invalidação de sessões.**
- Configure `.env` no servidor, fora do Git, usando `.env.example` como referência.
- Revise permissões, assinaturas, bloqueios de dispositivo e regras de acesso entre empresas.

## 2. Cloudflare Tunnel (Windows)
O túnel existente pode adicionar um hostname sem alterar os ingress já configurados. Exemplo de regra para inserir **antes do catch-all 404** em `C:\Windows\System32\config\systemprofile\.cloudflared\config.yml`:

```yaml
  - hostname: atwash.ademartognasca.com.br
    service: http://localhost:8087
```

Ajuste o hostname para o domínio realmente escolhido. Configure a rota DNS pública correspondente no painel do Cloudflare Tunnel. Não substitua o arquivo inteiro nem modifique os outros serviços. Valide a sintaxe do túnel antes de reiniciar o serviço Windows. O frontend Nginx encaminha `/api/` para o contêiner da API; não exponha PostgreSQL nem a porta 5098 diretamente à internet.

## 3. Mercado Pago — assinatura do SaaS
Obtenha no painel oficial do Mercado Pago:
- **Access Token de produção** (privado).
- **Segredo da assinatura de notificações** (webhook).
- **URL de retorno** HTTPS da aplicação.

Defina no `.env` do servidor:
```dotenv
MERCADOPAGO_ACCESS_TOKEN=COLE_AQUI_APENAS_NO_SERVIDOR
MERCADOPAGO_WEBHOOK_SECRET=COLE_AQUI_APENAS_NO_SERVIDOR
MERCADOPAGO_BACK_URL=https://atwash.ademartognasca.com.br/
ATWASH_CORS_ORIGINS=https://atwash.ademartognasca.com.br
```

Configure no Mercado Pago o webhook:
`https://atwash.ademartognasca.com.br/api/webhooks/mercadopago`

Não envie segredos por chat nem os salve no repositório. Não confunda credenciais de teste e produção. A assinatura do cliente só deve ser ativada após confirmação do provedor e reconciliação.

## 4. Atualização e validação
```powershell
cd C:\git\ATWash
git pull origin main
docker compose config
docker compose up -d --build
docker compose ps
docker compose logs api --tail=100
Invoke-WebRequest http://localhost:5098/health
```

Antes de cobrar clientes reais, validar: login e expiração de token; acesso isolado por empresa; checkout e webhook reais; renovação, atraso e cancelamento; Pix e estornos; backup e restauração; operação offline e sincronização; instalação Android e funcionamento com conexão intermitente; HTTPS, domínio e dispositivos revogados. Uma compilação bem-sucedida não substitui essa homologação.

## 5. Situação atual
O código possui integração de assinatura com Mercado Pago, mas **a ativação comercial depende das credenciais reais e dos testes de ponta a ponta**. O arquivo Cloudflare Tunnel fica no computador do operador e não pode ser atualizado por um commit GitHub. Não afirmar que a publicação está concluída antes de verificar DNS, TLS, rotas, webhooks e backups.
