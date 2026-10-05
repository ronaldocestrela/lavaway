namespace CarWashSaaS.WhatsApp.Domain;

public enum ChatbotStep
{
    Greeting,
    Menu,
    SelectingService,
    SelectingVehicleSize,
    SelectingDate,
    SelectingTimeSlot,
    CollectingPlate,
    AwaitingConfirmation,
    Completed
}
