using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class WorkOrderComponentTests : BunitContext
{
    [Fact]
    public void CheckinCustomerHeader_ShouldRenderCustomerInfoAndPlate_AndAllowChangingSize()
    {
        string? changedSize = null;
        var cut = Render<CheckinCustomerHeader>(parameters => parameters
            .Add(p => p.CustomerName, "Guilherme Santos")
            .Add(p => p.Phone, "(11) 98888-7777")
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.SelectedVehicleSize, "HatchSedan")
            .Add(p => p.OnVehicleSizeChanged, size => changedSize = size));

        Assert.Contains("Guilherme Santos", cut.Markup);
        Assert.Contains("(11) 98888-7777", cut.Markup);
        Assert.Contains("ABC-1D23", cut.Markup);

        // Click SUV pill
        var suvButton = cut.FindAll("button.size-pill").First(b => b.TextContent.Contains("SUV"));
        suvButton.Click();

        Assert.Equal("Suv", changedSize);
    }

    [Fact]
    public void ServiceSelectorCard_ShouldRenderAvailableServicesForVehicleSize_AndToggleSelection()
    {
        var serviceId = Guid.NewGuid();
        var service = new ServiceDto(
            serviceId,
            "Lavagem Completa",
            "Lavagem",
            [
                new ServicePriceDto("HatchSedan", 60m, 35),
                new ServicePriceDto("Suv", 80m, 45)
            ]);

        (ServiceDto Service, int Quantity)? changedItem = null;
        var selectedDict = new Dictionary<Guid, int>();

        var cut = Render<ServiceSelectorCard>(parameters => parameters
            .Add(p => p.Services, [service])
            .Add(p => p.VehicleSize, "Suv")
            .Add(p => p.SelectedServices, selectedDict)
            .Add(p => p.OnServiceQuantityChanged, item => changedItem = item));

        Assert.Contains("Lavagem Completa", cut.Markup);
        Assert.Contains("80,00", cut.Markup);
        Assert.Contains("45 min", cut.Markup);

        // Click on service card to select
        var card = cut.Find(".service-card");
        card.Click();

        Assert.NotNull(changedItem);
        Assert.Equal(serviceId, changedItem.Value.Service.Id);
        Assert.Equal(1, changedItem.Value.Quantity);
    }

    [Fact]
    public void WorkOrderSummaryCard_ShouldCalculateTotalsAndForecast_AndTriggerSubmit()
    {
        var service1 = new ServiceDto(Guid.NewGuid(), "Lavagem", "Lavagem", [new ServicePriceDto("Suv", 80m, 40)]);
        var service2 = new ServiceDto(Guid.NewGuid(), "Cera", "Acabamento", [new ServicePriceDto("Suv", 40m, 20)]);

        var items = new List<(ServiceDto Service, decimal UnitPrice, int DurationMinutes, int Quantity)>
        {
            (service1, 80m, 40, 1),
            (service2, 40m, 20, 2)
        };

        var submitClicked = false;
        var notesValue = "Atenção nas rodas";

        var cut = Render<WorkOrderSummaryCard>(parameters => parameters
            .Add(p => p.SelectedItems, items)
            .Add(p => p.Notes, notesValue)
            .Add(p => p.OnSubmit, () => submitClicked = true));

        // 80 * 1 + 40 * 2 = 160
        Assert.Contains("160,00", cut.Markup);
        // 40 * 1 + 20 * 2 = 80 min = 1h 20min
        Assert.Contains("1h 20min", cut.Markup);
        Assert.Contains("PREVISÃO DE ENTREGA", cut.Markup);
        Assert.Contains("Atenção nas rodas", cut.Markup);

        var submitBtn = cut.Find("#btn-submit-workorder");
        Assert.False(submitBtn.HasAttribute("disabled"));
        submitBtn.Click();

        Assert.True(submitClicked);
    }

    [Fact]
    public void WorkOrderCreatedModal_ShouldDisplaySummaryAndTriggerNavigation()
    {
        var newReceptionClicked = false;
        var workOrder = new WorkOrderDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Mariana Souza",
            Guid.NewGuid(),
            "XYZ9Z99",
            "Suv",
            "Waiting",
            120m,
            60,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(60),
            "Cuidado ao manobrar",
            []);

        Services.AddSingleton(new WorkOrderApiClient(new HttpClient { BaseAddress = new Uri("http://localhost/") }));

        var cut = Render<WorkOrderCreatedModal>(parameters => parameters
            .Add(p => p.WorkOrder, workOrder)
            .Add(p => p.OnNewReception, () => newReceptionClicked = true));

        Assert.Contains("Ordem de Serviço Aberta", cut.Markup);
        Assert.Contains("Mariana Souza", cut.Markup);
        Assert.Contains("XYZ-9Z99", cut.Markup);
        Assert.Contains("120,00", cut.Markup);
        Assert.Contains("1h", cut.Markup);
        Assert.Contains("Cuidado ao manobrar", cut.Markup);
        Assert.NotNull(cut.Find("#btn-modal-download-pdf"));
        Assert.NotNull(cut.Find("#btn-modal-send-receipt-whatsapp"));

        cut.Find("#btn-modal-new-reception").Click();
        Assert.True(newReceptionClicked);
    }
}
