# ADR-0010: Confirmação e Prevenção de Faltas via WhatsApp com Lembretes Automatizados 24h e 2h

## Status
Aceito

## Contexto
Na subfase 4.4 do Lavaway SaaS, tornou-se necessária a implementação de um mecanismo ativo para prevenir no-shows (faltas de clientes) e assegurar a máxima taxa de ocupação dos boxes físicos de lavagem e estética automotiva.
O sistema deve:
1. Enviar notificações ativas de lembrete 24 horas e 2 horas antes do início agendado de cada reserva ativa.
2. Permitir que o cliente interaja com o chatbot respondendo com ações rápidas: **1: Confirmar**, **2: Remarcar** ou **3: Cancelar**.
3. Atualizar o agendamento no pátio físico e liberar o box imediatamente em caso de cancelamento ou remarcação sem depender de intervenção humana.
4. Manter a equipe operacional informada em tempo real na visão de Agenda & Reservas com indicadores visuais de confirmação e status dos disparos de lembretes, além de permitir o reenvio manual sob demanda.

Restrições arquiteturais ([agents.md](file:///home/rony/LPR/lavaway/agents.md)):
- **Monolito Modular & Hexagonal:** O módulo `WhatsApp` e o módulo `YardOperations` comunicam-se via `ISchedulingBookingLookup` (localizado em `Shared.Contracts`).
- **Comunicação Assíncrona & Idempotência:** Disparo de mensagens através da infraestrutura de mensageria com chaves determinísticas de idempotência (`reminder-24h-{id}` e `reminder-2h-{id}`).
- **Multi-Tenancy:** Varredura de agendamentos com isolamento estrito de tenant sem vazamento de dados (`IMustHaveTenant` e escopo por tenant).
- **Padrão Result:** Retorno de operações sem uso de exceções (`Result<T>`).

## Decisões

### 1. Entidade de Domínio `Booking` e Carimbos Temporais de Lembrete
- Foram introduzidas propriedades de auditoria na entidade de domínio `Booking`:
  - `Reminder24hSentAt`: Data/hora UTC em que o lembrete de 24h foi despachado.
  - `Reminder2hSentAt`: Data/hora UTC em que o lembrete de 2h foi despachado.
  - `ConfirmedAtUtc`: Data/hora UTC em que a presença foi expressamente confirmada pelo cliente ou operador.
- Métodos ricos de domínio adicionados: `Confirm()`, `Reschedule(DateOnly, TimeOnly)`, `MarkReminder24hSent()` e `MarkReminder2hSent()`.
- O status da reserva transita de `Pending` para `Confirmed` quando confirmada, e de `Confirmed` ou `Pending` para `Cancelled` quando cancelada.

### 2. Serviço Automatizado de Varredura e Disparo (`BookingReminderHostedService` & `BookingReminderApplicationService`)
- Criado um `BackgroundService` (`BookingReminderHostedService`) com intervalo configurável (padrão a cada 5 minutos).
- Para garantir isolamento multi-tenant seguro:
  - O hosted service consulta a lista de tenants ativos via `ITenantRepository`.
  - Executa a varredura (`ScanAndSendRemindersAsync`) criando um `IServiceScope` e injetando o contexto do tenant correspondente via `ICurrentTenantContext`.
- As queries do repositório filtram reservas com status ativo (`Pending` ou `Confirmed`), na janela de disparo e com os respectivos campos nulos (`Reminder24hSentAt == null` ou `Reminder2hSentAt == null`).
- O despacho utiliza `IOutboundWhatsAppDispatcher` com mensagem formatada incluindo o menu rápido de confirmação/remarcação/cancelamento e chaves de idempotência rigorosas.

### 3. FSM de Interação Conversacional no WhatsApp
- A máquina de estados do chatbot (`ChatbotConversationEngine`) foi estendida com os estados:
  - `AwaitingReminderAction`: Quando o cliente responde ao lembrete ativo, permitindo digitar 1 (Confirmar), 2 (Remarcar) ou 3 (Cancelar).
  - `ReschedulingDate`: Recebe a nova data solicitada.
  - `ReschedulingTimeSlot`: Exibe os novos horários vagos e conclui a remarcação.
- Integração com `ISchedulingBookingLookup`:
  - `ConfirmBookingAsync`: Atualiza o status para `Confirmed` e registra `ConfirmedAtUtc`.
  - `CancelBookingAsync`: Atualiza o status para `Cancelled`, liberando imediatamente a vaga para novos clientes.
  - `RescheduleBookingAsync`: Valida concorrentemente a disponibilidade de box no novo horário antes de aplicar a alteração.

### 4. Visão Operacional e Reenvio Manual no Frontend Blazor
- Atualização do componente `BookingCard.razor`:
  - Badges visuais indicando: Confirmação expressa (`✅ Confirmado`), Lembrete 24h enviado (`🔔 24h`) e Lembrete 2h enviado (`⏳ 2h`).
  - Botão de ação "Confirmar" (quando pendente), "Remarcar" (abre modal de data/hora) e "Reenviar Lembrete" (dispara notificação imediata via WhatsApp).
- Modal `RescheduleBookingModal` integrado na `SchedulingPage.razor` para remarcações manuais pela recepção física.

## Consequências

- **Positivas:**
  - Redução drástica da taxa de faltas (no-shows) de clientes devido à comunicação proativa.
  - Reatribuição automática de vagas ociosas causadas por cancelamentos ou remarcações sem intervenção manual.
  - Visibilidade em tempo real para a equipe operacional saber exatamente quais veículos estão confirmados para o turno.
  - Total rastreabilidade e idempotência no envio de notificações WhatsApp.
- **Negativas / Trade-offs:**
  - O HostedService adiciona ciclos periódicos de verificação a cada 5 minutos no banco de dados, mitigado por índices nas colunas `ScheduledDate`, `ScheduledTime` e flags de lembretes.
