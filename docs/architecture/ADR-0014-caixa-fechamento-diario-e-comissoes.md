# ADR-0014: Caixa, Fechamento Diário e Apuração de Comissões

## Status
Aceito

## Contexto
Na subfase 5.3 do Lavaway SaaS, tornou-se necessária a implementação do controle financeiro de caixa do estabelecimento e a apuração de comissões devidas aos colaboradores operacionais (lavadores, polidores, finalizadores), permitindo:
1. **Registro Unificado de Receitas:** Baixa presencial no balcão de Ordens de Serviço por múltiplos métodos (**Pix**, **Dinheiro** com cálculo em tempo real de troco, **Cartão de Crédito** e **Cartão de Débito**).
2. **Movimentações Avulsas de Caixa:** Registro auditável de aportes/suprimento de troco e sangrias/retiradas em dinheiro.
3. **Fechamento Diário de Caixa:** Encerramento consolidado do expediente por data de referência, confronto entre o saldo esperado em espécie na gaveta (`Aportes + Dinheiro - Sangrias`) e a contagem física com registro de eventuais sobras ou faltas.
4. **Cálculo Desacoplado de Comissões:** Computação automatizada de comissões por colaborador baseada nas regras pré-configuradas (`CommissionRule` relacionando serviço e cargo do operador) e no histórico de atendimentos concluídos/pagos.
5. **Multi-Tenancy e Isolamento Hexagonal:** Segregação absoluta entre o módulo `Billing` (livro-razão e fechamentos diários) e o módulo `YardOperations` (ordens de serviço, colaboradores e regras de comissão) via interfaces públicas em `Shared.Contracts`.

## Decisões

### 1. Livro-Razão do Caixa no Módulo `Billing`
- Criados os agregados raiz em `CarWashSaaS.Billing.Domain`:
  - `CashTransaction`: Identificadores UUIDv7, `IMustHaveTenant`, montantes estritamente positivos, tipos (`Income`, `Bleed`, `Supply`) e métodos (`Pix`, `Cash`, `CreditCard`, `DebitCard`).
  - `DailyCashClosing`: Identificadores UUIDv7, `IMustHaveTenant`, índice único composto `(TenantId, ClosingDate)`, cálculo invariante de saldo esperado em gaveta, diferença física auditada e ciclo de vida (`Closed`, `Reopened`).
- Mapeamento EF Core no schema `billing` com índices de cobertura por tenant, data e forma de pagamento (`AddCashRegisterAndDailyClosing`).

### 2. Integração e Conciliação Intermodular
- O serviço `CashRegisterApplicationService` orquestra a baixa da OS acionando a porta `IWorkOrderPaymentSettlementService` implementada no módulo `YardOperations` e persiste a transação correspondente no `BillingDbContext`.
- O processamento de webhooks Pix em `PixBillingApplicationService` registra automaticamente uma transação do tipo `Income` (`PaymentMethodConstants.Pix`) no repositório `ICashTransactionRepository`, garantindo que recebimentos instantâneos por QR Code componham o fechamento diário de caixa.

### 3. Motor de Comissões em `YardOperations`
- Criado o serviço de domínio puro `CommissionCalculator`: dado um item de OS e o cargo (`Role`) do operador atribuído, localiza a regra correspondente em `CommissionRule` e calcula `(Percentage, CommissionAmount)` com arredondamento comercial bancário.
- Implementada a porta pública `ICommissionCalculationLookup` através de `CommissionApplicationService`, consolidando faturamento total, comissões apuradas e extrato detalhado por colaborador em períodos customizados (Hoje, Semana, Mês).

### 4. Minimal APIs e Segurança
- `CashierEndpoints` expostos sob `/billing/cashier/*`:
  - `POST /billing/cashier/payments/work-order`: Baixa de OS e registro financeiro.
  - `POST /billing/cashier/movements`: Sangrias e aportes.
  - `GET /billing/cashier/summary`: KPIs do dia, proporções por método e extrato.
  - `POST /billing/cashier/close`: Fechamento auditado do expediente.
  - `GET /billing/cashier/closings`: Histórico de fechamentos.
- `YardOperationsEndpoints`:
  - `GET /yard/commissions/report`: Relatório analítico e sintético de comissões.

### 5. Frontend Blazor WebAssembly
- Nova tela `CashierPage.razor` (`/cashier`, item `06` na navegação principal) com estética rica, dark mode, cards com gradientes, barra de proporção percentual de recebimentos, histórico e apuração sanfonada por profissional.
- Componentes dedicados:
  - `RegisterWorkOrderPaymentModal.razor`: Integrado no Kanban (`YardKanbanCard.razor`) e no caixa, com seletor de método e calculadora em tempo real de troco em dinheiro.
  - `CashMovementModal.razor`: Registro ágil de sangrias e reforços.
  - `CloseDailyCashModal.razor`: Encerramento do caixa com conferência física e justificativa.

## Consequências

- **Positivas:**
  - O gestor tem visão 360° do fluxo financeiro do dia, eliminando planilhas manuais e inconsistências no acerto de troco ou contagem de gaveta.
  - As comissões da equipe são calculadas com transparência auditável, reduzindo conflitos trabalhistas e operacionais.
  - O isolamento entre módulos permanece 100% íntegro e em conformidade com NetArchTest.
- **Mitigações de Risco:**
  - Bloqueio de múltiplos fechamentos para o mesmo dia no mesmo tenant (`daily_cash_closing.already_closed`).
  - Prevenção de baixas em dinheiro com valor recebido insuficiente.
