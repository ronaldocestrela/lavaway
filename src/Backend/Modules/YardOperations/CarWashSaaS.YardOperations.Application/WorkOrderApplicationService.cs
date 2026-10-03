using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed class WorkOrderApplicationService(
    IWorkOrderRepository workOrderRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IServiceRepository serviceRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<WorkOrderDto>> CreateAsync(
        Guid tenantId,
        CreateWorkOrderCommand command,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (command.CustomerId == Guid.Empty || command.VehicleId == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.owner.required", "Cliente e veículo são obrigatórios.", ErrorType.Validation));
        }

        if (command.Items is null || command.Items.Count == 0)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.items.required", "Pelo menos um serviço é obrigatório.", ErrorType.Validation));
        }

        if (command.Items.Any(i => i.ServiceId == Guid.Empty || i.Quantity <= 0))
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.item.invalid", "Serviço e quantidade válida são obrigatórios.", ErrorType.Validation));
        }

        if (command.Items.Select(i => i.ServiceId).Distinct().Count() != command.Items.Count)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.duplicate_service", "Não é permitido adicionar serviços duplicados na mesma ordem de serviço.", ErrorType.Conflict));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, command.CustomerId, ct);
        if (customer is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("customer.not_found", "Cliente não encontrado.", ErrorType.NotFound));
        }

        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, command.VehicleId, ct);
        if (vehicle is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("vehicle.not_found", "Veículo não encontrado.", ErrorType.NotFound));
        }

        if (vehicle.CustomerId != customer.Id)
        {
            return Result<WorkOrderDto>.Failure(new Error("vehicle.not_owned_by_customer", "O veículo informado não pertence a este cliente.", ErrorType.Conflict));
        }

        var workOrderItems = new List<WorkOrderItem>();
        foreach (var itemInput in command.Items)
        {
            var service = await serviceRepository.GetByIdAsync(tenantId, itemInput.ServiceId, ct);
            if (service is null)
            {
                return Result<WorkOrderDto>.Failure(new Error("service.not_found", $"Serviço '{itemInput.ServiceId}' não encontrado.", ErrorType.NotFound));
            }

            var matchingPrice = service.Prices.FirstOrDefault(p => p.VehicleSize == vehicle.Size);
            if (matchingPrice is null)
            {
                return Result<WorkOrderDto>.Failure(new Error(
                    "service.price_not_configured_for_size",
                    $"O serviço '{service.Name}' não possui preço configurado para o porte {vehicle.Size}.",
                    ErrorType.Validation));
            }

            var itemResult = WorkOrderItem.Create(
                tenantId,
                service.Id,
                service.Name,
                matchingPrice.Amount,
                matchingPrice.EstimatedDurationMinutes,
                itemInput.Quantity);

            if (!itemResult.IsSuccess)
            {
                return Result<WorkOrderDto>.Failure(itemResult.Error!);
            }

            workOrderItems.Add(itemResult.Value!);
        }

        var workOrderResult = WorkOrder.Create(
            tenantId,
            customer.Id,
            vehicle.Id,
            workOrderItems,
            command.Notes);

        if (!workOrderResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(workOrderResult.Error!);
        }

        var workOrder = workOrderResult.Value!;
        await workOrderRepository.AddAsync(workOrder, ct);
        var saveResult = await unitOfWork.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<WorkOrderDto>.Failure(saveResult.Error!);
        }

        return Result<WorkOrderDto>.Success(ToDto(workOrder, customer.Name, vehicle.Plate, vehicle.Size.ToString()));
    }

    public async Task<Result<WorkOrderDto>> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.id.required", "Tenant e ID da ordem de serviço são obrigatórios.", ErrorType.Validation));
        }

        var workOrder = await workOrderRepository.GetByIdAsync(tenantId, id, ct);
        if (workOrder is null)
        {
            return Result<WorkOrderDto>.Failure(new Error("work_order.not_found", "Ordem de serviço não encontrada.", ErrorType.NotFound));
        }

        var customer = await customerRepository.GetByIdAsync(tenantId, workOrder.CustomerId, ct);
        var vehicle = await vehicleRepository.GetByIdAsync(tenantId, workOrder.VehicleId, ct);

        var customerName = customer?.Name ?? "Cliente";
        var plate = vehicle?.Plate ?? string.Empty;
        var size = vehicle?.Size.ToString() ?? "HatchSedan";

        return Result<WorkOrderDto>.Success(ToDto(workOrder, customerName, plate, size));
    }

    public async Task<Result<IReadOnlyCollection<WorkOrderDto>>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<WorkOrderDto>>.Failure(new Error("tenant.required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var boundedLimit = Math.Clamp(limit, 1, 100);
        var orders = await workOrderRepository.ListRecentAsync(tenantId, boundedLimit, ct);

        var list = new List<WorkOrderDto>(orders.Count);
        foreach (var order in orders)
        {
            var customer = await customerRepository.GetByIdAsync(tenantId, order.CustomerId, ct);
            var vehicle = await vehicleRepository.GetByIdAsync(tenantId, order.VehicleId, ct);
            list.Add(ToDto(order, customer?.Name ?? "Cliente", vehicle?.Plate ?? string.Empty, vehicle?.Size.ToString() ?? "HatchSedan"));
        }

        return Result<IReadOnlyCollection<WorkOrderDto>>.Success(list);
    }

    private static WorkOrderDto ToDto(WorkOrder order, string customerName, string plate, string vehicleSize) =>
        new(
            order.Id,
            order.CustomerId,
            customerName,
            order.VehicleId,
            plate,
            vehicleSize,
            order.Status.ToString(),
            order.TotalAmount,
            order.EstimatedDurationMinutes,
            order.CreatedAtUtc,
            order.EstimatedCompletionAtUtc,
            order.Notes,
            order.Items.Select(item => new WorkOrderItemDto(
                item.Id,
                item.ServiceId,
                item.ServiceName,
                item.UnitPrice,
                item.EstimatedDurationMinutes,
                item.Quantity,
                item.TotalAmount,
                item.TotalDurationMinutes)).ToList());
}
