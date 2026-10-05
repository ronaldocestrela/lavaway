using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class CustomerCommunicationPreferenceLookupService(
    ICustomerCommunicationPreferenceRepository preferenceRepository) : ICustomerCommunicationPreferenceLookup
{
    public async Task<Result<CustomerCommunicationPreferenceDto?>> GetPreferenceAsync(
        Guid tenantId,
        string phone,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(phone))
        {
            return Result<CustomerCommunicationPreferenceDto?>.Failure(new Error("preference.invalid_input", "Tenant e telefone são obrigatórios.", ErrorType.Validation));
        }

        var normalized = OutboundWhatsAppMessage.CleanPhoneNumber(phone);
        var pref = await preferenceRepository.GetByPhoneAsync(tenantId, normalized, ct);
        if (pref is null)
        {
            return Result<CustomerCommunicationPreferenceDto?>.Success(null);
        }

        return Result<CustomerCommunicationPreferenceDto?>.Success(ToDto(pref));
    }

    public async Task<Result<CustomerCommunicationPreferenceDto>> UpdatePreferenceAsync(
        Guid tenantId,
        string phone,
        bool isOptedIn,
        string? reason = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(phone))
        {
            return Result<CustomerCommunicationPreferenceDto>.Failure(new Error("preference.invalid_input", "Tenant e telefone são obrigatórios.", ErrorType.Validation));
        }

        var normalized = OutboundWhatsAppMessage.CleanPhoneNumber(phone);
        var pref = await preferenceRepository.GetByPhoneAsync(tenantId, normalized, ct);

        if (pref is null)
        {
            var createResult = CustomerCommunicationPreference.Create(tenantId, phone, isOptedIn, reason);
            if (!createResult.IsSuccess)
            {
                return Result<CustomerCommunicationPreferenceDto>.Failure(createResult.Error!);
            }

            pref = createResult.Value!;
            await preferenceRepository.AddAsync(pref, ct);
        }
        else
        {
            if (isOptedIn)
            {
                pref.OptIn();
            }
            else
            {
                pref.OptOut(reason ?? "Atualizado manualmente pela administração");
            }
        }

        await preferenceRepository.SaveChangesAsync(ct);
        return Result<CustomerCommunicationPreferenceDto>.Success(ToDto(pref));
    }

    public async Task<Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>> ListPreferencesAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>.Failure(new Error("preference.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var list = await preferenceRepository.ListPreferencesAsync(tenantId, ct);
        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<CustomerCommunicationPreferenceDto>>.Success(dtos);
    }

    private static CustomerCommunicationPreferenceDto ToDto(CustomerCommunicationPreference p) =>
        new(p.Id, p.TenantId, p.NormalizedPhone, p.IsOptedIn, p.OptedOutAtUtc, p.Reason, p.UpdatedAt);
}
