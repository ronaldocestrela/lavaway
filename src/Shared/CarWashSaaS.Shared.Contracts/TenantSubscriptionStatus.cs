namespace CarWashSaaS.Shared.Contracts;

public enum TenantSubscriptionStatus
{
    Trial = 0,
    Active = 1,
    GracePeriod = 2,
    Delinquent = 3,
    Canceled = 4
}
