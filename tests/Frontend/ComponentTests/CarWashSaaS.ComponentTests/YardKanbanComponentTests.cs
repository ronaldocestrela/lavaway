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
}
