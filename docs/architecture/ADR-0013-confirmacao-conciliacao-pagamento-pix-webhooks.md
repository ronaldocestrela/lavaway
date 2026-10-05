# ADR-0013: Confirmação, Idempotência e Conciliação de Pagamento Pix via Webhooks

## Status
Aceito

## Contexto
Na subfase 5.2 do Lavaway SaaS, após a disponibilização da cobrança Pix associada à Ordem de Serviço (OS) na subfase 5.1, tornou-se mandatória a implementação do recebimento automatizado das notificações de pagamento enviadas pelos gateways (Mercado Pago e Gateway Simulado) via webhooks REST.

Requisitos essenciais:
1. **Autenticidade & Validação de Assinatura:** Garantir que webhooks recebidos foram genuinamente emitidos pelo gateway bancário oficial (Mercado Pago com cabeçalho `x-signature` HMAC-SHA256 e proteção anti-replay via janela de tolerância de timestamp; Provedor Simulado via segredo compartilhado constante).
2. **Idempotência Absoluta:** Gateways de pagamento realizam reenvios compulsórios (*retries*) em casos de lentidão ou oscilação de rede. Eventos repetidos ou duplicados não podem recriar históricos, alterar valores nem duplicar mensagens enviadas ao cliente.
3. **Desacoplamento Hexagonal Intermodular:** O módulo `Billing` é responsável por receber e validar o webhook e atualizar o agregado `PixCharge`. A baixa e conciliação da `WorkOrder` no módulo `YardOperations` deve ocorrer exclusivamente por contratos tipados em `Shared.Contracts` (`IWorkOrderPaymentSettlementService`), sem compartilhamento de `DbContext` ou entidades de banco de dados.
4. **Isolamento Multi-Tenant:** Toda operação é isolada por `TenantId`, prevenindo qualquer vazamento de dados ou alteração indevida entre estabelecimentos distintos.
5. **Reatividade em Tempo Real no Frontend:** A equipe operacional no balcão e no pátio deve visualizar a baixa do pagamento instantaneamente no Kanban (`YardKanbanCard`) e no modal de cobrança Pix (`WorkOrderPixModal`).

## Decisões

### 1. Entidade de Domínio e Tabela de Idempotência `ProcessedPaymentWebhook`
- Criada a entidade `ProcessedPaymentWebhook` no módulo `CarWashSaaS.Billing.Domain` (schema `billing`), implementando `IMustHaveTenant`.
- O mapeamento EF Core estabelece índice único composto em `(TenantId, Provider, EventId)`.
- Antes de processar qualquer evento, o sistema verifica se o identificador do evento já foi registrado; se já foi, retorna HTTP 200 OK imediatamente sem reprocessamento colateral (garantia de idempotência estrita).

### 2. Validador Criptográfico de Webhook (`IPaymentWebhookValidator`)
- Criada a interface `IPaymentWebhookValidator` e sua implementação `PaymentWebhookValidator`:
  - **Mercado Pago:** Valida o cabeçalho `x-signature` (`ts=[timestamp],v1=[hmac]`) conferindo se o timestamp está dentro do limite aceitável de 5 minutos (prevenindo *replay attacks*) e comparando a assinatura HMAC-SHA256 do manifesto (`id:[dataId];request-id:[requestId];ts:[ts];`) com `CryptographicOperations.FixedTimeEquals`.
  - **Simulated:** Valida segredo constante seguro com tempo constante, viabilizando testes unitários, testes de integração e desenvolvimento local sem dependência de serviços externos.

### 3. Comunicação Intermodular de Liquidação (`IWorkOrderPaymentSettlementService`)
- Criado o contrato público `IWorkOrderPaymentSettlementService` em `CarWashSaaS.Shared.Contracts`:
  - Método `SettlePaymentAsync(tenantId, workOrderId, paidAmount, paymentMethod, transactionReference, paidAtUtc)`.
  - Implementado pelo `WorkOrderApplicationService` no módulo `YardOperations`.
  - Atualiza o agregado de domínio `WorkOrder` (`IsPaid = true`, `PaidAtUtc`, `PaymentMethod`, `PaidAmount`, `PaymentTransactionId`) e registra entrada de auditoria no histórico da OS.
  - Emite notificação em tempo real via `IYardRealtimeNotifier`.

### 4. Ciclo de Baixa no Orquestrador (`PixBillingApplicationService`)
- No método `ProcessPaymentWebhookAsync`:
  1. Valida identificadores e idempotência no `IProcessedPaymentWebhookRepository`.
  2. Consulta detalhes adicionais do pagamento no gateway caso o status não venha explícito ou necessite confirmação.
  3. Atualiza o status do `PixCharge` para `Paid` via `charge.MarkAsPaid(paidAtUtc)`.
  4. Executa a baixa da OS via `IWorkOrderPaymentSettlementService`.
  5. Salva o registro em `ProcessedPaymentWebhooks`.
  6. Dispara notificação de confirmação amigável via WhatsApp ao cliente caso o despachador esteja ativo.

### 5. Experiência de Usuário no Frontend Blazor
- **Kanban (`YardKanbanCard.razor`):** Exibição de badge visual verde `💳 Pago` no bloco de preço da OS e alteração do botão de ação para `✅ Pix Pago`.
- **Modal (`WorkOrderPixModal.razor`):** Quando a cobrança está paga, substitui o QR Code por um painel de comprovante e recibo de conciliação com data/hora, valor pago, TxId e selo de recebimento autenticado. Adicionado botão para verificação pontual caso o operador queira forçar a consulta.

## Consequências

- **Positivas:**
  - Idempotência absoluta em nível de banco de dados e aplicação.
  - Baixa de pagamentos segura e auditável sem necessidade de intervenção manual da equipe.
  - Notificação de confirmação automática no WhatsApp do cliente após a confirmação do gateway.
  - Cumprimento integral dos testes de arquitetura (NetArchTest), testes unitários, integração multi-tenant e componentes Blazor.

- **Mitigações de Risco:**
  - Tolerância máxima de 5 minutos para timestamp de webhook, rejeitando payloads antigos reutilizados.
  - Verificação dupla do status no gateway antes de efetivar a liquidação financeira da OS.
