# Agendamento Conversacional via WhatsApp e Agenda Operacional — Subfase 4.3

## 1. Visão Geral & Objetivo

Permitir que clientes de lava-jatos e estética automotiva consultem o catálogo de serviços, verifiquem horários vagos e reservem horários pelo WhatsApp da loja de forma 100% autônoma através de um chatbot conversacional com máquina de estados. O sistema garante a prevenção de conflitos de capacidade física (`TotalBoxes`), gerencia sessões com timeout de 30 minutos e exibe as reservas em tempo real para a equipe na tela de **Agenda & Reservas**, com ação de check-in para abertura direta de Ordem de Serviço na recepção.

> **Padronização de Fuso Horário (`BrazilTime`):** A resolução de "Hoje" e "Amanhã" no `ChatbotConversationEngine`, bem como o filtro de horários já decorridos no dia atual em `BookingCapacityChecker`, utilizam `BrazilTime.Today` e `BrazilTime.CurrentTime` (`America/Sao_Paulo` / `UTC-03:00`), evitando bloqueio prematuro de vagas vespertinas ou virada de dia às 21h00 em servidores internacionais.

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
        opt Sessão Expirada
            FSM->>SessionRepo: Complete() & UpdateAsync(session) [persiste inativação]
        end
        FSM->>SessionRepo: Start() & AddAsync(session) [persiste nova sessão]
        FSM->>WAppSender: Envia Menu Principal com Opções (1: Agendar, 2: Catálogo, 3: Minhas Reservas)
        FSM->>SessionRepo: MoveToStep(Menu) & UpdateAsync(session) [persiste transição]
    else Escolheu Agendar (Opção 1)
        FSM->>SchedContract: ICustomerLookupService.GetByPhoneAsync(tenantId, phone)
        FSM->>SchedContract: GetAvailableServicesCatalogAsync(tenantId)
        SchedContract-->>FSM: Lista de Serviços com Menor Preço
        FSM->>WAppSender: Saúda cliente cadastrado (ou solicita nome se novo) e envia Catálogo Numerado
        FSM->>SessionRepo: MoveToStep(SelectingService) & UpdateAsync(session)
    else Selecionou Serviço (Cliente com Veículos Cadastrados)
        FSM->>SchedContract: ICustomerLookupService.GetByPhoneAsync(tenantId, phone)
        FSM->>WAppSender: Lista veículos já cadastrados (placa/porte) + opção "Outro veículo"
        FSM->>SessionRepo: MoveToStep(SelectingRegisteredVehicle) & UpdateAsync(session)
    else Selecionou Veículo Cadastrado (ou Porte Manual) e Data
        FSM->>SchedContract: GetAvailableTimeSlotsAsync(tenantId, date, serviceId, size)
        SchedContract->>YardApp: Calcula Slots Vagos vs YardCapacity.TotalBoxes
        YardApp-->>SchedContract: Slots com Vagas Disponíveis
        FSM->>WAppSender: Envia Horários Vagos Disponíveis
        FSM->>SessionRepo: MoveToStep(SelectingTimeSlot) & UpdateAsync(session)
    else Confirmou Reserva
        FSM->>SchedContract: CreateBookingFromChatbotAsync(tenantId, request c/ CustomerId)
        SchedContract->>YardApp: Reutiliza Customer existente e cria Booking com trava de capacidade
        YardApp-->>SchedContract: BookingConfirmationDto
        FSM->>SessionRepo: Complete() & UpdateAsync(session) [persiste conclusão e desativação]
        FSM->>WAppSender: Envia Confirmação Formal com Protocolo
    end
    Note over SessionRepo: Persistência Atômica via SaveChangesAsync() em cada escopo com isolamento por TenantId
```

---

## 3. Máquina de Estados da Conversa (FSM)

```mermaid
stateDiagram-v2
    [*] --> Greeting
    Greeting --> Menu: Primeira interação ou comando "menu"
    
    Menu --> SelectingService: Opção 2 (Catálogo)
    Menu --> AwaitingReminderAction: Opção 3 (Minhas Reservas — 1 reserva ativa)
    Menu --> SelectingBookingToManage: Opção 3 (Minhas Reservas — múltiplas reservas ativas)
    SelectingBookingToManage --> AwaitingReminderAction: Escolhe reserva (1..N ou protocolo)
    AwaitingReminderAction --> Menu: Digita 1 (Confirmar) ou 3 (Cancelar)
    AwaitingReminderAction --> ReschedulingDate: Digita 2 (Remarcar)
    ReschedulingDate --> ReschedulingTimeSlot: Informa nova data
    ReschedulingTimeSlot --> Menu: Escolhe novo horário
    
    Menu --> CheckingCustomer: Opção 1 (Agendar)
    CheckingCustomer --> SelectingService: Cliente já cadastrado
    CheckingCustomer --> ConfirmingCustomerName: Não cadastrado & possui PushName
    CheckingCustomer --> CollectingCustomerName: Não cadastrado & sem PushName
    
    ConfirmingCustomerName --> SelectingService: Opção 1 (Confirma PushName) -> Cadastra Cliente
    ConfirmingCustomerName --> CollectingCustomerName: Opção 2 (Informar outro nome)
    CollectingCustomerName --> SelectingService: Envia nome completo válido -> Cadastra Cliente
    
    SelectingService --> SelectingRegisteredVehicle: Escolhe serviço & cliente possui veículos cadastrados
    SelectingService --> SelectingVehicleSize: Escolhe serviço & cliente sem veículos cadastrados
    
    SelectingRegisteredVehicle --> SelectingDate: Seleciona veículo salvo (1..N) -> Define Placa, Porte e Preço
    SelectingRegisteredVehicle --> CollectingPlate: Seleciona "Outro veículo" (N+1)
    CollectingPlate --> SelectingVehicleSize: Informa placa válida (quando veio de "Outro veículo")
    
    SelectingVehicleSize --> SelectingDate: Escolhe porte (1: Hatch, 2: SUV, 3: Picape, 4: Moto)
    SelectingDate --> SelectingTimeSlot: Informa data (Hoje, Amanhã ou DD/MM)
    
    SelectingTimeSlot --> AwaitingConfirmation: Placa já definida (veículo salvo ou "Outro veículo")
    SelectingTimeSlot --> CollectingPlate: Placa ainda não definida (cliente sem veículos prévios)
    CollectingPlate --> AwaitingConfirmation: Informa placa válida (quando porte já estava definido)
    
    AwaitingConfirmation --> Completed: Digita 1 (Confirmar) -> Salva Veículo (se novo) & Confirma Reserva
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
- **Dado** que existe um agendamento confirmado na agenda para o veículo "ABC-1D23" do cliente "Carlos" com serviço e observações,
- **Quando** o cliente chega à loja e o operador clica no botão "Iniciar Atendimento" no card da reserva,
- **Então** o sistema prepara o contexto de check-in (assegurando cadastro de cliente e veículo se ainda não persistidos) e abre diretamente a tela de Abertura de OS (`/work-orders/new?bookingId={id}`),
- **E** a OS é carregada com cliente, veículo, serviço contratado pré-selecionado (Qtd 1) e observações preenchidas,
- **E** ao confirmar a abertura da OS, a reserva é vinculada ao `WorkOrderId` e tem seu status atualizado para `Arrived` ("Em Atendimento").

### Cenário 4: Cliente não cadastrado inicia agendamento e confirma PushName
- **Dado** que um cliente cujo número de WhatsApp não possui correspondência no cadastro do lava-jato (mesmo considerando variações com/sem DDI `55` e 8 ou 9 dígitos) envia mensagem com PushName "Mariana",
- **Quando** seleciona a opção 1 para agendar,
- **Então** o chatbot pergunta se confirma o cadastro com o nome "Mariana",
- **E** ao responder "1" (Sim), o sistema cadastra o cliente no tenant e avança diretamente para a escolha do serviço.

### Cenário 5: Cliente recorrente com veículos cadastrados seleciona veículo logo após o serviço
- **Dado** que um cliente já possui cadastro no lava-jato (localizado pelo número de WhatsApp com equivalência de DDI `55` e 8/9 dígitos) com os veículos "ABC1D23" (Hatch/Sedan) e "XYZ9876" (SUV),
- **Quando** escolhe agendar e seleciona o serviço desejado,
- **Então** o chatbot o reconhece pelo nome sem recadastrá-lo e apresenta imediatamente a lista numerada dos seus veículos já cadastrados (junto à opção "Outro veículo"),
- **E** ao escolher um veículo cadastrado, o sistema preenche automaticamente a placa, o porte e o valor correspondente ao porte daquele veículo, avançando direto para a escolha de data e horário,
- **E** caso escolha "Outro veículo", o chatbot solicita a nova placa e o porte antes de prosseguir para a escolha de data e horário.

### Cenário 6: Cliente com múltiplos agendamentos ativos escolhe qual reserva cancelar
- **Dado** que o cliente possui mais de um agendamento ativo a partir da data atual (localizados por equivalência de telefone `Customer.GetCandidateNormalizedPhones` ou vínculo `CustomerId`, ordenados cronologicamente),
- **Quando** o cliente acessa a opção 3 ("Meus agendamentos") ou envia "cancelar" no WhatsApp,
- **Então** o chatbot apresenta a lista numerada de seus agendamentos futuros e solicita que informe qual deles deseja gerenciar (`SelectingBookingToManage`),
- **E** ao selecionar o agendamento desejado e optar por cancelar ("3"), o sistema altera o status da reserva selecionada para `Cancelled`, limpa o carimbo `ConfirmedAtUtc` e atualiza automaticamente o painel de Agenda & Reservas.

---

## 5. Endpoints REST & Contratos

| Método | Rota | Autorização | Finalidade |
| :--- | :--- | :--- | :--- |
| `POST` | `/whatsapp/webhooks/evolution` | Anônimo (valida Secret) | Ingestão de mensagens recebidas (`messages_upsert`) |
| `GET` | `/scheduling/bookings` | `Receptionist` | Lista agendamentos do dia por data, status e busca |
| `GET` | `/scheduling/bookings/{id}` | `Receptionist` | Consulta detalhes de um agendamento |
| `POST` | `/scheduling/bookings` | `Receptionist` | Criação manual de reserva pela equipe |
| `POST` | `/scheduling/bookings/{id}/prepare-checkin` | `Receptionist` | Prepara contexto de check-in assegurando cliente/veículo e retornando dados para abertura da OS |
| `POST` | `/scheduling/bookings/{id}/cancel` | `Receptionist` | Cancelamento de reserva com justificativa |
| `GET` | `/scheduling/slots` | `Receptionist` | Consulta horários vagos e capacidade de boxes |

---

## 6. Especificação Visual do Componente BookingCard (Agenda Operacional)

### 6.1 Hierarquia Visual e Isolamento de Estilos

```mermaid
flowchart TD
    subgraph Card ["BookingCard (.booking-card.card-status-*)"]
        direction TB
        Header["Header (.booking-header, flex-wrap)"]
        subgraph HeaderElements ["Elementos do Header"]
            Time["TimeBadge (.time-badge)"]
            Badges["HeaderBadges (.header-badges, wrap)"]
            Origin["Origem (.origin-badge)"]
            StatusBadge["Status (.status-badge.status-*)"]
        end
        Vehicle["Veículo (.booking-vehicle-row): Placa Mercosul + Modelo + Porte"]
        Customer["Cliente (.booking-customer-row): Nome (WCAG AA) + Telefone Formatado"]
        Service["Serviço (.booking-service-row): Nome do Serviço + Preço Estimado"]
        Notes["Observações (.booking-notes, condicional)"]
        Footer["Footer (.booking-footer): Protocolo + Ações / Atendimento Aberto"]
    end
```

### 6.2 Diretrizes de Estilização e Acessibilidade (WCAG AA)
1. **Superfície Sólida do Card (`#122421`):** O container possui fundo escuro sólido e borda de destaque superior (`card-status-*`), prevenindo qualquer colisão de especificidade CSS com os badges de status internos.
2. **Badges Escopados:** Classes de status (`status-scheduled`, `status-confirmed`, `status-arrived`, `status-completed`, `status-cancelled`) são escopadas a `.status-badge`, mantendo `white-space: nowrap`.
3. **Responsividade sem Transbordamento:** Cabeçalho e rodapé utilizam `flex-wrap: wrap` e `gap: 6px-8px`, garantindo que cards em colunas de 320px acomodem múltiplos badges sem truncamento.
4. **Formatação de Dados:** Telefone exibido com máscara brasileira legível `(XX) XXXXX-XXXX` ou `(XX) XXXX-XXXX` e link direto para conversa via `wa.me`.

