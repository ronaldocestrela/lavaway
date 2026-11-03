namespace CarWashSaaS.Shared.Contracts;

public interface IAppUrlResolver
{
    string GetBaseUrl();
    string GetDisplayDomain();
}

