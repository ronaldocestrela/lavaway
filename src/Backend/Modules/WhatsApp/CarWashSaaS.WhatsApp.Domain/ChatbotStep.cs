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
    SelectingBookingToManage,
    AwaitingReminderAction,
    ReschedulingDate,
    ReschedulingTimeSlot,
    Completed
}
