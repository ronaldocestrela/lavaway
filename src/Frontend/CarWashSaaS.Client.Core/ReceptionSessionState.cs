using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

/// <summary>
/// State container centralizado para reter a seleção de cliente e veículo na recepção,
/// garantindo transição reativa e desacoplada para a futura ordem de serviço (Fase 3.2).
/// </summary>
public sealed class ReceptionSessionState
{
    public CustomerVehicleMatchDto? SelectedCustomer { get; private set; }
    public VehicleSummaryDto? SelectedVehicle { get; private set; }

    public event Action? OnChange;

    public void SelectCustomer(CustomerVehicleMatchDto? customer)
    {
        SelectedCustomer = customer;
        if (customer is null)
        {
            SelectedVehicle = null;
        }
        else if (SelectedVehicle is null || !customer.Vehicles.Any(v => v.Id == SelectedVehicle.Id))
        {
            SelectedVehicle = customer.Vehicles.FirstOrDefault();
        }

        NotifyStateChanged();
    }

    public void SelectVehicle(Guid vehicleId)
    {
        if (SelectedCustomer is not null)
        {
            SelectedVehicle = SelectedCustomer.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
            NotifyStateChanged();
        }
    }

    public void ClearSelection()
    {
        SelectedCustomer = null;
        SelectedVehicle = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
