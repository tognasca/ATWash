ATWashControl — especificação e acompanhamento de implementação

> Nome comercial e identidade visual atualizados: **ATWashControl** (roxo, grafite e branco). O nome técnico do repositório, namespaces e configurações existentes permanece **ATWash** para evitar quebra de compatibilidade.
>
> **Atenção:** esta especificação descreve o produto-alvo. Nem todos os itens abaixo estão implementados ou homologados. Veja o acompanhamento ao final do documento.

PROMPT MASTER — ATWash

Seu lava-rápido na palma da mão

OBJETIVO

Atue como arquiteto de software sênior e desenvolvedor full-stack, especializado em ASP.NET Core, React, PostgreSQL, Android, sistemas SaaS multiempresa, PDV, pagamentos e aplicações offline-first.

Desenvolva o ATWash, uma plataforma completa de gestão de lava-rápidos e centros de estética automotiva.

O sistema será comercializado em dois formatos:

1. ATWash Software: assinatura do sistema para estabelecimentos que já possuem computador ou tablet compatível.
2. ATWash Kit: tablet Android configurado com o aplicativo ATWash, acompanhado de assinatura do sistema.

O ATWash deverá funcionar pela web e em tablets Android, tanto online quanto offline, com sincronização automática quando houver internet.

Não desenvolver apenas protótipos, telas estáticas ou funcionalidades simuladas. O objetivo é construir um produto comercial funcional, seguro, escalável e preparado para produção.

⸻

1. IDENTIDADE DO PRODUTO

Nome comercial atual: ATWashControl (identificadores técnicos existentes: ATWash)

Slogan: Seu lava-rápido na palma da mão.

Segmento: Gestão de lava-rápidos e estética automotiva.

Identidade visual:

* Roxo profundo (#64107b) como cor principal.
* Branco, grafite (#252a32) e tons de cinza.
* Verde para operações concluídas.
* Laranja para serviços em andamento.
* Vermelho para alertas.
* Interface premium, elegante e intuitiva.
* Ícones Lucide.
* Design responsivo e touch-first.

Criar uma identidade visual exclusiva para o ATWash, incluindo logotipo, favicon, ícone Android, splash screen e elementos gráficos consistentes.

Não utilizar o nome LavaGest em nenhum arquivo, tela, configuração ou documentação.

⸻

2. TECNOLOGIAS

Backend

* ASP.NET Core .NET 10 LTS
* C#
* Entity Framework Core
* PostgreSQL
* REST API com OpenAPI
* JWT e refresh tokens
* FluentValidation
* Serilog
* SignalR
* Background Services

Frontend

* React
* TypeScript
* Vite
* Tailwind CSS
* shadcn/ui
* TanStack Query
* React Router
* PWA
* IndexedDB com Dexie.js

Android

* Capacitor
* Compatibilidade com tablets Android
* Armazenamento local persistente
* Sincronização offline-first
* Suporte a câmera
* Suporte opcional a impressora térmica

Infraestrutura

* Docker
* Docker Compose
* PostgreSQL
* Redis, quando necessário
* HTTPS
* Backup automático
* Deploy local ou em nuvem
* Compatibilidade com Cloudflare Tunnel

Organizar o código em monorepositório, com separação entre API, frontend, Android, infraestrutura e testes.

⸻

3. ARQUITETURA SAAS MULTIEMPRESA

O ATWash deverá permitir que diversos lava-rápidos utilizem a mesma plataforma.

Cada estabelecimento terá:

* Cadastro empresarial.
* Unidades.
* Funcionários.
* Clientes.
* Veículos.
* Serviços.
* Produtos.
* Estoque.
* Comandas.
* Pagamentos.
* Caixas.
* Relatórios.
* Configurações Pix.
* Assinatura.
* Dispositivos autorizados.

Todos os registros devem ser isolados por TenantId.

Nenhuma empresa poderá acessar informações de outra.

Implementar controle de permissões e validações de isolamento no backend.

⸻

4. PERFIS DE ACESSO

Superadministrador ATWash

* Gerenciar empresas.
* Gerenciar assinaturas.
* Criar planos.
* Ativar e suspender clientes.
* Gerenciar tablets.
* Controlar licenças.
* Consultar métricas comerciais.
* Gerenciar suporte.

Proprietário

* Acessar dashboard completo.
* Gerenciar funcionários.
* Configurar serviços e produtos.
* Consultar faturamento.
* Controlar estoque.
* Gerenciar caixa.
* Autorizar descontos e cancelamentos.
* Consultar relatórios.

Gerente

* Supervisionar operação.
* Gerenciar comandas.
* Acompanhar funcionários.
* Operar caixa conforme permissões.

Atendente

* Cadastrar clientes e veículos.
* Abrir comandas.
* Adicionar serviços e produtos.
* Realizar fechamento autorizado.

Lavador

* Consultar serviços atribuídos.
* Iniciar lavagem.
* Atualizar andamento.
* Finalizar serviços.
* Registrar consumo de insumos.

⸻

5. FLUXO DE ATENDIMENTO

O processo deverá ser simples e rápido.

Entrada do veículo

O funcionário pega o tablet Android e informa:

* Placa.
* Cliente.
* Telefone.
* Marca.
* Modelo.
* Cor.
* Tipo de veículo.
* Serviço solicitado.
* Funcionário responsável.
* Observações.

Se a placa já estiver cadastrada, preencher automaticamente os dados conhecidos.

Permitir cadastro rápido para clientes novos.

Abertura da comanda

Gerar automaticamente uma comanda contendo:

* Número.
* Cliente.
* Veículo.
* Placa.
* Serviços.
* Produtos.
* Valores.
* Funcionário.
* Data e horário.
* Status operacional.
* Status financeiro.

Execução

Utilizar os status:

1. Aguardando.
2. Em lavagem.
3. Em acabamento.
4. Pronto para entrega.
5. Entregue.
6. Cancelado.

Exibir as comandas em um painel Kanban.

Consumo

Permitir adicionar produtos e serviços extras durante o atendimento.

Atualizar o valor da comanda automaticamente.

Pagamento

Exibir o valor total e permitir:

* Pix.
* Cartão de crédito.
* Cartão de débito.
* Dinheiro.
* Pagamento misto.
* Outros meios configurados.

Finalização

Após confirmar o pagamento:

* Registrar a transação.
* Atualizar o caixa.
* Dar baixa financeira na comanda.
* Registrar horário e operador.
* Emitir comprovante.
* Permitir registrar a saída do veículo.
* Manter histórico para o proprietário.

O status operacional e o financeiro devem ser independentes.

⸻

6. SERVIÇOS

Permitir cadastrar serviços com:

* Nome.
* Descrição.
* Preço.
* Duração estimada.
* Categoria.
* Tipo de veículo.
* Custo estimado.
* Comissão.
* Insumos consumidos.

Permitir preços diferentes para carros pequenos, sedãs, SUVs, caminhonetes, motocicletas e outros veículos.

⸻

7. PRODUTOS E ESTOQUE

Separar:

Produtos de revenda

* Bebidas.
* Snacks.
* Aromatizantes.
* Acessórios automotivos.

Insumos internos

* Shampoo automotivo.
* Limpa-vidros.
* Pretinho.
* Ceras.
* Desengraxantes.
* Outros materiais.

Controlar:

* Entrada.
* Saída.
* Venda.
* Consumo.
* Reposição.
* Estoque mínimo.
* Perdas.
* Custo médio.
* Fornecedores.

Criar ficha técnica para calcular o consumo estimado de insumos por lavagem.

⸻

8. PAGAMENTOS E PIX

Criar checkout visual e otimizado para tablet.

Pix

O administrador poderá configurar sua chave Pix.

Implementar QR Code Pix e Pix Copia e Cola conforme o padrão aplicável.

Para pagamentos Pix dinâmicos e confirmação automática, utilizar integração com instituição financeira ou provedor compatível.

Não considerar pagamento confirmado apenas porque o QR Code foi gerado.

Quando não houver integração bancária, exigir confirmação manual autorizada.

Cartão

O cliente realiza o pagamento na maquininha física do estabelecimento.

O funcionário registra a confirmação, podendo informar NSU e código de autorização.

Não integrar diretamente com maquininhas sem API ou SDK homologado.

Dinheiro

Registrar valor recebido e calcular troco.

Pagamento misto

Permitir combinar diferentes meios de pagamento na mesma comanda.

Implementar proteção contra pagamentos duplicados e trilha de auditoria.

⸻

9. CONTROLE DE CAIXA

Implementar:

* Abertura.
* Saldo inicial.
* Entradas.
* Saídas.
* Sangrias.
* Suprimentos.
* Pagamentos.
* Estornos.
* Fechamento.
* Conferência.
* Histórico por operador.

Separar vendas realizadas, pagamentos recebidos e valores pendentes.

⸻

10. FUNCIONÁRIOS

Cadastrar funcionários com:

* Nome.
* Cargo.
* Contato.
* Permissões.
* Status.
* Histórico de serviços.
* Produtividade.
* Comissões.

Criar relatórios de:

* Veículos atendidos.
* Tempo médio de lavagem.
* Serviços realizados.
* Faturamento atribuído.
* Comissões.

⸻

11. DASHBOARD DO PROPRIETÁRIO

Exibir:

* Faturamento do dia.
* Faturamento mensal.
* Quantidade de veículos atendidos.
* Veículos em andamento.
* Ticket médio.
* Serviços mais vendidos.
* Produtos vendidos.
* Pagamentos por modalidade.
* Despesas.
* Lucro estimado.
* Produtividade.
* Estoque baixo.
* Clientes recorrentes.

Adicionar gráficos, filtros por período e exportação para PDF, Excel e CSV.

Todos os indicadores devem utilizar dados reais do banco.

⸻

12. FUNCIONAMENTO OFFLINE-FIRST

O ATWash deverá continuar operando quando a internet cair.

Permitir offline:

* Consultar cadastros sincronizados.
* Cadastrar clientes.
* Cadastrar veículos.
* Abrir comandas.
* Adicionar serviços.
* Adicionar produtos.
* Atualizar lavagens.
* Registrar pagamentos presenciais confirmados.
* Finalizar atendimentos locais autorizados.

Utilizar armazenamento local persistente.

Criar fila de sincronização com identificadores únicos e operações idempotentes.

Quando a internet retornar:

1. Detectar conexão com a API.
2. Enviar operações pendentes.
3. Validar autorização.
4. Evitar duplicações.
5. Resolver conflitos.
6. Atualizar dados locais.
7. Informar resultado.

Não sobrescrever movimentações financeiras ou de estoque silenciosamente.

Exibir indicador:

* Online.
* Offline.
* Sincronizando.
* Erro de sincronização.

Pagamentos que dependam de confirmação bancária não poderão ser confirmados automaticamente sem conexão.

⸻

13. APLICATIVO ANDROID

O aplicativo ATWash deverá ser otimizado para tablets.

Implementar:

* Botões grandes.
* Interface touch-first.
* Modo horizontal e vertical.
* Login seguro.
* Persistência offline.
* Câmera.
* Cadastro rápido.
* Checkout.
* Sincronização.
* Identificação do dispositivo.
* Revogação de acesso.
* Impressão opcional.

Preparar distribuição privada ou pela Google Play, conforme estratégia comercial.

⸻

14. VERSÃO WEB

Criar versão web responsiva com as mesmas regras de negócio.

Permitir utilização em:

* Computadores Windows.
* Notebooks.
* Tablets.
* Navegadores modernos.

A versão web deverá compartilhar API, autenticação, permissões e banco de dados com o aplicativo Android.

⸻

15. ADMINISTRAÇÃO COMERCIAL DO ATWash

Criar painel para gerenciamento do SaaS.

Funcionalidades:

* Empresas clientes.
* Planos.
* Assinaturas.
* Licenças.
* Dispositivos.
* Cobranças.
* Períodos de teste.
* Suspensões.
* Suporte.
* Histórico comercial.

Separar comercialmente:

ATWash Software

* Assinatura mensal.
* Utilização em equipamentos próprios.

ATWash Kit

* Tablet Android.
* Aplicativo instalado.
* Configuração inicial.
* Treinamento opcional.
* Assinatura mensal.

Permitir configuração dos preços pelo superadministrador.

⸻

16. CARGA INICIAL AUTOMÁTICA — SEED DATA

Implementar Seed Data real no PostgreSQL para o ambiente de demonstração.

Não utilizar mocks como substitutos da persistência.

Estabelecimento

* Nome: ATWash Demo
* Tipo: Lava-rápido e estética automotiva
* Status: Ativo
* Moeda: BRL
* Fuso: America/Sao_Paulo

Serviços iniciais

Serviço	Preço
Lavagem simples	R$ 45,00
Lavagem completa	R$ 65,00
Lavagem premium	R$ 90,00
Lavagem de motor	R$ 50,00
Aspiração interna	R$ 25,00
Enceramento	R$ 80,00
Higienização interna	R$ 150,00
Polimento	R$ 250,00

Produtos iniciais

Produto	Preço	Estoque
Água mineral	R$ 4,00	50
Refrigerante	R$ 6,00	40
Café	R$ 3,00	30
Energético	R$ 12,00	20
Aromatizante automotivo	R$ 15,00	25
Pano de microfibra	R$ 12,00	30

Funcionários

* João Silva — Lavador.
* Carlos Oliveira — Lavador.
* Mariana Santos — Atendente.

Criar funcionários fictícios vinculados ao estabelecimento de demonstração.

Clientes, veículos e comandas

Comanda 001

* Placa: ABC-1D23
* Veículo: Honda Civic
* Serviço: Lavagem completa
* Valor: R$ 65,00
* Status: Em lavagem
* Pagamento: Pendente

Comanda 002

* Placa: FJK-4G56
* Veículo: Jeep Renegade
* Serviço: Lavagem premium
* Valor: R$ 90,00
* Status: Pronto para entrega
* Pagamento: Pendente

Comanda 003

* Placa: GHT-7H89
* Veículo: Fiat Argo
* Serviço: Lavagem simples
* Valor: R$ 45,00
* Status: Aguardando
* Pagamento: Pendente

Criar clientes fictícios vinculados aos veículos.

Os registros devem permitir testes completos de operação.

Regras do Seed Data

* Executar no primeiro provisionamento do ambiente de demonstração.
* Não duplicar registros.
* Utilizar identificadores estáveis.
* Não sobrescrever alterações posteriores.
* Não inserir dados fictícios em empresas reais.
* Permitir restauração administrativa explícita.
* Criar migrations.
* Implementar Seed Data no backend .NET.
* Utilizar dados reais da API no frontend.
* Criar testes de idempotência.

⸻

17. MODELAGEM DO BANCO

Criar entidades:

* Tenants
* Branches
* Users
* Roles
* Permissions
* Employees
* Customers
* Vehicles
* Services
* ServicePrices
* Products
* InventoryMovements
* Suppliers
* Orders
* OrderItems
* ServiceExecutions
* Payments
* PaymentMethods
* CashRegisters
* CashMovements
* Expenses
* EmployeeCommissions
* Devices
* DeviceSyncOperations
* Subscriptions
* Plans
* AuditLogs
* TenantSettings

Criar relacionamentos, índices, constraints, migrations e integridade referencial.

Utilizar decimal para valores monetários.

⸻

18. SEGURANÇA

Implementar:

* HTTPS.
* Autenticação segura.
* Controle de permissões.
* Isolamento multi-tenant.
* Logs de auditoria.
* Rate limiting.
* Validação de entrada.
* Proteção contra pagamentos duplicados.
* Segredos fora do código.
* Backups.
* Recuperação de dados.
* Adequação à LGPD.
* Criptografia apropriada para dados sensíveis.

Não confiar em valores financeiros enviados pelo frontend sem validação.

⸻

19. TESTES OBRIGATÓRIOS

Criar testes para:

* Clientes.
* Veículos.
* Serviços.
* Comandas.
* Produtos.
* Estoque.
* Pagamentos.
* Pix.
* Caixa.
* Permissões.
* Multi-tenant.
* Offline.
* Sincronização.
* Conflitos.
* Idempotência.
* Revogação de dispositivos.
* Seed Data.

Testar dois tablets trabalhando offline simultaneamente.

Testar perda de conexão durante uma operação.

Testar reconexão e sincronização após várias horas.

Testar duplicidade de pagamentos e movimentações.

⸻

20. ETAPAS DE DESENVOLVIMENTO

Fase 1 — Fundação

* Estrutura do projeto.
* Backend.
* Frontend.
* PostgreSQL.
* Autenticação.
* Multi-tenant.
* Docker.
* Seed Data.

Fase 2 — Operação

* Clientes.
* Veículos.
* Funcionários.
* Serviços.
* Comandas.
* Fila de lavagem.

Fase 3 — Financeiro

* Produtos.
* Estoque.
* Pagamentos.
* Pix.
* Caixa.

Fase 4 — Offline e Android

* Armazenamento local.
* Sincronização.
* Aplicativo Android.
* Identificação de dispositivos.
* Testes offline.

Fase 5 — Gestão SaaS

* Dashboard.
* Relatórios.
* Assinaturas.
* Licenças.
* Administração comercial.

Fase 6 — Produção

* Segurança.
* Testes.
* Backups.
* Monitoramento.
* Documentação.
* Deploy.
* Homologação Android.

⸻

21. REGRAS FINAIS

1. A marca comercial atual é ATWashControl; preservar os identificadores técnicos ATWash existentes até migração controlada.
2. Utilizar o slogan “Seu lava-rápido na palma da mão”.
3. Não criar telas estáticas como produto final.
4. Todas as operações devem funcionar com persistência real.
5. Implementar Seed Data completo para demonstração.
6. Não inserir dados de demonstração em clientes reais.
7. Não ignorar o funcionamento offline.
8. Não considerar Pix confirmado sem evidência.
9. Não permitir acesso entre empresas.
10. Não criar botões sem funcionalidade.
11. Não inventar integrações bancárias.
12. Implementar testes automatizados.
13. Documentar instalação e execução.
14. Preparar o sistema para comercialização.
15. Validar os módulos antes de declarar o produto pronto.

INSTRUÇÃO DE EXECUÇÃO

Primeiro, apresente a arquitetura técnica, a estrutura de pastas, o modelo de dados e os fluxos principais.

Depois, implemente o ATWash por fases, com código real, migrations, APIs, interfaces e testes.

A primeira execução do ambiente de demonstração deverá carregar automaticamente os serviços, produtos, funcionários, clientes, veículos e comandas definidos neste documento.

O resultado esperado é um SaaS profissional, multiempresa, online/offline, preparado para uso em tablets Android e computadores, com operação completa de lava-rápido, gestão financeira e modelo comercial de assinaturas.

---

## Estado real da implementação (12/11/2026)

Este quadro diferencia **requisito planejado** de **funcionalidade entregue**. Build aprovado não substitui testes em tablet físico, testes de carga, auditoria de segurança ou homologação comercial.

| Área | Estado | Evidência/limitação |
|---|---|---|
| API .NET 10, PostgreSQL, Docker | Implementado | Workflow `.github/workflows/build.yml` compila a API e aplica migrations em PostgreSQL de teste. |
| Autenticação e isolamento básico por TenantId | Implementado parcialmente | Há filtros e validações por tenant nas rotas existentes; testes negativos completos entre duas empresas ainda necessários. |
| Serviços, produtos, clientes, veículos, comandas | Implementado parcialmente | CRUD operacional e seed demonstrativo; ainda faltam ficha técnica, movimentações detalhadas de estoque, fornecedores e diversos campos do README. |
| Pagamentos, caixa, Pix, estornos, conciliação | Implementado parcialmente | QR Code Pix estático e confirmação manual; não há confirmação bancária automática nem estorno bancário automático. |
| Offline | Implementado parcialmente | Cache IndexedDB por tenant, fila de cadastros/comandas/etapas e replay com UUIDs; não há login offline após reinício, pagamentos offline ou extras offline. |
| Android | APK debug compilado | Capacitor e workflow Android; requer URL HTTPS real da API, teste em dispositivo físico e build release assinado. |
| Gestão comercial SaaS | Pendente | Planos, assinaturas, licenças, superadmin e gestão de dispositivos não estão completos. |
| Dashboard, relatórios e exportações | Pendente/parcial | Indicadores avançados e exportação PDF/Excel/CSV previstos nesta especificação ainda não foram homologados. |
| Segurança e produção | Pendente | Revisão de permissões, auditoria, LGPD, backup/restauração, observabilidade, testes de carga e homologação. |

### Próximas entregas obrigatórias

1. Gestão de dispositivos e revogação, autenticação offline segura e resolução explícita de conflitos.
2. Movimentação de estoque, venda de produtos e consumo de insumos com integridade transacional.
3. Relatórios reais e painel comercial multiempresa (planos, assinaturas, licenças).
4. Testes negativos multi-tenant, simultaneidade entre dois tablets, backup/restauração e homologação Android físico.

Consulte `docs/tablet-offline-android.md` para os limites operacionais e instruções Android. Nunca tratar esta especificação como prova de entrega funcional.
