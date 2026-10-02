using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public sealed record CreateCustomerWithVehicleCommand(string Name, string Phone, string Plate, VehicleSize Size);
