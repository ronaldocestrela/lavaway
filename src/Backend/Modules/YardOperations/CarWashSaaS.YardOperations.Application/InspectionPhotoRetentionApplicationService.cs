using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.YardOperations.Application;

public sealed class InspectionPhotoRetentionApplicationService(
    IVehicleInspectionRepository inspectionRepository,
    ITenantObjectStorage storage,
    IUnitOfWork unitOfWork,
    ITenantStoreProfileLookup storeProfileLookup,
    ILogger<InspectionPhotoRetentionApplicationService> logger)
{
    public const int DefaultRetentionDays = 90;
    public const int MaxRetentionDays = 365;
    public const int MinRetentionDays = 1;

    public async Task<Result<int>> PurgeExpiredInspectionPhotosAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<int>.Failure(new Error("retention.tenant.required", "Tenant é obrigatório para execução do expurgo.", ErrorType.Validation));
        }

        var retentionDays = DefaultRetentionDays;
        var profileResult = await storeProfileLookup.GetProfileAsync(tenantId, ct);
        if (profileResult.IsSuccess && profileResult.Value is not null)
        {
            var configuredDays = profileResult.Value.InspectionPhotoRetentionDays;
            retentionDays = Math.Clamp(configuredDays, MinRetentionDays, MaxRetentionDays);
        }

        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var eligibleInspections = await inspectionRepository.GetInspectionsWithPhotosEligibleForPurgeAsync(
            tenantId,
            cutoffUtc,
            limit: 100,
            ct);

        if (eligibleInspections.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var totalPurged = 0;
        var purgeTimestamp = DateTimeOffset.UtcNow;

        foreach (var inspection in eligibleInspections)
        {
            var unpurgedPhotos = inspection.Photos.Where(p => !p.IsPurged).ToList();
            if (unpurgedPhotos.Count == 0)
            {
                continue;
            }

            foreach (var photo in unpurgedPhotos)
            {
                try
                {
                    await storage.DeleteAsync(tenantId, "inspections", photo.FileName, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao remover binário físico da foto {PhotoId} ({FileName}) no storage para o tenant {TenantId}.", photo.Id, photo.FileName, tenantId);
                }

                inspection.PurgePhoto(photo.Id, purgeTimestamp, "LGPD Art. 16 - Expurgo por tempo limite de retenção");
                totalPurged++;
            }

            inspectionRepository.Update(inspection);
        }

        if (totalPurged > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation("Expurgo LGPD L3.1 concluído: {Count} foto(s) de vistoria expurgada(s) para o tenant {TenantId} (Retenção: {Days} dias, Cutoff: {Cutoff:O}).",
                totalPurged, tenantId, retentionDays, cutoffUtc);
        }

        return Result<int>.Success(totalPurged);
    }
}
