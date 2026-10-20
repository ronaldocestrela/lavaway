namespace CarWashSaaS.WhatsApp.Domain;

public enum ChatbotStep
{
    Greeting,
    Menu,
    ConfirmingCustomerName,
    CollectingCustomerName,
    SelectingService,
    SelectingVehicleSize,
    SelectingDate,
    SelectingTimeSlot,
    SelectingRegisteredVehicle,
    CollectingPlate,
    AwaitingConfirmation,
    AwaitingReminderAction,
    ReschedulingDate,
    ReschedulingTimeSlot,
    Completed
}
