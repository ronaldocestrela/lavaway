using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class CustomerDataExportService(
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IWorkOrderRepository workOrderRepository,
    ICustomerWhatsAppHistoryLookup? whatsAppHistoryLookup = null,
    IAuditTrailService? auditTrailService = null,
    ITenantStoreProfileLookup? storeProfileLookup = null,
    ICustomerDossierPdfGenerator? pdfGenerator = null)
{
    private const string LegalNoticeTitle = "Dossiê de Dados do Titular — LGPD (Art. 18, II e V)";
    private const string LegalBasis = "Art. 7º, V (Execução de Contrato) e Art. 16, I (Cumprimento de Obrigação Legal e Fiscal) da Lei Federal nº 13.709/2018 (LGPD)";
    private const string ControllerRole = "O estabelecimento credenciado atua como Controlador dos dados cadastrais e de veículos. O Lava Aê atua como Operador de tecnologia.";
    private const string RetentionPolicy = "Dados cadastrais e fotos de vistoria observam ciclo de vida de serviço; registros de Ordens de Serviço e transações financeiras são mantidos pelo prazo regulamentar de 5 anos (Código Civil e Código de Defesa do Consumidor).";
    private const string DpoEmail = "privacidade@lavaway.com.br";

    public async Task<Result<CustomerDossierDto>> ExportDossierAsync(
        Guid tenantId,
        Guid customerId,
        Guid? operatorId = null,
        string? operatorEmail = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<CustomerDossierDto>.Failure(new Error("customer.tenant.required", "Tenant is required.", ErrorType.Validation));
        }

        if (customerId == Guid.Empty)
        {
            return Result<CustomerDossierDto>.Failure(new Error("customer.id.required", "Customer ID is required.", ErrorType.Validation));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, customerId, ct);
        if (customer is null)
        {
            return Result<CustomerDossierDto>.Failure(new Error("customer.not_found", "Customer was not found.", ErrorType.NotFound));
        }

        // 1. Veículos associados
        var vehicles = await vehicleRepository.GetVehiclesByCustomerIdAsync(tenantId, customerId, ct);
        var vehicleDtos = vehicles.Select(v => new VehicleDossierDto(
            v.Id,
            v.Plate,
            v.Size.ToString())).ToList();

        // 2. Ordens de Serviço e Avaliações (CSAT)
        var workOrders = await workOrderRepository.GetByCustomerIdAsync(tenantId, customerId, ct);
        var workOrderDtos = workOrders
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(w => new WorkOrderDossierDto(
                w.Id,
                w.CreatedAtUtc,
                w.PickedUpAtUtc,
                w.Status.ToString(),
                w.TotalAmount,
                w.IsPaid,
                w.PaymentMethod,
                w.Items.Select(i => new WorkOrderItemDossierDto(
                    i.ServiceName,
                    i.UnitPrice,
                    i.Quantity,
                    i.TotalAmount)).ToList(),
                w.SurveyRating.HasValue
                    ? new WorkOrderSurveyDossierDto(w.SurveyRating, w.SurveyFeedback, w.SurveyRespondedAtUtc)
                    : null)).ToList();

        // 3. Histórico de comunicação WhatsApp (Preferências e Logs)
        WhatsAppHistoryDossierDto whatsAppHistory;
        if (!customer.IsAnonymized && whatsAppHistoryLookup is not null && !string.IsNullOrWhiteSpace(customer.Phone))
        {
            var whatsAppResult = await whatsAppHistoryLookup.GetCustomerWhatsAppHistoryAsync(tenantId, customer.Phone, ct);
            whatsAppHistory = whatsAppResult.IsSuccess && whatsAppResult.Value is not null
                ? whatsAppResult.Value
                : new WhatsAppHistoryDossierDto(false, null, null, []);
        }
        else
        {
            whatsAppHistory = new WhatsAppHistoryDossierDto(false, null, null, []);
        }

        var legalNotice = new DossierLegalNoticeDto(
            LegalNoticeTitle,
            LegalBasis,
            ControllerRole,
            RetentionPolicy,
            DpoEmail);

        var profile = new CustomerProfileDossierDto(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.IsAnonymized,
            customer.AnonymizedAtUtc);

        var dossier = new CustomerDossierDto(
            tenantId,
            DateTimeOffset.UtcNow,
            profile,
            vehicleDtos,
            workOrderDtos,
            whatsAppHistory,
            legalNotice);

        // 4. Registro de auditoria administrativa
        if (auditTrailService is not null && operatorId.HasValue && operatorId.Value != Guid.Empty)
        {
            var auditDetails = JsonSerializer.Serialize(new
            {
                CustomerId = customerId,
                CustomerName = customer.Name,
                IsAnonymized = customer.IsAnonymized,
                VehiclesCount = vehicleDtos.Count,
                WorkOrdersCount = workOrderDtos.Count,
                ExportedAtUtc = dossier.GeneratedAtUtc
            });

            await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                operatorId.Value,
                string.IsNullOrWhiteSpace(operatorEmail) ? "system@lavaway.com.br" : operatorEmail,
                "Administrator",
                "Shop",
                PlatformActionConstants.CustomerDataExported,
                PlatformTargetTypeConstants.Customer,
                customerId.ToString(),
                tenantId,
                ipAddress,
                userAgent,
                auditDetails), ct);
        }

        return Result<CustomerDossierDto>.Success(dossier);
    }

    public async Task<Result<byte[]>> ExportDossierPdfAsync(
        Guid tenantId,
        Guid customerId,
        Guid? operatorId = null,
        string? operatorEmail = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        if (pdfGenerator is null)
        {
            return Result<byte[]>.Failure(new Error("pdf_generator.unavailable", "PDF generator service is not configured.", ErrorType.Validation));
        }

        var dossierResult = await ExportDossierAsync(tenantId, customerId, operatorId, operatorEmail, ipAddress, userAgent, ct);
        if (!dossierResult.IsSuccess)
        {
            return Result<byte[]>.Failure(dossierResult.Error!);
        }

        StoreProfileDto? storeProfile = null;
        if (storeProfileLookup is not null)
        {
            var storeProfileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
            if (storeProfileResult.IsSuccess)
            {
                storeProfile = storeProfileResult.Value;
            }
        }

        return pdfGenerator.GenerateDossierPdf(dossierResult.Value!, storeProfile);
    }
}
