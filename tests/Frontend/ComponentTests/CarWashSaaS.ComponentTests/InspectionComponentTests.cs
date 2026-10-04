using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class InspectionComponentTests : BunitContext
{
    [Fact]
    public void VehicleInspectionDiagram_ShouldRenderViewsAndAllowSwitching()
    {
        var cut = Render<VehicleInspectionDiagram>(parameters => parameters
            .Add(p => p.Damages, [])
            .Add(p => p.IsReadOnly, false));

        Assert.Contains("Diagrama Interativo do Veículo", cut.Markup);
        Assert.Contains("Superior", cut.Markup);
        Assert.Contains("Lat. Esquerda", cut.Markup);
        Assert.Contains("Lat. Direita", cut.Markup);

        // Alterna para Lateral Esquerda
        var leftBtn = cut.FindAll("button.btn-view").First(b => b.TextContent.Contains("Lat. Esquerda"));
        leftBtn.Click();

        Assert.Contains("LATERAL ESQUERDA", cut.Markup);
    }

    [Fact]
    public void InspectionChecklistCard_ShouldRenderFuelPillsAndChecklistItems()
    {
        var items = new List<InspectionChecklistItemDto>
        {
            new(Guid.NewGuid(), "spare_tire", "Estepe", ChecklistItemStatus.Ok, null),
            new(Guid.NewGuid(), "wheel_wrench", "Chave de Roda", ChecklistItemStatus.Missing, "Não encontrado")
        };

        var cut = Render<InspectionChecklistCard>(parameters => parameters
            .Add(p => p.FuelLevel, "Half")
            .Add(p => p.OdometerKm, 45000)
            .Add(p => p.Items, items)
            .Add(p => p.IsReadOnly, false));

        Assert.Contains("Itens e Estado do Veículo", cut.Markup);
        Assert.Contains("Estepe", cut.Markup);
        Assert.Contains("Chave de Roda", cut.Markup);

        var activePill = cut.Find("button.btn-pill.active");
        Assert.Contains("1/2", activePill.TextContent);
    }

    [Fact]
    public void InspectionPhotoGallery_ShouldShowFourRequiredSlotsAndStatus()
    {
        var photos = new List<InspectionPhotoDto>
        {
            new(Guid.NewGuid(), InspectionPhotoCategory.Front, "front.jpg", "image/jpeg", 2048, null, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), InspectionPhotoCategory.Rear, "rear.jpg", "image/jpeg", 2048, null, DateTimeOffset.UtcNow)
        };

        var cut = Render<InspectionPhotoGallery>(parameters => parameters
            .Add(p => p.WorkOrderId, Guid.NewGuid())
            .Add(p => p.Photos, photos)
            .Add(p => p.IsReadOnly, false)
            .Add(p => p.PhotoBaseUrl, "https://api.test/"));

        Assert.Contains("Fotos Obrigatórias de Perímetro", cut.Markup);
        Assert.Contains("2/4 Fotos Obrigatórias", cut.Markup);
        Assert.Contains("Frente / Placa Dianteira", cut.Markup);
        Assert.Contains("Traseira / Placa Traseira", cut.Markup);
        Assert.Contains("Lateral Esquerda", cut.Markup);
        Assert.Contains("Lateral Direita", cut.Markup);
    }

    [Fact]
    public void WorkOrderCreatedModal_ShouldTriggerOnGoToInspectionWhenClicked()
    {
        Guid? clickedWoId = null;
        var wo = new WorkOrderDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Carlos Silva",
            Guid.NewGuid(),
            "ABC1D23",
            "Suv",
            "Waiting",
            120m,
            60,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(60),
            "Sem observações",
            []);

        var cut = Render<WorkOrderCreatedModal>(parameters => parameters
            .Add(p => p.WorkOrder, wo)
            .Add(p => p.OnGoToInspection, id => clickedWoId = id));

        var inspectionBtn = cut.Find("#btn-modal-go-inspection");
        inspectionBtn.Click();

        Assert.Equal(wo.Id, clickedWoId);
    }
}
