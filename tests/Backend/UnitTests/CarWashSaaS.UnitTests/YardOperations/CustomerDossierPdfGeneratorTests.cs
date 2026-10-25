using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Infrastructure;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class CustomerDossierPdfGeneratorTests
{
    [Fact]
    public void GenerateDossierPdf_Should_ReturnValidPdfBytes_WhenModelIsValid()
    {
        var tenantId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();

        var dossier = new CustomerDossierDto(
            tenantId,
            DateTimeOffset.UtcNow,
            new CustomerProfileDossierDto(customerId, "Ana Maria", "11999998888", false, null),
            new List<VehicleDossierDto>
            {
                new(Guid.CreateVersion7(), "ABC1D23", "HatchSedan")
            },
            new List<WorkOrderDossierDto>
            {
                new(
                    Guid.CreateVersion7(),
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(-1).AddHours(2),
                    "ReadyForPickup",
                    120.00m,
                    true,
                    "Pix",
                    new List<WorkOrderItemDossierDto>
                    {
                        new("Lavagem Completa", 80.00m, 1, 80.00m),
                        new("Cera Cristalizadora", 40.00m, 1, 40.00m)
                    },
                    new WorkOrderSurveyDossierDto(5, "Excelente!", DateTimeOffset.UtcNow))
            },
            new WhatsAppHistoryDossierDto(
                true,
                DateTimeOffset.UtcNow.AddDays(-5),
                "Opt-in concedido no balcão",
                new List<OutboundMessageDossierDto>
                {
                    new(DateTimeOffset.UtcNow.AddDays(-1), "WorkOrderNotification", "Delivered")
                }),
            new DossierLegalNoticeDto(
                "Dossiê LGPD",
                "Art. 18, II e V",
                "Controlador: Lava Aê",
                "Retenção legal 5 anos",
                "privacidade@lavaway.com.br"));

        var storeProfile = new StoreProfileDto(
            Guid.CreateVersion7(),
            tenantId,
            "Lava-Jato Premium LTDA",
            "Lava-Jato Premium SP",
            "11.222.333/0001-44",
            "11988887777",
            "Av. Paulista, 1000",
            "Sao Paulo",
            "SP",
            "01310-100",
            null,
            null,
            null);

        var generator = new CustomerDossierPdfGenerator();
        var result = generator.GenerateDossierPdf(dossier, storeProfile);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Length > 100);

        // Header %PDF
        Assert.Equal(0x25, result.Value[0]); // %
        Assert.Equal(0x50, result.Value[1]); // P
        Assert.Equal(0x44, result.Value[2]); // D
        Assert.Equal(0x46, result.Value[3]); // F
    }

    [Fact]
    public void GenerateDossierPdf_Should_Fail_WhenTenantIdOrCustomerIdIsEmpty()
    {
        var invalidDossier = new CustomerDossierDto(
            Guid.Empty,
            DateTimeOffset.UtcNow,
            new CustomerProfileDossierDto(Guid.Empty, "Invalido", "", false, null),
            [],
            [],
            new WhatsAppHistoryDossierDto(false, null, null, []),
            new DossierLegalNoticeDto("", "", "", "", ""));

        var generator = new CustomerDossierPdfGenerator();
        var result = generator.GenerateDossierPdf(invalidDossier, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("dossier_pdf.invalid_data", result.Error!.Code);
    }
}
