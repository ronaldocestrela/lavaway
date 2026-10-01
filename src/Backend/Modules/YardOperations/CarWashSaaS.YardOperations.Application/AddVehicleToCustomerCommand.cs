using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed record AddVehicleToCustomerCommand(string Plate, VehicleSize Size);