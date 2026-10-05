using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class CustomersComponentTests : BunitContext
{
    [Fact]
    public void EditCustomerModal_ShouldRenderCustomerInfo_AndTriggerSave()
    {
        var saved = false;
        var name = "Carlos Eduardo";
        var phone = "(11) 98765-4321";

        var cut = Render<EditCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Name, name)
            .Add(p => p.Phone, phone)
            .Add(p => p.OnSave, EventCallback.Factory.Create<UpdateCustomerRequest>(this, req => saved = true)));

        Assert.Contains("Editar Cliente", cut.Markup);
        Assert.Contains("Carlos Eduardo", cut.Find("#customer-name").GetAttribute("value"));
        Assert.Contains("(11) 98765-4321", cut.Find("#customer-phone").GetAttribute("value"));

        var saveBtn = cut.Find("#btn-save-customer");
        Assert.False(saveBtn.HasAttribute("disabled"));

        saveBtn.Click();
        Assert.True(saved);
    }

    [Fact]
    public void EditCustomerModal_ShouldDisableSave_WhenNameOrPhoneIsEmpty()
    {
        var cut = Render<EditCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Name, "")
            .Add(p => p.Phone, ""));

        var saveBtn = cut.Find("#btn-save-customer");
        Assert.True(saveBtn.HasAttribute("disabled"));
    }

    [Fact]
    public void EditVehicleModal_ShouldRenderPlateAndSize_AndTriggerSave()
    {
        var saved = false;
        var plate = "ABC1D23";
        var size = VehicleSizeConstants.Suv;

        var cut = Render<EditVehicleModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.VehicleId, Guid.NewGuid())
            .Add(p => p.Plate, plate)
            .Add(p => p.Size, size)
            .Add(p => p.OnSave, EventCallback.Factory.Create(this, () => saved = true)));

        Assert.Contains("Editar Veículo", cut.Markup);
        Assert.Contains("ABC1D23", cut.Find("#vehicle-plate").GetAttribute("value"));
        Assert.Contains("ABC-1D23", cut.Find(".plate-preview").TextContent);

        var saveBtn = cut.Find("#btn-save-vehicle");
        Assert.False(saveBtn.HasAttribute("disabled"));

        saveBtn.Click();
        Assert.True(saved);
    }

    [Fact]
    public void EditVehicleModal_ShouldSelectNewSize_WhenSizeCardClicked()
    {
        string? selectedSize = null;

        var cut = Render<EditVehicleModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.VehicleId, Guid.NewGuid())
            .Add(p => p.Plate, "ABC1D23")
            .Add(p => p.Size, VehicleSizeConstants.HatchSedan)
            .Add(p => p.SizeChanged, EventCallback.Factory.Create<string>(this, s => selectedSize = s)));

        var suvCard = cut.FindAll(".size-card").First(c => c.TextContent.Contains("SUV"));
        suvCard.Click();

        Assert.Equal(VehicleSizeConstants.Suv, selectedSize);
    }

    [Fact]
    public void CreateCustomerModal_ShouldTriggerSave_WhenValid()
    {
        var saved = false;

        var cut = Render<CreateCustomerModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.CustomerName, "Mariana Ramos")
            .Add(p => p.Phone, "11988887777")
            .Add(p => p.Plate, "XYZ9Z99")
            .Add(p => p.Size, VehicleSizeConstants.PickupVan)
            .Add(p => p.OnSave, EventCallback.Factory.Create<CreateCustomerWithVehicleRequest>(this, req => saved = true)));

        var saveBtn = cut.Find("#btn-create-customer");
        Assert.False(saveBtn.HasAttribute("disabled"));

        saveBtn.Click();
        Assert.True(saved);
    }
}
