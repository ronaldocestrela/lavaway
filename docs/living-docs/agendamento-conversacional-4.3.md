# Agendamento Conversacional via WhatsApp e Agenda Operacional — Subfase 4.3

## 1. Visão Geral & Objetivo

Permitir que clientes de lava-jatos e estética automotiva consultem o catálogo de serviços, verifiquem horários vagos e reservem horários pelo WhatsApp da loja de forma 100% autônoma através de um chatbot conversacional com máquina de estados. O sistema garante a prevenção de conflitos de capacidade física (`TotalBoxes`), gerencia sessões com timeout de 30 minutos e exibe as reservas em tempo real para a equipe na tela de **Agenda & Reservas**, com ação de check-in para abertura direta de Ordem de Serviço na recepção.

---

## 2. Diagrama de Sequência & Ingestão Assíncrona

```mermaid
sequenceDiagram
    autonumber
    actor Cliente as Cliente (WhatsApp)
    participant Evo as Evolution API
    participant Webhook as WhatsAppEndpoints (/whatsapp/webhooks/evolution)
    participant Queue as RabbitMQ (IBackgroundQueue)
    participant Worker as TenantQueueWorker
    participant Handler as InboundWhatsAppMessageHandler
    participant FSM as ChatbotConversationEngine
    participant SessionRepo as IChatbotSessionRepository
    participant SchedContract as ISchedulingBookingLookup (Shared.Contracts)
    participant YardApp as BookingApplicationService
    participant WAppSender as IOutboundWhatsAppDispatcher

    Cliente->>Evo: Envia "Olá, quero agendar"
    Evo->>Webhook: POST /whatsapp/webhooks/evolution (messages_upsert)
    Webhook->>Webhook: Valida secret, extrai TenantId e descarta se fromMe == true
    Webhook->>Queue: EnqueueAsync(whatsapp.inbound.dispatch, InboundWhatsAppReceivedEvent)
    Webhook-->>Evo: 204 NoContent

    Note over Queue,Worker: Processamento Desacoplado em Background
    Worker->>Queue: DequeueAsync()
    Worker->>Handler: HandleAsync(InboundWhatsAppReceivedEvent)
    Handler->>FSM: ProcessIncomingMessageAsync(tenantId, phone, pushName, text)
    FSM->>SessionRepo: GetActiveByPhoneAsync(tenantId, phone)

    alt Sessão Nova ou Expirada (>30 min)
        FSM->>SessionRepo: Inicia Sessão (Greeting)
        FSM->>WAppSender: Envia Menu Principal com Opções (1: Agendar, 2: Catálogo, 3: Minhas Reservas)
    else Escolheu Agendar (Opção 1)
        FSM->>SchedContract: GetAvailableServicesCatalogAsync(tenantId)
        SchedContract-->>FSM: Lista de Serviços com Menor Preço
        FSM->>WAppSender: Envia Catálogo Numerado
    else Selecionou Serviço e Porte
        FSM->>SchedContract: GetAvailableTimeSlotsAsync(tenantId, date, serviceId, size)
        SchedContract->>YardApp: Calcula Slots Vagos vs YardCapacity.TotalBoxes
        YardApp-->>SchedContract: Slots com Vagas Disponíveis
        FSM->>WAppSender: Envia Horários Vagos Disponíveis
    else Confirmou Horário e Placa
        FSM->>SchedContract: CreateBookingFromChatbotAsync(tenantId, request)
        SchedContract->>YardApp: Cria Booking no banco com trava de capacidade
        YardApp-->>SchedContract: BookingConfirmationDto
        FSM->>SessionRepo: Complete()
        FSM->>WAppSender: Envia Confirmação Formal com Protocolo
    end
```

---

## 3. Máquina de Estados da Conversa (FSM)

```mermaid
stateDiagram-v2
    [*] --> Greeting
    Greeting --> Menu: Primeira interação ou comando "menu"
    Menu --> SelectingService: Opção 1 ou 2
    Menu --> ViewingBookings: Opção 3
    SelectingService --> SelectingVehicleSize: Escolhe número do serviço
    SelectingVehicleSize --> SelectingDate: Escolhe porte (1: Hatch, 2: SUV, 3: Picape, 4: Moto)
    SelectingDate --> SelectingTimeSlot: Informa data (Hoje, Amanhã ou DD/MM)
    SelectingTimeSlot --> CollectingPlate: Escolhe horário vago disponível
    CollectingPlate --> AwaitingConfirmation: Informa placa válida
    AwaitingConfirmation --> Completed: Digita 1 (Confirmar)
    AwaitingConfirmation --> Menu: Digita 2 (Cancelar)
    Completed --> [*]
```

---

## 4. Especificações Executáveis (BDD)

### Cenário 1: Cliente agenda serviço com sucesso via WhatsApp
- **Dado** que o estabelecimento possui 2 boxes cadastrados na capacidade operacional,
- **E** o cliente envia uma mensagem de saudação pelo WhatsApp para a loja,
- **Quando** o cliente seleciona a opção de agendamento, escolhe o serviço "Lavagem Completa", o porte "Hatch / Sedan", a data de amanhã às 14:00 e informa a placa "ABC-1234",
- **E** confirma o agendamento digitando "1",
- **Então** o sistema cria uma reserva no status `Confirmed` com protocolo gerado,
- **E** envia mensagem de confirmação para o WhatsApp do cliente com o protocolo e endereço da loja,
- **E** a reserva passa a ser exibida na tela de Agenda & Reservas da equipe operacional.

### Cenário 2: Prevenção de conflito de capacidade física (Overbooking)
- **Dado** que o pátio possui capacidade de 1 box físico cadastrado,
- **E** já existe uma reserva ativa confirmada para o horário das 10:00 da manhã,
- **Quando** um segundo cliente tenta confirmar um agendamento para o mesmo horário das 10:00,
- **Então** o sistema rejeita a operação com erro de capacidade excedida (`booking.capacity.exceeded`),
- **E** o chatbot informa educadamente que a vaga acabou de ser preenchida e convida o cliente a selecionar outro horário vago.

### Cenário 3: Operador inicia atendimento a partir da reserva
- **Dado** que existe um agendamento confirmado na agenda para o veículo "ABC-1D23" do cliente "Carlos",
- **Quando** o cliente chega à loja e o operador clica no botão "Iniciar Atendimento" no card da reserva,
- **Então** o sistema redireciona para a tela de Recepção com a placa e o telefone pré-preenchidos,
- **E** a busca automática é disparada para agilizar a criação da Ordem de Serviço.

---

## 5. Endpoints REST & Contratos

| Método | Rota | Autorização | Finalidade |
| :--- | :--- | :--- | :--- |
| `POST` | `/whatsapp/webhooks/evolution` | Anônimo (valida Secret) | Ingestão de mensagens recebidas (`messages_upsert`) |
| `GET` | `/scheduling/bookings` | `Receptionist` | Lista agendamentos do dia por data, status e busca |
| `GET` | `/scheduling/bookings/{id}` | `Receptionist` | Consulta detalhes de um agendamento |
| `POST` | `/scheduling/bookings` | `Receptionist` | Criação manual de reserva pela equipe |
| `POST` | `/scheduling/bookings/{id}/cancel` | `Receptionist` | Cancelamento de reserva com justificativa |
| `GET` | `/scheduling/slots` | `Receptionist` | Consulta horários vagos e capacidade de boxes |
