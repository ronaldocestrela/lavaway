using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class ReceptionComponentTests : BunitContext
{
    [Fact]
    public void CustomerMatchRow_ShouldRenderCustomerInfoAndVehicleCount()
    {
        var match = new CustomerVehicleMatchDto(
            Guid.NewGuid(),
            "Carlos Eduardo",
            "(11) 98765-4321",
            [
                new VehicleSummaryDto(Guid.NewGuid(), "ABC1D23", "HatchSedan"),
                new VehicleSummaryDto(Guid.NewGuid(), "XYZ9Z99", "Suv")
            ]);

        var cut = Render<CustomerMatchRow>(parameters => parameters
            .Add(p => p.Match, match)
            .Add(p => p.SelectedCustomerId, Guid.NewGuid()));

        Assert.Contains("Carlos Eduardo", cut.Markup);
        Assert.Contains("(11) 98765-4321", cut.Markup);
        Assert.Contains("02", cut.Markup);
        Assert.Contains("veículos", cut.Markup);
        Assert.Equal("C", cut.Find(".customer-initial").TextContent.Trim());
        Assert.DoesNotContain("selected", cut.Find("button").ClassList);
    }

    [Fact]
    public void CustomerMatchRow_ShouldApplySelectedClass_AndTriggerOnSelect()
    {
        var customerId = Guid.NewGuid();
        var match = new CustomerVehicleMatchDto(
            customerId,
            "Fernanda Lima",
            "(21) 99999-8888",
            [new VehicleSummaryDto(Guid.NewGuid(), "RIO2A18", "HatchSedan")]);

        Guid? selectedId = null;

        var cut = Render<CustomerMatchRow>(parameters => parameters
            .Add(p => p.Match, match)
            .Add(p => p.SelectedCustomerId, customerId)
            .Add(p => p.OnSelect, id => selectedId = id));

        Assert.Contains("selected", cut.Find("button").ClassList);

        cut.Find("button").Click();
        Assert.Equal(customerId, selectedId);
    }

    [Fact]
    public void CustomerVehicleCreateCard_ShouldRenderInitialValues_AndTriggerOnSubmit()
    {
        CreateCustomerWithVehicleRequest? submittedRequest = null;

        var cut = Render<CustomerVehicleCreateCard>(parameters => parameters
            .Add(p => p.Name, "Rodrigo Alves")
            .Add(p => p.Phone, "(11) 91234-5678")
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.Size, "Suv")
            .Add(p => p.OnSubmit, req => submittedRequest = req));

        Assert.Equal("Rodrigo Alves", cut.Find("#input-customer-name").GetAttribute("value"));
        Assert.Equal("(11) 91234-5678", cut.Find("#input-customer-phone").GetAttribute("value"));
        Assert.Equal("ABC1D23", cut.Find("#input-vehicle-plate").GetAttribute("value"));
        Assert.Equal("Suv", cut.Find("#select-vehicle-size").GetAttribute("value"));

        cut.Find("form").Submit();

        Assert.NotNull(submittedRequest);
        Assert.Equal("Rodrigo Alves", submittedRequest.Name);
        Assert.Equal("(11) 91234-5678", submittedRequest.Phone);
        Assert.Equal("ABC1D23", submittedRequest.Plate);
        Assert.Equal("Suv", submittedRequest.Size);
    }

    [Fact]
    public void CustomerVehicleCreateCard_ShouldTriggerOnCancel_WhenCancelClicked()
    {
        var cancelTriggered = false;

        var cut = Render<CustomerVehicleCreateCard>(parameters => parameters
            .Add(p => p.OnCancel, () => cancelTriggered = true));

        cut.Find("#btn-cancel-create").Click();
        Assert.True(cancelTriggered);
    }

    [Fact]
    public void CustomerVehicleCreateCard_ShouldDisableActions_WhenIsSavingIsTrue()
    {
        var cut = Render<CustomerVehicleCreateCard>(parameters => parameters
            .Add(p => p.IsSaving, true));

        Assert.NotNull(cut.Find("#btn-cancel-create").GetAttribute("disabled"));
        Assert.NotNull(cut.Find("#btn-submit-create").GetAttribute("disabled"));
        Assert.Contains("Salvando...", cut.Find("#btn-submit-create").TextContent);
    }

    [Fact]
    public void AddVehicleCard_ShouldRenderCustomerName_AndTriggerOnSubmit()
    {
        AddVehicleToCustomerRequest? submittedRequest = null;

        var cut = Render<AddVehicleCard>(parameters => parameters
            .Add(p => p.CustomerName, "Mariana Ramos")
            .Add(p => p.Plate, "BRA2E19")
            .Add(p => p.Size, "PickupVan")
            .Add(p => p.OnSubmit, req => submittedRequest = req));

        Assert.Contains("Mariana Ramos", cut.Markup);
        Assert.Equal("BRA2E19", cut.Find("#input-add-plate").GetAttribute("value"));
        Assert.Equal("PickupVan", cut.Find("#select-add-size").GetAttribute("value"));

        cut.Find("form").Submit();

        Assert.NotNull(submittedRequest);
        Assert.Equal("BRA2E19", submittedRequest.Plate);
        Assert.Equal("PickupVan", submittedRequest.Size);
    }

    [Fact]
    public void AddVehicleCard_ShouldTriggerOnCancel_WhenCancelClicked()
    {
        var cancelTriggered = false;

        var cut = Render<AddVehicleCard>(parameters => parameters
            .Add(p => p.CustomerName, "Mariana Ramos")
            .Add(p => p.OnCancel, () => cancelTriggered = true));

        cut.Find("#btn-cancel-add-vehicle").Click();
        Assert.True(cancelTriggered);
    }

    [Fact]
    public void AddVehicleCard_ShouldDisableActions_WhenIsSavingIsTrue()
    {
        var cut = Render<AddVehicleCard>(parameters => parameters
            .Add(p => p.CustomerName, "Mariana Ramos")
            .Add(p => p.IsSaving, true));

        Assert.NotNull(cut.Find("#btn-cancel-add-vehicle").GetAttribute("disabled"));
        Assert.NotNull(cut.Find("#btn-submit-add-vehicle").GetAttribute("disabled"));
        Assert.Contains("Salvando...", cut.Find("#btn-submit-add-vehicle").TextContent);
    }

    [Fact]
    public void ReceptionSessionState_ShouldMaintainSelection_AndNotifySubscribers()
    {
        var state = new ReceptionSessionState();
        var notifyCount = 0;
        state.OnChange += () => notifyCount++;

        var vehicle1 = new VehicleSummaryDto(Guid.NewGuid(), "ABC1D23", "HatchSedan");
        var vehicle2 = new VehicleSummaryDto(Guid.NewGuid(), "XYZ9Z99", "Suv");
        var customer = new CustomerVehicleMatchDto(
            Guid.NewGuid(),
            "Juliana Paes",
            "(11) 98888-7777",
            [vehicle1, vehicle2]);

        state.SelectCustomer(customer);
        Assert.Equal(customer.CustomerId, state.SelectedCustomer?.CustomerId);
        Assert.Equal(vehicle1.Id, state.SelectedVehicle?.Id);
        Assert.Equal(1, notifyCount);

        state.SelectVehicle(vehicle2.Id);
        Assert.Equal(vehicle2.Id, state.SelectedVehicle?.Id);
        Assert.Equal(2, notifyCount);

        state.ClearSelection();
        Assert.Null(state.SelectedCustomer);
        Assert.Null(state.SelectedVehicle);
        Assert.Equal(3, notifyCount);
    }
}
