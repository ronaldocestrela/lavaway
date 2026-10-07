# ADR-0020: Planos Comerciais, Billing Recorrente do SaaS, Gestão de Cotas e Política de Acesso

## Status
Aceito

## Contexto
O ecossistema Lavaway opera no modelo B2B SaaS multi-tenant atendendo múltiplos centros de lavagem e estética automotiva. Enquanto a Subfase 5.5 estruturou clubes de assinaturas de clientes finais (B2C), a Subfase 6.3 requer a infraestrutura de monetização da própria plataforma:
1. Cobrança recorrente dos estabelecimentos com integração de gateways líderes de mercado (ex.: Asaas, Stripe ou Iugu), operando de forma desacoplada para evitar vendor lock-in e possibilitando simulação determinística de testes.
2. Catálogo de planos Básico, Pro e Enterprise, com precificação mensal e cotas de uso para ordens de serviço (OS) e notificações WhatsApp.
3. Política estrita de inadimplência com carência (*grace period* de 5 dias), bloqueio progressivo e automático de acesso operacional para tenants inadimplentes, e desbloqueio/reativação em tempo real mediante confirmação via webhook idempotente.
4. Comunicação intermodular limpa sem acoplamento entre DbContexts, respeitando as regras inegociáveis do `agents.md`.

## Decisão de Arquitetura

### 1. Hexagonal Architecture & Isolamento Modular
* O faturamento da plataforma reside no módulo `Billing`, mapeado no schema `billing` do SQL Server.
* Para os módulos de operação (`YardOperations`) e mensageria (`WhatsApp`), foi introduzida a porta pública `ITenantPlanQuotaLookup` em `CarWashSaaS.Shared.Contracts`.
* Na criação de ordens de serviço (`WorkOrderApplicationService.CreateAsync`) e no envio de mensagens (`WhatsAppMessageApplicationService.DispatchTextMessageAsync`), as cotas mensais e o status operacional são validados e debitados estritamente via abstrações, preservando os limites modulares validados pelo `NetArchTest`.

### 2. Provedor de Billing Desacoplado & Webhook Idempotente
* A porta de saída `ISaasBillingGatewayProvider` abstrai as operações de clientes, assinaturas e faturas. A implementação `SimulatedSaasBillingGatewayProvider` provê IDs previsíveis, links de fatura e QR Codes Pix dinâmicos para ambientes locais, CI/CD e testes.
* O endpoint `POST /platform/billing/webhooks` recebe notificações assíncronas do provedor, valida assinatura digital (HMAC-SHA256) e aplica controle de idempotência por meio do agregado `ProcessedSaasWebhookEvent`.

### 3. Política de Inadimplência & Período de Tolerância (Grace Period)
* Quando uma fatura vence ou falha no pagamento:
  - **Grace Period (até 5 dias):** A assinatura entra em `GracePeriod`. O estabelecimento mantém a operação normal de atendimento, porém um banner âmbar de aviso de cobrança (`DelinquencyAlertBanner.razor`) é fixado no topo de sua interface com atalho para regularização via Pix.
  - **Delinquency (após os 5 dias):** A assinatura transita para `Delinquent`. As portas de criação de novas OS e de envio de WhatsApp bloqueiam a requisição com erro de conflito de negócio (`tenant.subscription.delinquent`).
  - **Reativação Imediata:** O recebimento do evento `invoice.paid` ou liquidação manual reverte compulsoriamente a assinatura e o status do estabelecimento (`TenantStatus.Active`) em tempo real, resetando os contadores de cota para o novo ciclo de 30 dias.

### 4. Modelo de Entidades e Quotas
* `SaasPlan`: catálogo comercial global (sem isolamento por tenant) para os tiers `Basic`, `Pro` e `Enterprise`.
* `TenantSaasSubscription`: agregado raiz do tenant com status (`Trial`, `Active`, `GracePeriod`, `Delinquent`, `Canceled`), datas de vigência, tolerância e identificadores do gateway.
* `TenantQuotaUsage`: agregado por ciclo com contadores atômicos e métodos invariantes (`CanCreateWorkOrder`, `RecordWorkOrderCreated`, `CanSendWhatsApp`, `RecordWhatsAppSent`, `ResetForNewCycle`).
* `SaasInvoice`: faturas auditáveis com QR Code Pix dinâmico, código copia-e-cola e controle de status.

### 5. Frontend Blazor WebAssembly
* **Painel do Lojista (`/settings/subscription`):** Gestão visual do plano atual, barras de progresso de consumo de cotas (`PlanQuotaProgressBar.razor`), tabela de faturas e modais de upgrade de plano (`UpgradePlanModal.razor`) e pagamento Pix (`SettleInvoiceModal.razor`).
* **Backoffice da Plataforma (`/platform/billing`):** Painel analítico para operadores de plataforma com KPIs consolidados de MRR, assinaturas ativas, inadimplentes e tabela com status e consumo de todos os tenants.

## Consequências
* **Positivas:**
  - Total proteção contra inadimplência com bloqueio automatizado e autônomo.
  - Eliminação de acoplamento direto entre pátio, mensageria e financeiro.
  - Previsibilidade e auditabilidade completa do ciclo de vida das assinaturas.
  - 100% de cobertura nos testes automatizados (unitários, integração Testcontainers, arquitetura e componentes bUnit).
* **Mitigações:**
  - A consulta de cotas e status adiciona verificação em memória/banco na criação de OS e envio de mensagens; mitigado por índices e modelo enxuto de consulta.
