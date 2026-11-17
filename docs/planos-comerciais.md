# ATWashControl — planos e cadastro público

## Preços mensais (sem teste gratuito)
| Plano | Preço | Usuários ativos | Dispositivos ativos |
| --- | ---: | ---: | ---: |
| Essencial | R$ 59,90 | 2 | 1 |
| Profissional | R$ 99,90 | 5 | 3 |
| Empresarial | R$ 179,90 | 15 | 10 |

Os preços são definidos em reais e cadastrados na tabela `SubscriptionPlans`. O seed cria os planos quando não existem, **sem sobrescrever planos existentes**. Confira preços e limites de registros preexistentes no painel da plataforma.

## Fluxo
1. Acesse `/cadastro`, selecione um plano e preencha empresa, e-mail e senha forte.
2. A API verifica o plano, cria a assinatura mensal no Mercado Pago e, somente após resposta válida, registra empresa, proprietário e assinatura `pending` em uma transação.
3. O navegador redireciona ao checkout oficial. A senha não é enviada ao Mercado Pago.
4. A API confirma o estado da assinatura consultando o provedor via webhook assinado ou reconciliação.
5. Somente o estado `active` autoriza login e acesso ao sistema. Não existe trial para novos cadastros.

## Limitações e validações antes da primeira venda
- A criação de usuário no endpoint de acesso dos funcionários respeita o limite por plano; revise também os caminhos administrativos da plataforma para evitar concessões excepcionais não intencionais.
- O limite de dispositivos é aplicado durante o login e novas autorizações.
- A troca de plano automática, retomada de checkout interrompido, recuperação de senha, e-mail transacional, termos/LGPD e cancelamento pelo cliente ainda requerem implementação/homologação.
- Um cadastro cuja assinatura permaneça pendente não pode fazer login; o suporte deve poder verificar a assinatura e a reconciliação.
- A configuração de produção exige `MERCADOPAGO_ACCESS_TOKEN`, `MERCADOPAGO_WEBHOOK_SECRET`, `MERCADOPAGO_BACK_URL` e DNS/HTTPS válidos.
- Teste checkout, webhook assinado, repetição de notificações, renovação, suspensão, cancelamento e acesso multiempresa antes de cobrar clientes.
