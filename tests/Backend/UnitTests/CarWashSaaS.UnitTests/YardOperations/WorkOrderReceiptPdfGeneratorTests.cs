using System.Text;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Infrastructure;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class WorkOrderReceiptPdfGeneratorTests
{
    [Fact]
    public void GenerateReceiptPdf_Should_Create_Valid_Pdf_With_Complete_Order_And_Inspection()
    {
        var generator = new WorkOrderReceiptPdfGenerator();
        var model = new WorkOrderReceiptPdfModel(
            TenantId: Guid.NewGuid(),
            WorkOrderId: Guid.NewGuid(),
            StoreTradeName: "Lava Car Premium",
            StorePhone: "(11) 98765-4321",
            StoreAddress: "Av. Paulista, 1000 - SP",
            CustomerName: "Carlos Eduardo",
            CustomerPhone: "(11) 91234-5678",
            VehiclePlate: "BRA2E19",
            VehicleModel: "Honda Civic G10",
            VehicleSize: "Sedan Médio",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            EstimatedCompletionAtUtc: DateTimeOffset.UtcNow.AddMinutes(90),
            TotalAmount: 180.00m,
            Notes: "Cuidado com o retrovisor esquerdo.",
            Items:
            [
                new WorkOrderReceiptItemModel("Lavagem Completa + Cera", 1, 100.00m, 100.00m),
                new WorkOrderReceiptItemModel("Higienizacao de Ar Condicionado", 1, 80.00m, 80.00m)
            ],
            Inspection: new WorkOrderReceiptInspectionModel(
                OdometerKm: 45200,
                FuelLevel: "3/4",
                ChecklistItems:
                [
                    new WorkOrderReceiptChecklistItemModel("Estepe e Chave de Roda", "Ok", null),
                    new WorkOrderReceiptChecklistItemModel("Tapetes Originais", "Ok", "Personalizados"),
                    new WorkOrderReceiptChecklistItemModel("Antena do Teto", "Avaria", "Haste solta")
                ],
                Damages:
                [
                    new WorkOrderReceiptDamageModel("Arranhao", "Lateral Esquerda", "Leve", "Porta do motorista")
                ]));

        var result = generator.GenerateReceiptPdf(model);

        Assert.True(result.IsSuccess);
        var bytes = result.Value!;
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        // Validações estruturais de arquivo PDF
        var rawString = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", rawString);
        Assert.Contains("%%EOF", rawString);
        Assert.Contains("LAVA CAR PREMIUM", rawString);
        Assert.Contains("Carlos Eduardo", rawString);
        Assert.Contains("BRA2E19", rawString);
        Assert.Contains("Lavagem Completa", rawString);
        Assert.Contains("CHECKLIST DA VISTORIA", rawString);
        Assert.Contains("45.200 km", rawString);
    }

    [Fact]
    public void GenerateReceiptPdf_Should_Reject_Empty_Tenant_Or_WorkOrderId()
    {
        var generator = new WorkOrderReceiptPdfGenerator();
        var model = new WorkOrderReceiptPdfModel(
            TenantId: Guid.Empty,
            WorkOrderId: Guid.Empty,
            StoreTradeName: "Loja",
            StorePhone: null,
            StoreAddress: null,
            CustomerName: "Cliente",
            CustomerPhone: "11999999999",
            VehiclePlate: "ABC1234",
            VehicleModel: "Gol",
            VehicleSize: "Hatch",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            EstimatedCompletionAtUtc: DateTimeOffset.UtcNow.AddMinutes(30),
            TotalAmount: 50m,
            Notes: null,
            Items: [],
            Inspection: null);

        var result = generator.GenerateReceiptPdf(model);

        Assert.False(result.IsSuccess);
        Assert.Equal("receipt_pdf.invalid_data", result.Error!.Code);
    }
}
