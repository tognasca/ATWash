# Mercado Pago — cobrança recorrente do ATWashControl

Integração inicial de assinatura mensal via API oficial `POST /preapproval` com status `pending`. O backend usa o token privado; nunca coloque o token no frontend, no Android, em logs ou no GitHub.

## Configuração

- `MERCADOPAGO_ACCESS_TOKEN`: token de acesso privado de produção/teste da aplicação Mercado Pago.
- `MERCADOPAGO_BACK_URL`: URL pública HTTPS de retorno ao site ATWashControl.
- `POST /api/saas/mercadopago/checkout`: apenas Owner autenticado; retorna `checkoutUrl` para concluir autorização no Mercado Pago.
- `POST /api/saas/mercadopago/sync/{subscriptionId}`: consulta o status na API oficial e confere `external_reference` contra a empresa autenticada antes de alterar o status local.

**Limitações:** a integração ainda não recebe webhooks verificados, não tem reconciliação automática agendada, não armazena o ID da assinatura em tabela própria e não implementa renovação de acesso de empresa suspensa. O endpoint de sincronização requer JWT válido; portanto não recupera por si só uma conta já bloqueada. Não habilite cobrança real para clientes até implementar e homologar essas partes. Uma URL de retorno não comprova pagamento; somente a API do provedor pode confirmar o status.

Documentação oficial: https://www.mercadopago.com.br/developers/pt/docs/subscriptions/overview
