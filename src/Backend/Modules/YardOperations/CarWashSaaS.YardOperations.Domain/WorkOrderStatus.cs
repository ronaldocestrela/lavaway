namespace CarWashSaaS.YardOperations.Domain;

public enum WorkOrderStatus
{
    Waiting,
    InWashing,
    Finishing,
    QualityControl,
    ReadyForPickup
}
