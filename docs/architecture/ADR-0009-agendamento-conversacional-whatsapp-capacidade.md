# ADR-0009: Agendamento Conversacional via WhatsApp, Gestão de Sessão do Chatbot e Prevenção de Conflitos de Capacidade

## Status
Aceito

## Contexto
Na subfase 4.3 do Lavaway SaaS, tornou-se mandatória a disponibilização de agendamento autônomo diretamente pelo WhatsApp da loja, permitindo que os clientes consultem o catálogo de serviços, verifiquem horários vagos e reservem horários com prevenção estrita de lotação simultânea de boxes (`YardCapacity.TotalBoxes`), refletindo a reserva em tempo real na visão operacional da equipe.

Restrições arquiteturais ([agents.md](file:///home/rony/LPR/lavaway/agents.md)):
- **Monolito Modular:** O módulo `WhatsApp` não pode referenciar DbContexts nem repositórios do módulo `YardOperations`.
- **Arquitetura Hexagonal:** Comunicação síncrona exclusiva via interfaces públicas em `Shared.Contracts`.
- **Comunicação Assíncrona:** Ingestão de webhooks desacoplada via filas (`IBackgroundQueue` / RabbitMQ) para proteger a disponibilidade do webhook da Evolution API.
- **Multi-Tenancy:** Isolamento estrito por tenant com `IMustHaveTenant` e Global Query Filters no EF Core.
- **Padrão Result:** Retorno de operações sem uso de exceções para regras de negócio (`Result<T>`).

## Decisões

### 1. Comunicação Desacoplada entre Chatbot e Pátio via `ISchedulingBookingLookup`
- Criada a interface pública `ISchedulingBookingLookup` no projeto `CarWashSaaS.Shared.Contracts`.
- Implementada pelo serviço `BookingApplicationService` no módulo `YardOperations`.
- O motor do chatbot (`ChatbotConversationEngine`) no módulo `WhatsApp` consome unicamente essa interface, sem acesso ao banco de dados do pátio ou entidades internas.

### 2. Máquina de Estados FSM e Gestão de Sessões do Chatbot
- Implementada a entidade de domínio `ChatbotConversationSession` no módulo `WhatsApp` (schema `whatsapp`) com máquina de estados finitos (`ChatbotStep`: `Greeting`, `Menu`, `SelectingService`, `SelectingVehicleSize`, `SelectingDate`, `SelectingTimeSlot`, `CollectingPlate`, `AwaitingConfirmation`, `Completed`).
- Timeout automático de inatividade fixado em 30 minutos e suporte a comandos globais de reinício (`menu`, `cancelar`, `sair`, `voltar`).
- Descarte obrigatório de mensagens inbound com `fromMe == true` no webhook para prevenir loops de respostas automatizadas.

### 3. Prevenção de Conflito de Capacidade (`BookingCapacityChecker`)
- A verificação de horários vagos calcula a disponibilidade baseada em $\text{TotalBoxes} - \text{ReservasAtivasNoSlot}$.
- A confirmação da reserva executa validação concorrente atômica no banco, retornando erro de domínio `booking.capacity.exceeded` caso o box tenha sido preenchido concorrentemente, permitindo ao bot orientar o cliente a escolher outro horário vago.

### 4. Visão Operacional da Equipe e Integração com Recepção
- Adicionada a página `SchedulingPage.razor` sob a rota `/scheduling` com o link `03 | Agenda & Reservas` no `NavMenu.razor`.
- Componentes `BookingCard.razor` (com visual de placa Mercosul, badges de status, badge de origem do WhatsApp e ações) e `BookingCapacityMeter.razor` (medidor de pico de boxes ocupados).
- Ação "Iniciar Atendimento" redireciona para a recepção (`/reception?plate=...&phone=...`), preenchendo automaticamente o cadastro e agilizando a abertura de OS.

## Consequências

- **Positivas:**
  - Isolamento modular absoluto verificado automaticamente por testes de arquitetura (`NetArchTest`).
  - Resiliência na ingestão de mensagens através de fila assíncrona RabbitMQ sem travar a thread HTTP da Evolution API.
  - Prevenção total de overbooking ou conflito de capacidade no pátio físico.
  - Equipe operacional ganha visibilidade completa dos agendamentos do dia originados pelo WhatsApp.
- **Negativas / Trade-offs:**
  - O fluxo conversacional textual requer normalização e parsing de respostas numéricas e datas digitadas pelo usuário, tratado com mensagens de orientação claras em caso de entradas inválidas.
