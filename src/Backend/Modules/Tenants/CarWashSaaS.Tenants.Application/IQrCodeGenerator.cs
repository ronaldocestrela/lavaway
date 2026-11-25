namespace CarWashSaaS.Tenants.Application;

public interface IQrCodeGenerator
{
    string GenerateSvg(string content, int pixelsPerModule = 10);
    byte[] GeneratePng(string content, int pixelsPerModule = 10);
}
