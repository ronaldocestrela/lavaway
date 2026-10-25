using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public interface ICustomerDossierPdfGenerator
{
    Result<byte[]> GenerateDossierPdf(CustomerDossierDto dossier, StoreProfileDto? storeProfile);
}
