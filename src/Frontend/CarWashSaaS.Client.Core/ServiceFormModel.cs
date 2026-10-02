using System.ComponentModel.DataAnnotations;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class ServicePriceFormItem
{
    public string VehicleSize { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class ServiceFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "O nome do serviço é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome do serviço deve ter no máximo 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A categoria é obrigatória.")]
    [StringLength(100, ErrorMessage = "A categoria deve ter no máximo 100 caracteres.")]
    public string Category { get; set; } = string.Empty;

    public List<ServicePriceFormItem> Prices { get; set; } = [];

    public static ServiceFormModel CreateDefault()
    {
        return new ServiceFormModel
        {
            Category = ServiceCategoryConstants.Ducha,
            Prices = VehicleSizeConstants.All.Select(size => new ServicePriceFormItem
            {
                VehicleSize = size,
                Amount = size switch
                {
                    VehicleSizeConstants.Motorcycle => 40m,
                    VehicleSizeConstants.HatchSedan => 60m,
                    VehicleSizeConstants.Suv => 80m,
                    VehicleSizeConstants.PickupVan => 100m,
                    _ => 50m
                },
                EstimatedDurationMinutes = size switch
                {
                    VehicleSizeConstants.Motorcycle => 30,
                    VehicleSizeConstants.HatchSedan => 45,
                    VehicleSizeConstants.Suv => 60,
                    VehicleSizeConstants.PickupVan => 75,
                    _ => 45
                },
                IsEnabled = true
            }).ToList()
        };
    }

    public static ServiceFormModel FromDto(ServiceDto dto)
    {
        var model = new ServiceFormModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Category = dto.Category,
            Prices = []
        };

        foreach (var size in VehicleSizeConstants.All)
        {
            var match = dto.Prices.FirstOrDefault(p => string.Equals(p.VehicleSize, size, StringComparison.OrdinalIgnoreCase));
            model.Prices.Add(new ServicePriceFormItem
            {
                VehicleSize = size,
                Amount = match?.Amount,
                EstimatedDurationMinutes = match?.EstimatedDurationMinutes,
                IsEnabled = match is not null
            });
        }

        return model;
    }

    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "O nome do serviço é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            return (false, "A categoria é obrigatória.");
        }

        var activePrices = Prices.Where(p => p.IsEnabled).ToList();
        if (activePrices.Count == 0)
        {
            return (false, "Configure ao menos um preço por porte de veículo.");
        }

        foreach (var price in activePrices)
        {
            if (!price.Amount.HasValue || price.Amount.Value <= 0)
            {
                return (false, $"Informe um valor válido (> 0) para o porte {VehicleSizeConstants.GetDisplayName(price.VehicleSize)}.");
            }

            if (!price.EstimatedDurationMinutes.HasValue || price.EstimatedDurationMinutes.Value <= 0)
            {
                return (false, $"Informe uma duração estimada válida (> 0 min) para o porte {VehicleSizeConstants.GetDisplayName(price.VehicleSize)}.");
            }
        }

        return (true, null);
    }

    public CreateServiceRequest ToCreateRequest()
    {
        var activePrices = Prices
            .Where(p => p.IsEnabled && p.Amount.HasValue && p.EstimatedDurationMinutes.HasValue)
            .Select(p => new ServicePriceDto(p.VehicleSize, p.Amount!.Value, p.EstimatedDurationMinutes!.Value))
            .ToList();

        return new CreateServiceRequest(Name.Trim(), Category.Trim(), activePrices);
    }

    public UpdateServiceRequest ToUpdateRequest()
    {
        var activePrices = Prices
            .Where(p => p.IsEnabled && p.Amount.HasValue && p.EstimatedDurationMinutes.HasValue)
            .Select(p => new ServicePriceDto(p.VehicleSize, p.Amount!.Value, p.EstimatedDurationMinutes!.Value))
            .ToList();

        return new UpdateServiceRequest(Name.Trim(), Category.Trim(), activePrices);
    }
}
