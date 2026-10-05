# Caixa, Fechamento Diário e Comissões — Subfase 5.3

## 1. Visão Geral & Objetivo

A subfase **5.3** do Lavaway SaaS implementa o controle operacional de caixa e o cálculo automatizado de comissões para lava-jatos e centros de estética automotiva. O sistema permite dar baixa imediata em ordens de serviço por múltiplos métodos (**Pix**, **Dinheiro** com troco calculado, **Cartão de Crédito** e **Cartão de Débito**), efetuar sangrias e aportes, emitir o fechamento diário do caixa com conferência cega/assistida de gaveta e gerar relatórios detalhados de comissões por colaborador com base nas regras configuradas no estabelecimento.

---

## 2. Diagrama de Sequência Mermaid: Baixa de Pagamento Presencial

```mermaid
sequenceDiagram
    autonumber
    actor Operador as Operador / Caixa
    participant UI as Blazor WASM (RegisterWorkOrderPaymentModal)
    participant API as Minimal API (POST /billing/cashier/payments/work-order)
    participant Cashier as CashRegisterApplicationService (Billing)
    participant Yard as IWorkOrderPaymentSettlementService (YardOperations)
    participant CashRepo as ICashTransactionRepository (Billing)
    participant Notifier as IYardRealtimeNotifier (SignalR)

    Operador->>UI: Seleciona OS e clica em "Receber"
    UI->>UI: Informa método (Dinheiro/Cartão/Pix) e valor
    opt Pagamento em Dinheiro
        UI->>UI: Digita valor recebido e visualiza Troco dinâmico
    end
    Operador->>UI: Clica em "Confirmar Pagamento e Baixar OS"
    UI->>API: POST /billing/cashier/payments/work-order
    API->>Cashier: RegisterWorkOrderPaymentAsync(tenantId, request)
    Cashier->>Yard: SettlePaymentAsync(tenantId, workOrderId, amount, method, ref, date)
    Yard->>Yard: WorkOrder.MarkPaymentConfirmed(amount, method, date, ref)
    Yard->>Notifier: Notifica baixa da OS (WorkOrderMovedNotification)
    Notifier-->>UI: Atualiza card no Kanban (Badge "💳 Pago")
    Yard-->>Cashier: Result.Success (OS baixada)
    Cashier->>CashRepo: AddAsync(CashTransaction.CreateIncome(...))
    Cashier->>CashRepo: SaveChangesAsync()
    Cashier-->>API: Result.Success(CashTransactionDto)
    API-->>UI: 200 OK + Toast de Sucesso
```

---

## 3. Diagrama de Estados do Caixa Diário

```mermaid
stateDiagram-v2
    [*] --> Aberto: Início do dia com primeiro lançamento ou aporte
    Aberto --> Aberto: Recebimento de OS (Pix, Dinheiro, Cartão)
    Aberto --> Aberto: Aporte de Troco (Supply)
    Aberto --> Aberto: Sangria de Dinheiro (Bleed)
    Aberto --> Fechado: Fechamento auditado (CloseDailyCashRequest)
    Fechado --> Reaberto: Reabertura para ajuste excepcional com justificativa
    Reaberto --> Fechado: Novo encerramento auditado
    Fechado --> [*]: Expediente arquivado para relatórios
```

---

## 4. Especificações Executáveis (BDD / Gherkin)

```gherkin
Funcionalidade: Caixa e Fechamento Diário
  Como gestor ou operador de caixa
  Quero registrar receitas por Pix, dinheiro e cartão, e realizar o fechamento diário
  Para manter o controle financeiro rigoroso do estabelecimento sem discrepâncias

  Cenário: Baixa de OS em dinheiro com cálculo de troco
    Dado que uma Ordem de Serviço possui valor total de R$ 120,00
    Quando o operador registra o pagamento em Dinheiro informando R$ 150,00 recebidos
    Então o sistema deve calcular o troco de R$ 30,00
    E a Ordem de Serviço deve ser marcada como paga
    E uma transação de receita de R$ 120,00 deve ser adicionada ao caixa

  Cenário: Sangria em dinheiro na gaveta
    Dado que o caixa do dia possui entradas em espécie
    Quando o gestor registra uma sangria de R$ 50,00 para "Compra de insumos"
    Então uma transação do tipo Bleed deve ser registrada
    E o saldo esperado em gaveta deve ser decrementado em R$ 50,00

  Cenário: Fechamento diário com conferência de gaveta
    Dado que o expediente do dia possui receitas e sangrias registradas
    Quando o gestor submete o fechamento diário com o valor físico contado
    Então o sistema deve consolidar os totais por Pix, Dinheiro e Cartões
    E registrar a diferença de caixa apurada
    E o status do caixa do dia deve ser atualizado para "Closed"
```

---

## 5. Especificações Executáveis: Apuração de Comissões

```gherkin
Funcionalidade: Cálculo de Comissões
  Como gestor do lava-jato
  Quero consultar o relatório de comissões por colaborador e período
  Para pagar a equipe de forma justa conforme os serviços efetivamente realizados

  Cenário: Cálculo de comissão com regra cadastrada
    Dado que o colaborador "Roberto" possui cargo "Detailer"
    E existe uma regra de comissão de 20% para "Polimento Cristalizado" e cargo "Detailer"
    Quando Roberto conclui uma OS contendo um serviço de "Polimento Cristalizado" de R$ 500,00
    Então a comissão calculada para o serviço deve ser de R$ 100,00
    E o relatório do período deve apresentar Roberto com R$ 100,00 de comissão acumulada

  Cenário: Serviço sem regra de comissão configurada
    Dado que o colaborador executa um serviço sem regra de comissão para seu cargo
    Quando o relatório de comissões for gerado
    Então a comissão para esse serviço específico deve ser computada como R$ 0,00 (0%)
```
