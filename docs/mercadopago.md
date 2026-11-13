# Mercado Pago — assinaturas recorrentes do ATWashControl

A integração persiste cada assinatura criada no PostgreSQL, recebe notificações com validação criptográfica, consulta a API oficial antes de atualizar o estado da empresa e executa reconciliação automática a cada 15 minutos.

## Variáveis (configuração final, não necessárias no CI)

- `MERCADOPAGO_ACCESS_TOKEN`: token privado da aplicação.
- `MERCADOPAGO_BACK_URL`: retorno HTTPS para o frontend.
- `MERCADOPAGO_WEBHOOK_SECRET`: assinatura secreta configurada no painel do Mercado Pago.
- Endpoint de notificações: `POST /api/webhooks/mercadopago`.

Não colocar segredos no frontend, APK, logs ou GitHub.

## Endpoints

- `POST /api/saas/mercadopago/checkout`: proprietário autenticado, inicia assinatura mensal pendente e grava vínculo entre empresa e ID do provedor. Impede múltiplas tentativas pendentes nos primeiros 15 minutos.
- `GET /api/saas/mercadopago/subscriptions`: histórico de vínculos do tenant autenticado.
- `POST /api/saas/mercadopago/sync/{subscriptionId}`: reconciliação manual, restrita ao proprietário e a IDs associados à sua empresa.
- `POST /api/webhooks/mercadopago`: exige assinatura HMAC-SHA256 válida, timestamp recente e ID na URL. O payload da notificação nunca é aceito como comprovação de pagamento.

## Segurança e reconciliação

A reconciliação busca `GET /preapproval/{id}` na API do Mercado Pago, confere o ID e `external_reference` contra a empresa gravada e só então altera o status local. Apenas a assinatura mais recente da empresa controla seu acesso. A rotina em segundo plano consulta assinaturas desatualizadas em lotes de até 100 a cada 15 minutos.

Estados: `authorized -> active`, `paused -> suspended`, `cancelled -> cancelled`, `pending -> pending`, demais -> `past_due`. O acesso da empresa depende do status local de assinatura, com a validação já existente no JWT.

## Homologação antes de cobranças reais

A migração cria `MercadoPagoSubscriptions`. O CI verifica migrações, API, frontend e Android sem credenciais reais. Antes de ativar cobranças, configurar as três variáveis, registrar a URL pública de webhook e executar testes de ponta a ponta com assinatura de teste, renovação, atraso, cancelamento, notificação duplicada e recuperação após suspensão.

Documentação oficial: https://www.mercadopago.com.br/developers/pt/docs/subscriptions/overview
