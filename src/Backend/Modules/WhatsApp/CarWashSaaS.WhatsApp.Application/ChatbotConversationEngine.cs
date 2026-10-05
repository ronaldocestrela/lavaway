using System.Globalization;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class ChatbotConversationEngine(
    IChatbotSessionRepository sessionRepository,
    ISchedulingBookingLookup schedulingLookup,
    ITenantStoreProfileLookup storeProfileLookup,
    IOutboundWhatsAppDispatcher messageDispatcher)
{
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    public async Task<Result> ProcessIncomingMessageAsync(
        Guid tenantId,
        string senderPhone,
        string? pushName,
        string incomingText,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(senderPhone))
        {
            return Result.Failure(new Error("chatbot.invalid_input", "Tenant and sender phone are required.", ErrorType.Validation));
        }

        var text = incomingText.Trim();
        var session = await sessionRepository.GetActiveByPhoneAsync(tenantId, senderPhone, ct);

        // Se sessão não existe ou expirou, inicia nova sessão
        if (session is null || session.IsExpired(SessionTimeout))
        {
            session = ChatbotConversationSession.Start(tenantId, senderPhone, pushName).Value!;
            await sessionRepository.AddAsync(session, ct);
        }
        else if (!string.IsNullOrWhiteSpace(pushName) && string.IsNullOrWhiteSpace(session.CustomerName))
        {
            session.SetCustomerName(pushName);
        }

        // Comandos globais de reset
        if (IsResetCommand(text))
        {
            session.Reset();
            await sessionRepository.UpdateAsync(session, ct);
            return await SendMainMenuAsync(tenantId, session, ct);
        }

        var lower = text.ToLowerInvariant().Trim();

        // Intercepta respostas contextuais a lembretes
        if (session.CurrentStep is ChatbotStep.Greeting or ChatbotStep.Menu)
        {
            var isConfirm = IsConfirmKeyword(lower);
            var isReschedule = IsRescheduleKeyword(lower);
            var isCancel = IsCancelKeyword(lower);

            if (isConfirm || isReschedule || isCancel)
            {
                var upcomingResult = await schedulingLookup.GetUpcomingBookingForCustomerAsync(tenantId, senderPhone, ct);
                if (upcomingResult.IsSuccess && upcomingResult.Value is not null)
                {
                    var upcoming = upcomingResult.Value;
                    if (isConfirm)
                    {
                        var confirmResult = await ExecuteConfirmationAsync(tenantId, session, upcoming, ct);
                        await sessionRepository.UpdateAsync(session, ct);
                        return confirmResult;
                    }

                    if (isCancel)
                    {
                        var cancelResult = await ExecuteCancellationAsync(tenantId, session, upcoming, ct);
                        await sessionRepository.UpdateAsync(session, ct);
                        return cancelResult;
                    }

                    if (isReschedule)
                    {
                        var rescheduleResult = await StartRescheduleFlowAsync(tenantId, session, upcoming, ct);
                        await sessionRepository.UpdateAsync(session, ct);
                        return rescheduleResult;
                    }
                }
            }
        }

        var result = session.CurrentStep switch
        {
            ChatbotStep.Greeting => await HandleGreetingAsync(tenantId, session, text, ct),
            ChatbotStep.Menu => await HandleMenuChoiceAsync(tenantId, session, text, ct),
            ChatbotStep.SelectingService => await HandleSelectingServiceAsync(tenantId, session, text, ct),
            ChatbotStep.SelectingVehicleSize => await HandleSelectingVehicleSizeAsync(tenantId, session, text, ct),
            ChatbotStep.SelectingDate => await HandleSelectingDateAsync(tenantId, session, text, ct),
            ChatbotStep.SelectingTimeSlot => await HandleSelectingTimeSlotAsync(tenantId, session, text, ct),
            ChatbotStep.CollectingPlate => await HandleCollectingPlateAsync(tenantId, session, text, ct),
            ChatbotStep.AwaitingConfirmation => await HandleAwaitingConfirmationAsync(tenantId, session, text, ct),
            ChatbotStep.AwaitingReminderAction => await HandleAwaitingReminderActionAsync(tenantId, session, text, ct),
            ChatbotStep.ReschedulingDate => await HandleReschedulingDateAsync(tenantId, session, text, ct),
            ChatbotStep.ReschedulingTimeSlot => await HandleReschedulingTimeSlotAsync(tenantId, session, text, ct),
            _ => await SendMainMenuAsync(tenantId, session, ct)
        };

        await sessionRepository.UpdateAsync(session, ct);
        return result;
    }

    private async Task<Result> HandleGreetingAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        return await SendMainMenuAsync(tenantId, session, ct);
    }

    private async Task<Result> HandleMenuChoiceAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        if (text is "1")
        {
            return await PresentServiceCatalogAsync(tenantId, session, isDirectBooking: true, ct);
        }

        if (text is "2")
        {
            return await PresentServiceCatalogAsync(tenantId, session, isDirectBooking: false, ct);
        }

        if (text is "3")
        {
            return await PresentCustomerBookingsAsync(tenantId, session, ct);
        }

        var reply = "Opção inválida. Por favor, digite:\n*1* para Agendar um serviço\n*2* para Ver catálogo e preços\n*3* para Consultar meus agendamentos";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
    }

    private async Task<Result> HandleSelectingServiceAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var servicesResult = await schedulingLookup.GetAvailableServicesCatalogAsync(tenantId, ct);
        if (!servicesResult.IsSuccess || servicesResult.Value!.Count == 0)
        {
            session.Reset();
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, "Não há serviços ativos cadastrados no momento.", ct);
        }

        var services = servicesResult.Value!;
        if (!int.TryParse(text, out var index) || index < 1 || index > services.Count)
        {
            var reply = $"Por favor, digite um número de *1 a {services.Count}* correspondente ao serviço desejado:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        var selected = services[index - 1];
        session.SelectService(selected.Id, selected.Name);
        session.MoveToStep(ChatbotStep.SelectingVehicleSize);

        var message = $"Você selecionou: *{selected.Name}*\n\n" +
                      "🚗 *Qual é o porte do seu veículo?*\n" +
                      "1 - Hatch / Sedan\n" +
                      "2 - SUV\n" +
                      "3 - Picape / Van\n" +
                      "4 - Moto\n\n" +
                      "Digite o *número do porte*:";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> HandleSelectingVehicleSizeAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var size = text switch
        {
            "1" => VehicleSizeConstants.HatchSedan,
            "2" => VehicleSizeConstants.Suv,
            "3" => VehicleSizeConstants.PickupVan,
            "4" => VehicleSizeConstants.Motorcycle,
            _ => null
        };

        if (size is null)
        {
            var reply = "Opção de porte inválida. Digite:\n1 - Hatch / Sedan\n2 - SUV\n3 - Picape / Van\n4 - Moto";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        var servicesResult = await schedulingLookup.GetAvailableServicesCatalogAsync(tenantId, ct);
        var service = servicesResult.Value?.FirstOrDefault(s => s.Id == session.SelectedServiceId);
        var price = service?.Prices.FirstOrDefault(p => p.VehicleSize.Equals(size, StringComparison.OrdinalIgnoreCase))?.Amount ?? 0m;

        session.SelectVehicleSize(size, price);
        session.MoveToStep(ChatbotStep.SelectingDate);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var dayAfter = today.AddDays(2);

        var message = $"Porte: *{VehicleSizeConstants.GetDisplayName(size)}* (Valor: *R$ {price:N2}*)\n\n" +
                      "📅 *Para qual data deseja agendar?*\n" +
                      $"1 - Hoje ({today:dd/MM})\n" +
                      $"2 - Amanhã ({tomorrow:dd/MM})\n" +
                      $"3 - {dayAfter:dd/MM}\n\n" +
                      "Ou digite a data no formato *DD/MM*:";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> HandleSelectingDateAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly targetDate;

        if (text is "1") targetDate = today;
        else if (text is "2") targetDate = today.AddDays(1);
        else if (text is "3") targetDate = today.AddDays(2);
        else if (TryParseDate(text, out var parsedDate)) targetDate = parsedDate;
        else
        {
            var reply = "Data inválida. Digite 1 para Hoje, 2 para Amanhã ou a data no formato DD/MM (Ex: 15/10):";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        if (targetDate < today)
        {
            var reply = "Não é possível agendar para uma data no passado. Por favor, escolha a partir de hoje:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        var slotsResult = await schedulingLookup.GetAvailableTimeSlotsAsync(
            tenantId,
            targetDate,
            session.SelectedServiceId!.Value,
            session.SelectedVehicleSize!,
            ct);

        if (!slotsResult.IsSuccess || slotsResult.Value!.Count == 0)
        {
            var reply = $"Infelizmente não há horários disponíveis para {targetDate:dd/MM}.\nPor favor, digite outra data no formato DD/MM:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        session.SelectDate(targetDate);
        session.MoveToStep(ChatbotStep.SelectingTimeSlot);

        var slots = slotsResult.Value!;
        var lines = slots.Select((s, i) => $"{i + 1} - {s.StartTime:HH\\:mm} ({s.AvailableBoxes} {(s.AvailableBoxes == 1 ? "vaga" : "vagas")})");
        var message = $"🕒 *Horários disponíveis para {targetDate:dd/MM}:*\n\n" +
                      string.Join("\n", lines) + "\n\n" +
                      "Digite o *número do horário desejado*:";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> HandleSelectingTimeSlotAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var slotsResult = await schedulingLookup.GetAvailableTimeSlotsAsync(
            tenantId,
            session.SelectedDate!.Value,
            session.SelectedServiceId!.Value,
            session.SelectedVehicleSize!,
            ct);

        var slots = slotsResult.Value ?? [];
        if (!int.TryParse(text, out var index) || index < 1 || index > slots.Count)
        {
            var reply = $"Por favor, digite um número de *1 a {slots.Count}* correspondente ao horário:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        var selectedSlot = slots[index - 1];
        session.SelectTime(selectedSlot.StartTime);

        if (string.IsNullOrWhiteSpace(session.VehiclePlate))
        {
            session.MoveToStep(ChatbotStep.CollectingPlate);
            var reply = "🚘 Por favor, digite a *placa do seu veículo* (Ex: ABC-1234 ou ABC1D23):";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        session.MoveToStep(ChatbotStep.AwaitingConfirmation);
        return await SendConfirmationSummaryAsync(tenantId, session, ct);
    }

    private async Task<Result> HandleCollectingPlateAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var normalizedPlate = new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalizedPlate.Length < 7 || normalizedPlate.Length > 10)
        {
            var reply = "Placa inválida. Por favor, digite uma placa válida (Ex: ABC-1234 ou BRA2E19):";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        session.SetVehiclePlate(normalizedPlate);
        session.MoveToStep(ChatbotStep.AwaitingConfirmation);
        return await SendConfirmationSummaryAsync(tenantId, session, ct);
    }

    private async Task<Result> HandleAwaitingConfirmationAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        if (text is "2" or "cancelar" or "não" or "nao")
        {
            session.Reset();
            var reply = "Agendamento cancelado. Se precisar de algo mais, digite *menu*. Até logo!";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        if (text is "1" or "confirmar" or "sim")
        {
            var bookingRequest = new CreateBookingFromChatbotRequest(
                session.CustomerPhone,
                session.CustomerName,
                session.VehiclePlate!,
                null,
                session.SelectedVehicleSize!,
                session.SelectedServiceId!.Value,
                session.SelectedDate!.Value,
                session.SelectedTime!.Value);

            var createResult = await schedulingLookup.CreateBookingFromChatbotAsync(tenantId, bookingRequest, ct);
            if (!createResult.IsSuccess)
            {
                if (createResult.Error!.Code == "booking.capacity.exceeded")
                {
                    session.MoveToStep(ChatbotStep.SelectingTimeSlot);
                    var reply = "⚠️ Ops! Esse horário acabou de ser preenchido por outro cliente.\n" +
                                "Por favor, digite *1* para escolher outro horário vago para a mesma data.";
                    return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
                }

                return await SendBotReplyAsync(tenantId, session.CustomerPhone, $"Não foi possível confirmar: {createResult.Error.Description}", ct);
            }

            var confirmation = createResult.Value!;
            session.Complete();

            var message = "✅ *Agendamento Confirmado com Sucesso!*\n\n" +
                          $"• Protocolo: *{confirmation.Protocol}*\n" +
                          $"• Serviço: *{confirmation.ServiceName}*\n" +
                          $"• Data: *{confirmation.ScheduledDate:dd/MM/yyyy} às {confirmation.ScheduledTime:HH\\:mm}*\n" +
                          $"• Valor estimado: *R$ {confirmation.EstimatedPrice:N2}*\n\n" +
                          "Aguardamos você no horário agendado! Se precisar remarcar ou cancelar, basta nos enviar uma mensagem. Até logo! 🚗✨";

            return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
        }

        var prompt = "Digite *1* para Confirmar o agendamento ou *2* para Cancelar:";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, prompt, ct);
    }

    private async Task<Result> SendMainMenuAsync(Guid tenantId, ChatbotConversationSession session, CancellationToken ct)
    {
        session.MoveToStep(ChatbotStep.Menu);
        var storeProfile = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfile.Value?.TradeName ?? "Lavaway Estética Automotiva";
        var nameGreeting = string.IsNullOrWhiteSpace(session.CustomerName) ? "" : $", *{session.CustomerName}*";

        var menu = $"👋 Olá{nameGreeting}! Bem-vindo ao *{storeName}* 🚗✨\n" +
                   "Eu sou o assistente virtual de agendamentos.\n\n" +
                   "Como podemos te ajudar hoje?\n" +
                   "1️⃣ - 📅 Agendar um serviço\n" +
                   "2️⃣ - 📋 Ver catálogo e preços\n" +
                   "3️⃣ - 🕒 Meus agendamentos\n\n" +
                   "Digite o *número da opção desejada*:";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, menu, ct);
    }

    private async Task<Result> PresentServiceCatalogAsync(Guid tenantId, ChatbotConversationSession session, bool isDirectBooking, CancellationToken ct)
    {
        var servicesResult = await schedulingLookup.GetAvailableServicesCatalogAsync(tenantId, ct);
        if (!servicesResult.IsSuccess || servicesResult.Value!.Count == 0)
        {
            var reply = "No momento não temos serviços disponíveis no catálogo. Por favor, tente novamente mais tarde.";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        session.MoveToStep(ChatbotStep.SelectingService);
        var services = servicesResult.Value!;
        var lines = services.Select((s, i) =>
        {
            var minPrice = s.Prices.Count > 0 ? s.Prices.Min(p => p.Amount) : 0m;
            return $"{i + 1} - *{s.Name}* (a partir de R$ {minPrice:N2})";
        });

        var title = isDirectBooking ? "📅 *Escolha o serviço para agendamento:*" : "📋 *Catálogo de Serviços Disponíveis:*";
        var message = $"{title}\n\n" +
                      string.Join("\n", lines) + "\n\n" +
                      "Digite o *número do serviço desejado*:";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> PresentCustomerBookingsAsync(Guid tenantId, ChatbotConversationSession session, CancellationToken ct)
    {
        var bookingsResult = await schedulingLookup.GetCustomerActiveBookingsAsync(tenantId, session.CustomerPhone, ct);
        var bookings = bookingsResult.Value ?? [];

        if (bookings.Count == 0)
        {
            var reply = "Você não possui agendamentos ativos no momento.\n\nDigite *1* para agendar um novo serviço ou *menu* para voltar.";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        session.SetTargetBooking(bookings[0].Id);
        session.MoveToStep(ChatbotStep.AwaitingReminderAction);

        var lines = bookings.Select(b => $"• *{b.Protocol}*: {b.ServiceName} ({b.VehiclePlate}) em {b.ScheduledDate:dd/MM} às {b.ScheduledTime:HH\\:mm} - Status: *{BookingStatusConstants.GetDisplayName(b.Status)}*");
        var message = "🕒 *Seus Próximos Agendamentos:*\n\n" +
                      string.Join("\n\n", lines) + "\n\n" +
                      "Deseja gerenciar seu agendamento? Digite:\n" +
                      "1️⃣ para *Confirmar presença*\n" +
                      "2️⃣ para *Remarcar horário*\n" +
                      "3️⃣ para *Cancelar agendamento*\n\n" +
                      "Ou digite *menu* para voltar.";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> HandleAwaitingReminderActionAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var lower = text.Trim().ToLowerInvariant();

        BookingSummaryDto? targetBooking = null;
        if (session.TargetBookingId.HasValue)
        {
            var activeResult = await schedulingLookup.GetCustomerActiveBookingsAsync(tenantId, session.CustomerPhone, ct);
            targetBooking = activeResult.Value?.FirstOrDefault(b => b.Id == session.TargetBookingId.Value);
        }

        if (targetBooking is null)
        {
            var upcomingResult = await schedulingLookup.GetUpcomingBookingForCustomerAsync(tenantId, session.CustomerPhone, ct);
            targetBooking = upcomingResult.Value;
        }

        if (targetBooking is null)
        {
            session.Reset();
            var reply = "Nenhum agendamento ativo encontrado para gerenciar. Digite *menu* para voltar ao início.";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        if (lower is "1" || IsConfirmKeyword(lower))
        {
            return await ExecuteConfirmationAsync(tenantId, session, targetBooking, ct);
        }

        if (lower is "2" || IsRescheduleKeyword(lower))
        {
            return await StartRescheduleFlowAsync(tenantId, session, targetBooking, ct);
        }

        if (lower is "3" || IsCancelKeyword(lower))
        {
            return await ExecuteCancellationAsync(tenantId, session, targetBooking, ct);
        }

        var prompt = "Opção inválida. Digite:\n*1* para Confirmar presença\n*2* para Remarcar horário\n*3* para Cancelar agendamento\nOu *menu* para voltar.";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, prompt, ct);
    }

    private async Task<Result> HandleReschedulingDateAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly targetDate;

        if (text is "1") targetDate = today;
        else if (text is "2") targetDate = today.AddDays(1);
        else if (text is "3") targetDate = today.AddDays(2);
        else if (TryParseDate(text, out var parsedDate)) targetDate = parsedDate;
        else
        {
            var reply = "Data inválida. Digite 1 para Hoje, 2 para Amanhã ou a data no formato DD/MM (Ex: 15/10):";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        if (targetDate < today)
        {
            var reply = "Não é possível remarcar para uma data no passado. Por favor, escolha a partir de hoje:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        var slotsResult = await schedulingLookup.GetAvailableTimeSlotsAsync(
            tenantId,
            targetDate,
            session.SelectedServiceId!.Value,
            session.SelectedVehicleSize!,
            ct);

        if (!slotsResult.IsSuccess || slotsResult.Value!.Count == 0)
        {
            var reply = $"Infelizmente não há horários disponíveis para {targetDate:dd/MM}.\nPor favor, digite outra data no formato DD/MM:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        session.SelectDate(targetDate);
        session.MoveToStep(ChatbotStep.ReschedulingTimeSlot);

        var slots = slotsResult.Value!;
        var lines = slots.Select((s, i) => $"{i + 1} - {s.StartTime:HH\\:mm} ({s.AvailableBoxes} {(s.AvailableBoxes == 1 ? "vaga" : "vagas")})");
        var message = $"🕒 *Horários disponíveis para {targetDate:dd/MM}:*\n\n" +
                      string.Join("\n", lines) + "\n\n" +
                      "Digite o *número do horário desejado*:";
        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> HandleReschedulingTimeSlotAsync(Guid tenantId, ChatbotConversationSession session, string text, CancellationToken ct)
    {
        var slotsResult = await schedulingLookup.GetAvailableTimeSlotsAsync(
            tenantId,
            session.SelectedDate!.Value,
            session.SelectedServiceId!.Value,
            session.SelectedVehicleSize!,
            ct);

        var slots = slotsResult.Value ?? [];
        if (!int.TryParse(text, out var index) || index < 1 || index > slots.Count)
        {
            var reply = $"Por favor, digite um número de *1 a {slots.Count}* correspondente ao horário:";
            return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
        }

        var selectedSlot = slots[index - 1];
        var bookingId = session.TargetBookingId!.Value;

        var rescheduleResult = await schedulingLookup.RescheduleBookingAsync(
            tenantId,
            bookingId,
            session.SelectedDate!.Value,
            selectedSlot.StartTime,
            ct);

        if (!rescheduleResult.IsSuccess)
        {
            if (rescheduleResult.Error!.Code == "booking.capacity.exceeded")
            {
                var reply = "⚠️ Ops! Esse horário acabou de ser preenchido por outro cliente.\n" +
                            "Por favor, digite outro número de horário vago listado acima:";
                return await SendBotReplyAsync(tenantId, session.CustomerPhone, reply, ct);
            }

            return await SendBotReplyAsync(tenantId, session.CustomerPhone,
                $"Não foi possível remarcar: {rescheduleResult.Error.Description}", ct);
        }

        var updated = rescheduleResult.Value!;
        session.Reset();

        var message = "✅ *Agendamento Remarcado com Sucesso!*\n\n" +
                      $"• Protocolo: *{updated.Protocol}*\n" +
                      $"• Serviço: *{updated.ServiceName}*\n" +
                      $"• Novo Horário: *{updated.ScheduledDate:dd/MM/yyyy} às {updated.ScheduledTime:HH\\:mm}*\n\n" +
                      "Te aguardamos no novo horário! Se precisar de algo mais, digite *menu*. 🚗✨";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> ExecuteConfirmationAsync(
        Guid tenantId,
        ChatbotConversationSession session,
        BookingSummaryDto booking,
        CancellationToken ct)
    {
        var confirmResult = await schedulingLookup.ConfirmBookingAsync(tenantId, booking.Id, ct);
        if (!confirmResult.IsSuccess)
        {
            return await SendBotReplyAsync(tenantId, session.CustomerPhone,
                $"Não foi possível confirmar o agendamento: {confirmResult.Error?.Description}", ct);
        }

        session.Reset();
        var message = "✅ *Presença Confirmada com Sucesso!*\n\n" +
                      $"Agradecemos a confirmação, *{session.CustomerName ?? booking.CustomerName}*!\n" +
                      $"Seu horário para o veículo *{booking.VehiclePlate}* está garantido para *{booking.ScheduledDate:dd/MM/yyyy} às {booking.ScheduledTime:HH\\:mm}*.\n\n" +
                      "Te aguardamos com tudo pronto! Se precisar de algo mais, digite *menu*. 🚗✨";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> ExecuteCancellationAsync(
        Guid tenantId,
        ChatbotConversationSession session,
        BookingSummaryDto booking,
        CancellationToken ct)
    {
        var cancelResult = await schedulingLookup.CancelBookingAsync(tenantId, booking.Id, "Cancelado pelo cliente via WhatsApp", ct);
        if (!cancelResult.IsSuccess)
        {
            return await SendBotReplyAsync(tenantId, session.CustomerPhone,
                $"Não foi possível cancelar o agendamento: {cancelResult.Error?.Description}", ct);
        }

        session.Reset();
        var message = "❌ *Agendamento Cancelado com Sucesso.*\n\n" +
                      $"Sua reserva #{booking.Protocol} para o veículo *{booking.VehiclePlate}* em {booking.ScheduledDate:dd/MM/yyyy} às {booking.ScheduledTime:HH\\:mm} foi cancelada e a vaga foi liberada.\n\n" +
                      "Caso queira realizar um novo agendamento no futuro, basta digitar *menu*. Até logo!";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> StartRescheduleFlowAsync(
        Guid tenantId,
        ChatbotConversationSession session,
        BookingSummaryDto booking,
        CancellationToken ct)
    {
        session.SetTargetBooking(booking.Id);
        session.SelectService(booking.ServiceId, booking.ServiceName);
        session.SelectVehicleSize(booking.VehicleSize, booking.EstimatedPrice);
        session.SetVehiclePlate(booking.VehiclePlate);
        session.MoveToStep(ChatbotStep.ReschedulingDate);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var dayAfter = today.AddDays(2);

        var message = $"🔄 *Remarcação de Agendamento (#{booking.Protocol})*\n\n" +
                      $"Serviço: *{booking.ServiceName}* ({booking.VehiclePlate})\n\n" +
                      "📅 *Para qual data deseja remarcar?*\n" +
                      $"1 - Hoje ({today:dd/MM})\n" +
                      $"2 - Amanhã ({tomorrow:dd/MM})\n" +
                      $"3 - {dayAfter:dd/MM}\n\n" +
                      "Ou digite a data no formato *DD/MM*:";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private static bool IsConfirmKeyword(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower is "confirmar" or "confirmo" or "confirmado" or "sim" or "ok" or "confirm";
    }

    private static bool IsRescheduleKeyword(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower is "remarcar" or "remarcação" or "remarcacao" or "trocar" or "mudar" or "reagendar";
    }

    private static bool IsCancelKeyword(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower is "cancelar" or "cancela" or "cancelamento" or "desmarcar" or "não vou" or "nao vou";
    }

    private async Task<Result> SendConfirmationSummaryAsync(Guid tenantId, ChatbotConversationSession session, CancellationToken ct)
    {
        var storeProfile = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        var storeName = storeProfile.Value?.TradeName ?? "Lavaway";
        var address = storeProfile.Value is not null
            ? $"{storeProfile.Value.Street}, {storeProfile.Value.City}"
            : "Consulte nosso endereço no perfil";

        var message = "📝 *Por favor, confira os dados do seu agendamento:*\n\n" +
                      $"• Estabelecimento: *{storeName}*\n" +
                      $"• Serviço: *{session.SelectedServiceName}*\n" +
                      $"• Porte: *{VehicleSizeConstants.GetDisplayName(session.SelectedVehicleSize)}*\n" +
                      $"• Valor estimado: *R$ {session.SelectedPrice:N2}*\n" +
                      $"• Data e Horário: *{session.SelectedDate:dd/MM/yyyy} às {session.SelectedTime:HH\\:mm}*\n" +
                      $"• Placa: *{session.VehiclePlate}*\n" +
                      $"• Local: *{address}*\n\n" +
                      "Digite:\n" +
                      "1️⃣ para *Confirmar o Agendamento*\n" +
                      "2️⃣ para *Cancelar*";

        return await SendBotReplyAsync(tenantId, session.CustomerPhone, message, ct);
    }

    private async Task<Result> SendBotReplyAsync(Guid tenantId, string recipientPhone, string text, CancellationToken ct)
    {
        var idempotencyKey = $"bot-{Guid.NewGuid():N}";
        var dispatchResult = await messageDispatcher.DispatchTextMessageAsync(tenantId, recipientPhone, text, idempotencyKey, ct);
        return dispatchResult.IsSuccess ? Result.Success() : Result.Failure(dispatchResult.Error!);
    }

    private static bool IsResetCommand(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower is "menu" or "sair" or "voltar" or "inicio" or "início" or "começar" or "comecar";
    }

    private static bool TryParseDate(string text, out DateOnly date)
    {
        date = default;
        var parts = text.Split('/', '-', '.');
        if (parts.Length != 2) return false;

        if (int.TryParse(parts[0], out var day) && int.TryParse(parts[1], out var month))
        {
            try
            {
                var year = DateTime.Today.Year;
                date = new DateOnly(year, month, day);
                if (date < DateOnly.FromDateTime(DateTime.Today).AddMonths(-1))
                {
                    date = date.AddYears(1);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
