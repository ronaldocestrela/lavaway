# ADR-0016: Assinaturas e Créditos Recorrentes com Controle por Placa Cadastrada

## Status
Aceito

## Contexto
Na subfase 5.5 do Lavaway SaaS, tornou-se necessária a implementação de um modelo de receita recorrente previsível (MRR) através de clubes de assinatura e pacotes mensais de lavagens, atendendo aos seguintes objetivos:
1. **Planos Mensais e Cobrança Recorrente:** Permitir aos tenants criar planos com periodicidade configurável, valor de mensalidade e quantidade de créditos/lavagens inclusos.
2. **Integração com Gateway de Pagamento:** Processamento recorrente de cartão de crédito via contrato de porta `IRecurringBillingGatewayProvider` e gateway simulado determinístico, preparado para integração com adquirentes e gateways de mercado (Mercado Pago, Asaas, Stripe).
3. **Controle Estrito por Placa Cadastrada:** Cada assinatura é vinculada a uma ou mais placas de veículos autorizadas (`SubscriptionVehiclePlate`), respeitando o limite do plano contratado (`AllowedPlatesLimit`). Apenas veículos com placa formalmente associada podem usufruir dos créditos.
4. **Garantia de Não Ultrapassagem de Saldo (Ledger Invariante):** O consumo de créditos é estritamente controlado no domínio (`UsedCreditsInCycle < TotalCreditsInCycle`). Se o saldo se esgotar, o sistema bloqueia tentativas adicionais com erro expressivo de validação.
5. **Idempotência e Prevenção de Cobranças Duplicadas:** Uma mesma Ordem de Serviço não pode consumir créditos duplicados, garantido por invariantes de domínio e índice único no banco de dados.
6. **Integração Intermodular Desacoplada:** Integração limpa entre `Billing` e `YardOperations` via `ISubscriptionLookup` e `ISubscriptionUsageService` em `CarWashSaaS.Shared.Contracts`, em estrita conformidade com as regras de isolamento do `ModuleBoundaryTests`.

## Decisões

### 1. Modelo de Domínio e Agregados em `Billing`
- **`SubscriptionPlan` (Aggregate Root):** UUIDv7, `TenantId`, `Name`, `Description`, `MonthlyPrice`, `CreditsPerCycle`, `AllowedPlatesLimit`, `BillingIntervalDays`, `IsActive`.
- **`CustomerSubscription` (Aggregate Root):** UUIDv7, `TenantId`, `CustomerId`, `CustomerName`, `CustomerPhone`, `PlanId`, `PlanName`, `Status` (`Active`, `PastDue`, `Canceled`), vigência do ciclo (`CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`), `TotalCreditsInCycle`, `UsedCreditsInCycle`, dados mascarados do cartão e identificador no gateway.
  - Coleção de placas autorizadas: `IReadOnlyCollection<SubscriptionVehiclePlate> AuthorizedPlates`.
  - Coleção contábil de consumos: `IReadOnlyCollection<SubscriptionUsage> Usages`.
  - Invariantes: `CanConsumeCredit(plate, nowUtc)`, `ConsumeCredit(plate, workOrderId, serviceName, nowUtc, notes)`, `AddAuthorizedPlate(plate, limit, nowUtc)`, `RemoveAuthorizedPlate(plate)`, `RenewCycle(start, end, credits)`, `Cancel(reason, nowUtc)`.

### 2. Mapeamento e Persistência no EF Core (Schema `billing`)
- Tabelas mapeadas: `billing.SubscriptionPlans`, `billing.CustomerSubscriptions`, `billing.SubscriptionVehiclePlates` e `billing.SubscriptionUsages`.
- Índice condicional único `IX_SubscriptionUsages_TenantId_WorkOrderId` filtrado por `[WorkOrderId] IS NOT NULL` para prevenção definitiva de consumo duplicado em concorrência.
- Query filters globais por tenant e validação em tempo de escrita (`ValidateTenantWrites`) assegurando isolamento multi-tenant absoluto.

### 3. Portas e Adapters de Gateway Recorrente
- Interface `IRecurringBillingGatewayProvider` no Application de `Billing`.
- Implementação padrão `SimulatedRecurringBillingGatewayProvider` que gera identificadores de assinatura, detecta bandeiras de cartão (Visa, Mastercard, Elo, Amex), armazena apenas últimos 4 dígitos mascarados e suporta cartões terminados em `0000` para testes de recusa determinísticos.

### 4. Integração Intermodular via `Shared.Contracts`
- `ISubscriptionLookup`: Permite consultar status, saldo e autorização de placa (`GetActiveSubscriptionByPlateAsync`) sem referenciar tabelas ou DbContext de `Billing`.
- `ISubscriptionUsageService`: Permite que a recepção ou o caixa debitem créditos e estornem consumos em caso de cancelamento de OS.
- `PaymentMethodConstants.SubscriptionCredit`: Adicionado como método de pagamento oficial em toda a plataforma.

### 5. Frontend Blazor WebAssembly
- Nova página `SubscriptionsPage.razor` (`/subscriptions`, item `08` do menu principal), com visual escuro elegante, cards de KPIs (Assinaturas Ativas, MRR Estimado, Créditos Disponíveis, Usos no Mês), abas de Assinantes e Planos Comerciais.
- Modais dedicados:
  - `CreateSubscriptionPlanModal.razor`: Criação e edição de planos de recorrência.
  - `SubscribeCustomerModal.razor`: Formulário ágil de contratação com vínculo de cliente, seleção de plano, validação de placas e simulação de cartão de crédito.
  - `SubscriptionUsageHistoryModal.razor`: Extrato auditável de utilizações por placa e OS.
  - `ManageSubscriptionPlatesModal.razor`: Gerenciador de placas autorizadas com validação de limite.
- Integração no `RegisterWorkOrderPaymentModal.razor`: Reconhecimento instantâneo de veículos assinantes e opção de liquidação por crédito com 1 clique.
- Atualização visual no card do Kanban (`YardKanbanCard.razor`): Badge `⭐ Assinatura` para ordens liquidadas via crédito recorrente.

## Consequências

- **Positivas:**
  - Criação de fluxo contínuo de receita previsível (MRR) para o estabelecimento de estética automotiva.
  - Bloqueio rígido de fraudes de empréstimo de plano: apenas veículos com placa autorizada podem consumir créditos.
  - O saldo do ciclo nunca é ultrapassado, protegendo o estabelecimento contra prejuízos operacionais.
  - Total conformidade com arquitetura hexagonal, padrões Result, isolamento multi-tenant e suíte de testes automatizados.
- **Mitigações de Risco:**
  - Rollback automático de consumo caso a ordem de serviço seja cancelada.
  - Tratamento gracioso quando o cliente troca de veículo: a gestão de placas permite substituição dentro do limite estipulado pelo plano.
