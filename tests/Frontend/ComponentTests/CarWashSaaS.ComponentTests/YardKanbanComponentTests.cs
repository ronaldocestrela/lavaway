using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class YardKanbanComponentTests : BunitContext
{
    private static WorkOrderKanbanCardDto CreateSampleCard(string status = "Waiting", string plate = "ABC1D23") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Lucas Mendes",
            Guid.NewGuid(),
            plate,
            "Suv",
            status,
            120m,
            45,
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(25),
            null,
            null,
            "Cuidado ao manobrar",
            ["Lavagem Completa", "Cera Protetora"],
            2,
            DateTimeOffset.UtcNow.AddMinutes(-20));

    [Fact]
    public void YardKanbanCard_ShouldRenderPlate_Customer_Services_AndTriggerAdvance()
    {
        var card = CreateSampleCard("Waiting", "ABC1D23");
        WorkOrderKanbanCardDto? advancedCard = null;

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card)
            .Add(p => p.OnAdvanceClicked, c => advancedCard = c));

        Assert.Contains("ABC-1D23", cut.Markup);
        Assert.Contains("Lucas Mendes", cut.Markup);
        Assert.Contains("Suv", cut.Markup);
        Assert.Contains("Lavagem Completa", cut.Markup);
        Assert.Contains("120,00", cut.Markup);
        Assert.Contains("Cuidado ao manobrar", cut.Markup);

        var advanceBtn = cut.Find("button.btn-advance");
        Assert.Contains("Iniciar Lavagem", advanceBtn.TextContent);
        advanceBtn.Click();

        Assert.NotNull(advancedCard);
        Assert.Equal(card.Id, advancedCard.Id);
    }

    [Fact]
    public void YardKanbanCard_InFinishingStatus_ShouldOfferRewindButton()
    {
        var card = CreateSampleCard("Finishing", "XYZ9Z99");
        WorkOrderKanbanCardDto? rewindCard = null;

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card)
            .Add(p => p.OnRewindClicked, c => rewindCard = c));

        var rewindBtn = cut.Find("button.btn-rewind");
        Assert.NotNull(rewindBtn);
        rewindBtn.Click();

        Assert.NotNull(rewindCard);
        Assert.Equal(card.Id, rewindCard.Id);
    }

    [Fact]
    public void YardKanbanColumn_ShouldRenderHeader_Count_AndCards()
    {
        var card1 = CreateSampleCard("InWashing", "AAA1111");
        var card2 = CreateSampleCard("InWashing", "BBB2222");

        var column = new YardKanbanColumnDto(
            "InWashing",
            "Em Lavagem",
            2,
            [card1, card2]);

        var cut = Render<YardKanbanColumn>(parameters => parameters
            .Add(p => p.Column, column));

        Assert.Contains("Em Lavagem", cut.Markup);
        Assert.Contains("2", cut.Markup);
        Assert.Contains("AAA-1111", cut.Markup);
        Assert.Contains("BBB-2222", cut.Markup);
    }

    [Fact]
    public void YardKanbanBoard_ShouldDisplayCapacityIndicator_AndColumns()
    {
        var card = CreateSampleCard("InWashing", "CAR1234");
        var columns = new List<YardKanbanColumnDto>
        {
            new("Waiting", "Aguardando", 0, []),
            new("InWashing", "Em Lavagem", 1, [card]),
            new("Finishing", "Secagem / Acabamento", 0, []),
            new("QualityControl", "Controle de Qualidade", 0, []),
            new("ReadyForPickup", "Pronto para Retirada", 0, [])
        };

        var board = new YardKanbanBoardDto(columns, 1, 6, 1);

        var cut = Render<YardKanbanBoard>(parameters => parameters
            .Add(p => p.Board, board)
            .Add(p => p.IsLiveConnected, true));

        // Capacity
        Assert.Contains("CAPACIDADE DE BOXES", cut.Markup);
        Assert.Contains("1", cut.Markup);
        Assert.Contains("6", cut.Markup);
        Assert.Contains("TEMPO REAL", cut.Markup);

        // Columns
        Assert.Contains("Aguardando", cut.Markup);
        Assert.Contains("Em Lavagem", cut.Markup);
        Assert.Contains("Secagem / Acabamento", cut.Markup);
        Assert.Contains("Controle de Qualidade", cut.Markup);
        Assert.Contains("Pronto para Retirada", cut.Markup);
        Assert.Contains("CAR-1234", cut.Markup);
    }

    [Fact]
    public void YardKanbanBoard_ShouldNotThrow_WhenBoardIsNull()
    {
        var cut = Render<YardKanbanBoard>(parameters => parameters
            .Add(p => p.Board, null));

        Assert.NotNull(cut.Markup);
    }

    [Fact]
    public void WorkOrderHistoryDrawer_ShouldRenderTimelineWhenOpen()
    {
        var orderId = Guid.NewGuid();
        var history = new List<WorkOrderStatusHistoryDto>
        {
            new(Guid.NewGuid(), null, "Waiting", DateTimeOffset.UtcNow.AddMinutes(-30), null, null, "Check-in realizado"),
            new(Guid.NewGuid(), "Waiting", "InWashing", DateTimeOffset.UtcNow.AddMinutes(-20), Guid.NewGuid(), "Pedro Lavador", null),
            new(Guid.NewGuid(), "InWashing", "Finishing", DateTimeOffset.UtcNow.AddMinutes(-5), Guid.NewGuid(), "Lucas Secador", "Lavagem OK")
        };

        var closedClicked = false;
        var cut = Render<WorkOrderHistoryDrawer>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.WorkOrderId, orderId)
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.History, history)
            .Add(p => p.OnClose, () => closedClicked = true));

        Assert.Contains("Trilha Operacional", cut.Markup);
        Assert.Contains("ABC1D23", cut.Markup);
        Assert.Contains("Aguardando", cut.Markup);
        Assert.Contains("Em Lavagem", cut.Markup);
        Assert.Contains("Pedro Lavador", cut.Markup);
        Assert.Contains("Secagem / Acabamento", cut.Markup);
        Assert.Contains("Lucas Secador", cut.Markup);
        Assert.Contains("Lavagem OK", cut.Markup);

        cut.Find("button.btn-close-drawer").Click();
        Assert.True(closedClicked);
    }

    [Fact]
    public void YardKanbanCard_InReadyForPickupStatus_ShouldOfferNotifyReadyButton_AndTriggerCallback()
    {
        var card = CreateSampleCard("ReadyForPickup", "ABC1D23");
        WorkOrderKanbanCardDto? notifiedCard = null;

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card)
            .Add(p => p.OnNotifyReadyClicked, c => notifiedCard = c));

        var notifyBtn = cut.Find("button.btn-notify-ready");
        Assert.NotNull(notifyBtn);
        Assert.Contains("Notificar", notifyBtn.TextContent);
        Assert.Contains("LIBERADO", cut.Markup);

        notifyBtn.Click();
        Assert.NotNull(notifiedCard);
        Assert.Equal(card.Id, notifiedCard.Id);
    }

    [Fact]
    public void YardKanbanCard_InReadyForPickupStatus_WithoutPickup_ShouldOfferPickupButton_AndTriggerCallback()
    {
        var card = CreateSampleCard("ReadyForPickup", "ABC1D23");
        WorkOrderKanbanCardDto? pickedUpCard = null;

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card)
            .Add(p => p.OnPickupClicked, c => pickedUpCard = c));

        var pickupBtn = cut.Find("button.btn-pickup");
        Assert.NotNull(pickupBtn);
        Assert.Contains("Retirada", pickupBtn.TextContent);

        pickupBtn.Click();
        Assert.NotNull(pickedUpCard);
        Assert.Equal(card.Id, pickedUpCard.Id);
    }

    [Fact]
    public void YardKanbanCard_WhenAlreadyPickedUp_ShouldRenderPickupBadge_AndSurveyStatus()
    {
        var card = new WorkOrderKanbanCardDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Lucas Mendes",
            Guid.NewGuid(),
            "ABC1D23",
            "Suv",
            "ReadyForPickup",
            120m,
            45,
            DateTimeOffset.UtcNow.AddMinutes(-60),
            DateTimeOffset.UtcNow.AddMinutes(-30),
            null,
            null,
            null,
            ["Lavagem Completa"],
            1,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(-10),
            5);

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card));

        Assert.Contains("Retirado", cut.Markup);
        Assert.Contains("⭐ 5/5", cut.Markup);
        Assert.Empty(cut.FindAll("button.btn-pickup"));
    }

    [Fact]
    public void YardKanbanCard_WhenPaid_ShouldRenderPaidBadge_AndPixPaidButton()
    {
        var card = new WorkOrderKanbanCardDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Fernanda Lima",
            Guid.NewGuid(),
            "XYZ1A23",
            "Suv",
            "InWashing",
            180m,
            60,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow.AddMinutes(30),
            null,
            null,
            null,
            ["Polimento"],
            1,
            DateTimeOffset.UtcNow,
            IsPaid: true,
            PaidAtUtc: DateTimeOffset.UtcNow,
            PaymentMethod: "Pix");

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card));

        Assert.Contains("💳 Pago", cut.Markup);
        Assert.Contains("✅ Pix Pago", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".payment-badge-paid"));
        Assert.NotEmpty(cut.FindAll(".btn-pix-settled"));
    }

    [Fact]
    public void YardKanbanBoard_ShouldExcludeCards_WhenPickedUpAtUtcIsSet()
    {
        var readyCard = CreateSampleCard("ReadyForPickup", "ABC1234");
        var pickedUpCard = new WorkOrderKanbanCardDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Cliente Entregue",
            Guid.NewGuid(),
            "XYZ9999",
            "Suv",
            "ReadyForPickup",
            100m,
            30,
            DateTimeOffset.UtcNow.AddMinutes(-50),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            null,
            null,
            null,
            ["Ducha"],
            1,
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(-10));

        var columns = new List<YardKanbanColumnDto>
        {
            new("Waiting", "Aguardando", 0, []),
            new("InWashing", "Em Lavagem", 0, []),
            new("Finishing", "Secagem / Acabamento", 0, []),
            new("QualityControl", "Controle de Qualidade", 0, []),
            new("ReadyForPickup", "Pronto para Retirada", 2, [readyCard, pickedUpCard])
        };

        var board = new YardKanbanBoardDto(columns, 1, 6, 0);

        var cut = Render<YardKanbanBoard>(parameters => parameters
            .Add(p => p.Board, board));

        Assert.Contains("ABC-1234", cut.Markup);
        Assert.DoesNotContain("XYZ-9999", cut.Markup);
    }
}

