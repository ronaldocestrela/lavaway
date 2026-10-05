# ADR-0015: Programa de Fidelidade, Selos e Notificações de Retenção

## Status
Aceito

## Contexto
Na subfase 5.4 do Lavaway SaaS, tornou-se necessária a implementação de um programa de fidelidade configurável por tenant, permitindo:
1. **Acúmulo Automático de Selos/Pontos:** Atribuição sistemática e idempotente de selos a cada Ordem de Serviço concluída ou paga.
2. **Prevenção Rigorosa de Fraudes e Duplicidades:** Garantir que uma mesma OS não pontue mais de uma vez para o cliente sob nenhuma hipótese de concorrência ou reprocessamento.
3. **Notificação Ativa de Retenção via WhatsApp:** Disparo de mensagens automáticas de engajamento quando o cliente se aproxima do resgate, em especial quando resta **apenas 1 serviço** (`Remaining == 1`) ou quando a meta é alcançada (`Remaining == 0`).
4. **Resgate Auditável com Ledger:** Abatimento de pontos com validação rigorosa de saldo mínimo e registro de transação com tipo e justificativa.
5. **Consulta Conversacional e Omni-channel:** Acesso instantâneo ao saldo de fidelidade tanto pelo operador no balcão (Blazor WebAssembly) quanto pelo próprio cliente via chatbot WhatsApp.
6. **Isolamento Multi-Tenant e Conformidade Arquitetural:** Políticas e saldos segregados estritamente por tenant, respeitando os limites modulares definidos em `ModuleBoundaryTests`.

## Decisões

### 1. Modelo de Domínio e Ledger de Transações em `YardOperations`
- Entidade `LoyaltyProgram`: Configuração por tenant (`TenantId`, `IsEnabled`, `TargetStamps`, `RewardTitle`, `ProximityThreshold`, `AllServicesEligible`, `EligibleCategoryFilter`).
- Agregado Raiz `CustomerLoyaltyAccount`: Identificador UUIDv7, `TenantId`, `CustomerId`, `Balance`, `LifetimeEarned`, `LifetimeRedeemed`.
  - Métodos invariantes: `CreditStamps(stamps, workOrderId, targetStamps)`, `RedeemReward(stamps, notes)`, `AdjustBalance(delta, notes)`.
  - Métodos de conveniência: `IsNearRedemption(threshold, target)`, `CalculateRemaining(target)`, `IsEligibleForReward(target)`.
- Entidade `LoyaltyTransaction`: Registro auditável contendo `TenantId`, `CustomerLoyaltyAccountId`, `DeltaStamps`, `BalanceAfter`, `Type` (`Earned`, `Redeemed`, `Adjustment`), `WorkOrderId` e `Notes`.

### 2. Idempotência e Integridade no Banco de Dados
- Mapeamento EF Core no schema `yard` com as tabelas `LoyaltyPrograms`, `CustomerLoyaltyAccounts` e `LoyaltyTransactions`.
- Criação de índice exclusivo condicional para garantir idempotência irrestrita:
  - `IX_LoyaltyTransactions_TenantId_CustomerLoyaltyAccountId_WorkOrderId` com filtro `[WorkOrderId] IS NOT NULL AND [Type] = 1`.

### 3. Integração com Ordens de Serviço e WhatsApp
- `WorkOrderApplicationService` invoca `ILoyaltyApplicationService.CreditStampForCompletedWorkOrderAsync` de forma síncrona nos métodos `ChangeStatusAsync(ReadyForPickup)` e `SettlePaymentAsync`.
- `LoyaltyApplicationService` verifica a proximidade do resgate e, caso o cliente esteja no limiar configurado (ex: `Remaining == 1` ou `0`), monta a mensagem personalizada e enfileira no `IWhatsAppDispatchQueue` (Outbox com entrega confiável).

### 4. Integração Intermodular via `Shared.Contracts` (Chatbot WhatsApp)
- Criada a interface `ILoyaltyLookup` em `CarWashSaaS.Shared.Contracts` para expor `GetCustomerLoyaltySummaryAsync(tenantId, customerPhone)`.
- `ChatbotConversationEngine` em `CarWashSaaS.WhatsApp.Application` consome `ILoyaltyLookup` através de injeção de dependência, interceptando mensagens com intenção de fidelidade (`"FIDELIDADE"`, `"SELOS"`, `"PONTOS"`) sem quebrar a regra de isolamento entre módulos de aplicação (`ModuleBoundaryTests`).

### 5. Frontend Blazor WebAssembly
- Nova página `LoyaltyPage.razor` mapeada na rota `/loyalty` (item `07` do menu principal), com visual escuro elegante, cards de KPIs (Clientes Ativos, Total de Selos Acumulados, Recompensas Entregues, Clientes Próximos do Resgate), tabela com barra de progresso visual de selos e filtros.
- Componentes dedicados:
  - `RedeemLoyaltyRewardModal.razor`: Modal de resgate rápido com validação de saldo e cálculo do saldo restante.
  - `CustomerLoyaltyHistoryModal.razor`: Modal com extrato cronológico auditável de movimentações (créditos, resgates, ajustes).

## Consequências

- **Positivas:**
  - Estímulo direto à fidelização e retorno dos clientes com comunicação automatizada e humana via WhatsApp.
  - O operador tem visibilidade completa de clientes fiéis na recepção e no caixa.
  - Garantia formal de idempotência no banco de dados e no domínio contra duplicidade de créditos por OS.
  - Isolamento estrito entre módulos mantido com 100% de aprovação na suíte de testes de arquitetura.
- **Mitigações de Risco:**
  - Bloqueio de resgates com saldo insuficiente através de `Result.Failure`.
  - Tratamento gracioso quando o cliente não possui número de WhatsApp informado ou quando o programa estiver desabilitado no tenant.
