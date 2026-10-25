using Bunit;
using Bunit.TestDoubles;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web.Pages;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void CustomersPage_ShouldRenderAnonymizedBadgeAndHideEdit_WhenCustomerIsAnonymized()
    {
        var customerId = Guid.NewGuid();
        var anonymizedCustomer = new CustomerVehicleMatchDto(
            customerId,
            "Cliente Anonimizado (LGPD)",
            "",
            [],
            true,
            DateTimeOffset.UtcNow);

        SetupCustomersPageServices([anonymizedCustomer]);

        var cut = Render<CustomersPage>();

        // Badge na lista e nos detalhes
        Assert.Contains("Anonimizado - LGPD", cut.Markup);
        var detailBadge = cut.Find("#badge-anonymized-detail");
        Assert.NotNull(detailBadge);
        Assert.Contains("Anonimizado - LGPD", detailBadge.TextContent);

        // Banner informativo da LGPD
        var notice = cut.Find("#anonymized-banner-notice");
        Assert.NotNull(notice);
        Assert.Contains("Art. 18, VI da LGPD", notice.TextContent);

        // Botão Exportar Dossiê deve existir
        var exportBtn = cut.Find("#btn-export-dossier");
        Assert.NotNull(exportBtn);

        // Botões de Editar e Anonimizar NÃO devem existir para cliente já anonimizado
        Assert.Empty(cut.FindAll("#btn-edit-customer"));
        Assert.Empty(cut.FindAll("#btn-open-anonymize-customer"));
    }

    [Fact]
    public void CustomersPage_ShouldRenderExportAndAnonymizeButtons_WhenCustomerIsActive()
    {
        var customerId = Guid.NewGuid();
        var activeCustomer = new CustomerVehicleMatchDto(
            customerId,
            "Carlos Eduardo",
            "11988887777",
            [new VehicleSummaryDto(Guid.NewGuid(), "ABC1D23", "HatchSedan")],
            false,
            null);

        SetupCustomersPageServices([activeCustomer]);

        var cut = Render<CustomersPage>();

        // Não deve ter badge de anonimizado
        Assert.Empty(cut.FindAll("#badge-anonymized-detail"));
        Assert.Empty(cut.FindAll("#anonymized-banner-notice"));

        // Botão Exportar Dossiê presente
        var exportBtn = cut.Find("#btn-export-dossier");
        Assert.NotNull(exportBtn);

        // Botões Editar Dados e Anonimizar Titular presentes
        var editBtn = cut.Find("#btn-edit-customer");
        Assert.NotNull(editBtn);

        var anonymizeBtn = cut.Find("#btn-open-anonymize-customer");
        Assert.NotNull(anonymizeBtn);

        // Ao clicar no botão de anonimizar, o modal abre
        anonymizeBtn.Click();
        Assert.Contains("LGPD — ARTIGO 18, INCISO VI", cut.Markup);
        Assert.Contains("Confirmar Anonimização Irreversível", cut.Markup);
    }

    private void SetupCustomersPageServices(CustomerVehicleMatchDto[] customers)
    {
        this.AddAuthorization().SetAuthorized("admin@lavaway.com.br").SetRoles("Administrator");
        Services.AddSingleton<CarWashSaaS.Client.Core.IToastService>(new CarWashSaaS.Client.Core.ToastService());
        Services.AddSingleton<ReceptionSessionState>();

        var handler = new FakeCustomerHttpMessageHandler(customers);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.lavaway.test/") };
        Services.AddSingleton(new CustomerVehicleApiClient(httpClient));
    }

    private sealed class FakeCustomerHttpMessageHandler(CustomerVehicleMatchDto[] customers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(customers);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
