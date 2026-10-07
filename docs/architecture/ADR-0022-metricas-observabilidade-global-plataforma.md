# ADR-0022: Métricas Executivas, Observabilidade Global e Investigação Operacional Desacoplada

## Status
Aceito

## Contexto
A conclusão do Marco M6 (Governança e Backoffice da Plataforma) requer a consolidação de métricas financeiras recorrentes (MRR, ARR, Churn Rate e LTV), telemetria operacional de pátio (veículos atendidos), pagamentos Pix transacionados e central de investigação técnica (logs de webhooks e falhas de envio de WhatsApp). 

O desafio arquitetural consiste em agregar dados originários de quatro módulos autônomos distintos (`Billing`, `YardOperations`, `WhatsApp` e `Identity`) sem criar dependências circulares, sem acoplar DbContexts e sem violar as regras de isolamento multi-tenant estipuladas em `agents.md`.

## Decisão de Arquitetura

### 1. Comunicação Intermodular Desacoplada via Portas Públicas (Shared Contracts)
A orquestração de métricas no host da API (`CarWashSaaS.Api`) consome exclusivamente interfaces públicas definidas em `CarWashSaaS.Shared.Contracts`:
- `IPlatformBillingMetricsLookup`: provê cálculo de receita recorrente (MRR, ARR, Churn, LTV), consolidação de Pix e listagem de webhooks recebidos (`billing.ProcessedPaymentWebhooks` e `billing.ProcessedSaasWebhookEvents`).
- `IPlatformYardMetricsLookup`: provê agregação de veículos atendidos e ordens em andamento no pátio através de consultas no módulo `YardOperations`.
- `IPlatformWhatsAppObservabilityLookup`: provê telemetria de conectividade e auditoria de mensagens com falha e motivo de erro no módulo `WhatsApp`.

### 2. Metodologia de Cálculo das Métricas SaaS
- **MRR (Monthly Recurring Revenue):** Soma dos valores mensais contratados de todas as assinaturas ativas (`TenantSubscriptionStatus.Active`) e em tolerância (`GracePeriod`).
- **ARR (Annual Recurring Revenue):** MRR anualizado ($\text{MRR} \times 12$).
- **Churn Rate (%):** Razão percentual de cancelamentos ocorridos na janela selecionada dividida pela base ativa no período.
- **LTV (Lifetime Value):** Cálculo dinâmico ($\text{ARPU} / \text{Churn Rate}$) com fallback seguro e transparente para a receita média histórica realizada por estabelecimento quando a taxa de evasão for 0% (evitando divisão por zero).

### 3. Consultas Globais no EF Core e Proteção Multi-Tenant
- Nas portas de observabilidade da plataforma, as consultas utilizam `.IgnoreQueryFilters()` para agregação transversal em todos os estabelecimentos, enquanto o pipeline HTTP restringe os endpoints `/platform/metrics/*` e `/platform/observability/*` compulsoriamente à política `PlatformAuthorizationPolicyNames.PlatformUser` (exclusivo para operadores de plataforma com claim `user_realm = "Platform"`).
- Usuários comuns de estabelecimentos (lojistas) são sumariamente bloqueados com HTTP 403 Forbidden.
- A persistência de dados de tenant continua estritamente governada por `TenantIsolationExtensions`, impedindo qualquer contaminação cruzada.

### 4. Proteção de Dados Sensíveis na Observabilidade
- Números de telefone de clientes finais em mensagens rejeitadas são automaticamente mascarados na porta de observabilidade (`+55 (11) ****-1234`), preservando a privacidade (LGPD).
- Identificadores de transação Pix (TxId) e hashes de payload permanecem auditáveis para fins forenses.

### 5. Frontend Blazor WebAssembly
- Criada a nova página `/platform/metrics` (`PlatformObservabilityPage.razor`), vinculada ao item 18 do menu lateral `NavMenu.razor`.
- A interface conta com 4 cards executivos em destaque, seletor de janelas temporais (7d, 30d, 90d, 12m), tabelas de série temporal e abas dedicadas para logs de webhooks (`WebhookPayloadInspectionModal.razor`) e investigação de falhas de mensageria (`WhatsAppFailureDetailsModal.razor`).

## Consequências
- **Positivas:**
  - Visibilidade holística e em tempo real sobre a saúde financeira e operacional do ecossistema SaaS.
  - Zero acoplamento entre os bancos e módulos de negócio, validado por 100% de sucesso nos testes de arquitetura `NetArchTest`.
  - Investigação de incidentes operacionais rápida e autoexplicativa com trilha forense completa.
  - 703 testes automatizados aprovados com cobertura ponta a ponta (unitários, integração com SQL Server Testcontainers e bUnit).
- **Mitigações:**
  - A consolidação transversal de métricas pode envolver volumes maiores de dados em períodos extensos; atenuada por agrupamentos indexados e paginação nas consultas de logs e falhas.
