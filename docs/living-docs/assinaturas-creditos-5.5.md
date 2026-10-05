# Assinaturas e Créditos Recorrentes — Subfase 5.5

## 1. Visão Geral & Objetivo

A subfase **5.5** do Lavaway SaaS implementa o modelo de assinaturas mensais e pacotes recorrentes de lavagens por placa de veículo cadastrada. O sistema visa assegurar receita previsível (MRR) para o lava-jato ou estética automotiva, oferecendo conveniência aos clientes finais e governança financeira estrita:
- **Planos Mensais Flexíveis:** Definição de planos com periodicidade mensal, limites de placas autorizadas por assinatura e quantidade fixa de créditos de serviço inclusos no ciclo.
- **Cobrança Recorrente no Cartão:** Integração com gateway via porta `IRecurringBillingGatewayProvider`, capturando token e identificador de assinatura, renovando ciclos e créditos de forma idempotente.
- **Controle por Placa de Veículo:** Cada assinatura mantém uma lista explícita de placas autorizadas (`SubscriptionVehiclePlate`). O consumo do crédito exige a correspondência exata da placa atendida na Ordem de Serviço.
- **Garantia de Não Ultrapassagem de Saldo:** O domínio garante que `UsedCredits <= TotalCredits`. Quando o saldo atinge zero no ciclo atual, novas tentativas de consumo são bloqueadas instantaneamente.
- **Idempotência Operacional:** A mesma Ordem de Serviço não pode ser liquidada duas vezes com créditos da mesma assinatura.
- **Isolamento Multi-tenant Estrito:** Planos, assinaturas, veículos e extratos operam segregados por `TenantId` em todas as camadas.

---

## 2. Diagrama de Sequência Mermaid: Adesão e Cobrança Recorrente

```mermaid
sequenceDiagram
    autonumber
    actor Atendente as Atendente / Cliente
    participant UI as SubscriptionsPage (Blazor Wasm)
    participant Api as SubscriptionsEndpoints (Minimal API)
    participant App as SubscriptionApplicationService (Billing)
    participant Gateway as IRecurringBillingGatewayProvider
    participant SubRepo as ICustomerSubscriptionRepository
    participant PlanRepo as ISubscriptionPlanRepository

    Atendente->>UI: Submete formulário de adesão (Cliente, Plano, Placas, Cartão)
    UI->>Api: POST /subscriptions (SubscribeCustomerRequest)
    Api->>App: SubscribeCustomerAsync(tenantId, request)
    App->>PlanRepo: GetByIdAsync(tenantId, planId)
    PlanRepo-->>App: SubscriptionPlan (Credits: 4, PlatesLimit: 2)
    App->>Gateway: CreateSubscriptionAsync(tenantId, customerId, price, cardData)
    Gateway-->>App: RecurringGatewaySubscriptionData (GatewaySubId, LastFour: 4242, Brand: Visa)
    App->>App: CustomerSubscription.Create(plates: ["ABC1D23"], credits: 4, period: 30d)
    App->>SubRepo: AddAsync(customerSubscription)
    App->>SubRepo: SaveChangesAsync()
    SubRepo-->>App: OK (Gravado no schema billing)
    App-->>Api: Result.Success(CustomerSubscriptionDto)
    Api-->>UI: 201 Created (Assinatura Ativa)
    UI-->>Atendente: Exibe confirmação com placas e saldo de 4 créditos
```

---

## 3. Diagrama de Sequência Mermaid: Consumo de Crédito por Placa na OS

```mermaid
sequenceDiagram
    autonumber
    actor Operador as Operador / Caixa
    participant Modal as RegisterWorkOrderPaymentModal (Blazor)
    participant Cashier as CashRegisterApplicationService (Billing)
    participant SubService as SubscriptionApplicationService (Billing)
    participant SubRepo as ICustomerSubscriptionRepository
    participant Yard as WorkOrderApplicationService (YardOperations)

    Operador->>Modal: Abre modal de recebimento da OS (Placa: "ABC1D23")
    Modal->>SubService: GetActiveSubscriptionByPlateAsync(tenantId, "ABC1D23")
    SubService-->>Modal: SubscriptionPlateSummaryDto (Plan: "Plano Ouro", AvailableCredits: 3, CanConsume: true)
    Modal->>Modal: Exibe opção "⭐ Crédito Assinatura (3 disp.)"
    Operador->>Modal: Seleciona "Crédito Assinatura" e confirma
    Modal->>Cashier: RegisterWorkOrderPaymentAsync(workOrderId, SubscriptionCredit, plate: "ABC1D23")
    Cashier->>SubService: ConsumeCreditForWorkOrderAsync(workOrderId, plate: "ABC1D23")
    SubService->>SubRepo: GetActiveByPlateAsync(tenantId, "ABC1D23", now)
    SubRepo-->>SubService: CustomerSubscription
    SubService->>SubService: Subscription.ConsumeCredit("ABC1D23", workOrderId, now)
    alt Saldo Esgotado ou Placa Não Autorizada
        SubService-->>Cashier: Result.Failure("subscription.credits_exhausted" ou "plate_unauthorized")
        Cashier-->>Modal: Exibe mensagem de erro ao operador
    else Sucesso no Débito do Crédito
        SubService->>SubRepo: SaveChangesAsync() (UsedCredits++, grava SubscriptionUsage)
        SubService-->>Cashier: Result.Success(SubscriptionUsageReceiptDto)
        Cashier->>Yard: SettlePaymentAsync(workOrderId, SubscriptionCredit, TxId: "CRED-...")
        Yard-->>Cashier: WorkOrder marcada como IsPaid = true
        Cashier-->>Modal: Result.Success(CashTransactionDto)
        Modal-->>Operador: OS baixada com sucesso e badge "⭐ Assinatura" atualizado no Kanban
    end
```

---

## 4. Diagrama de Estados do Ciclo de Vida da Assinatura

```mermaid
stateDiagram-v2
    [*] --> Ativa: Adesão e primeiro pagamento aprovado
    Ativa --> Ativa: Consumo de créditos (UsedCredits < TotalCredits)
    Ativa --> CreditosEsgotados: Todos os créditos consumidos no ciclo (Available == 0)
    CreditosEsgotados --> Ativa: Renovação mensal do ciclo (RenewCycle) recarrega saldo
    Ativa --> Inadimplente: Falha na cobrança recorrente do gateway (PastDue)
    Inadimplente --> Ativa: Regularização do pagamento recorrente
    Ativa --> Cancelada: Solicitação do cliente ou operador (Cancel)
    Inadimplente --> Cancelada: Cancelamento por falta de pagamento
    Cancelada --> [*]
```

---

## 5. Especificações Executáveis (BDD / Gherkin)

```gherkin
Funcionalidade: Gestão de Planos de Assinatura e Créditos Recorrentes por Placa
  Como proprietário de um lava-jato
  Quero disponibilizar planos mensais com créditos por placa de veículo
  Para garantir faturamento recorrente previsível e fidelizar clientes

  Cenário: Adesão de cliente com placas autorizadas respeitando o limite do plano
    Dado que existe um plano "Clube Duo" com limite de 2 placas e 4 créditos mensais
    Quando o cliente "Mariana" assina o plano informando as placas "BRA2E19" e "XYZ9876"
    Então a assinatura é criada com status "Ativa"
    E o saldo inicial de créditos disponíveis é 4
    E ambas as placas constam como autorizadas para uso

  Cenário: Bloqueio ao tentar vincular mais placas do que o limite do plano
    Dado que o plano "Clube Individual" permite no máximo 1 placa
    Quando o operador tenta vincular 2 placas na contratação
    Então o sistema rejeita a operação com erro "subscription.plate_limit_reached"

  Cenário: Consumo com sucesso de crédito de serviço para veículo autorizado
    Dado que a assinatura possui 2 créditos disponíveis para a placa "BRA2E19"
    Quando uma Ordem de Serviço para o veículo "BRA2E19" é baixada com "Crédito de Assinatura"
    Então 1 crédito é debitado da assinatura
    E o saldo restante de créditos passa a ser 1
    E um registro de utilização com o Id da OS é adicionado ao histórico contábil

  Cenário: Rejeição de consumo para veículo com placa não autorizada
    Dado que a assinatura está ativa mas apenas autoriza a placa "BRA2E19"
    Quando o cliente tenta utilizar o crédito para o veículo de placa "OUT9999"
    Então o sistema recusa o consumo com o erro "subscription.plate_unauthorized"
    E nenhum crédito é debitado

  Cenário: Garantia de entrega: consumo bloqueado quando créditos estão esgotados
    Dado que uma assinatura mensal já teve todos os seus 4 créditos utilizados no ciclo
    Quando uma tentativa de novo consumo é solicitada para a placa autorizada
    Então o sistema recusa a baixa com o erro "subscription.credits_exhausted"
    E o saldo de créditos permanece em 0 sem ultrapassar o limite disponível

  Cenário: Renovação do ciclo mensal recarrega os créditos
    Dado que a assinatura teve os créditos esgotados no ciclo anterior
    Quando o gateway confirma o pagamento da renovação mensal
    Então a vigência da assinatura é estendida por mais 30 dias
    E a contagem de créditos utilizados é reiniciada em 0
    E o saldo de créditos volta a ser o total do plano contratado
```
