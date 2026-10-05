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
    AwaitingReminderAction,
    ReschedulingDate,
    ReschedulingTimeSlot,
    Completed
}
