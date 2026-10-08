using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Application;

public sealed class TenantPaymentGatewayApplicationService(
    ITenantPaymentGatewayConfigRepository configRepository,
    IPaymentCredentialsEncryptor encryptor,
    HttpClient httpClient,
    ILogger<TenantPaymentGatewayApplicationService>? logger = null)
{
    public async Task<Result<TenantPaymentGatewayConfigDto>> GetConfigAsync(
        Guid tenantId,
        string baseUrl,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantPaymentGatewayConfigDto>.Failure(
                new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var config = await configRepository.GetByTenantIdAsync(tenantId, ct);
        var webhookUrl = BuildWebhookUrl(baseUrl, tenantId, config?.Provider ?? PaymentGatewayProviderConstants.PagarMe);

        if (config is null)
        {
            return Result<TenantPaymentGatewayConfigDto>.Success(new TenantPaymentGatewayConfigDto(
                TenantId: tenantId,
                Provider: PaymentGatewayProviderConstants.PagarMe,
                PagarMePublicKey: null,
                PagarMeSecretKeyMasked: null,
                HasSecretKey: false,
                PagarMeWebhookSecretMasked: null,
                HasWebhookSecret: false,
                IsActive: false,
                WebhookUrl: webhookUrl,
                LastTestedAtUtc: null,
                LastTestSuccess: null,
                LastTestMessage: null));
        }

        var decryptedSecret = encryptor.Decrypt(config.PagarMeSecretKeyEncrypted ?? string.Empty);
        var decryptedWebhookSecret = encryptor.Decrypt(config.PagarMeWebhookSecretEncrypted ?? string.Empty);

        var maskedSecret = MaskKey(decryptedSecret);
        var maskedWebhookSecret = MaskKey(decryptedWebhookSecret);

        return Result<TenantPaymentGatewayConfigDto>.Success(new TenantPaymentGatewayConfigDto(
            TenantId: tenantId,
            Provider: config.Provider,
            PagarMePublicKey: config.PagarMePublicKey,
            PagarMeSecretKeyMasked: maskedSecret,
            HasSecretKey: !string.IsNullOrWhiteSpace(decryptedSecret),
            PagarMeWebhookSecretMasked: maskedWebhookSecret,
            HasWebhookSecret: !string.IsNullOrWhiteSpace(decryptedWebhookSecret),
            IsActive: config.IsActive,
            WebhookUrl: webhookUrl,
            LastTestedAtUtc: config.LastTestedAtUtc,
            LastTestSuccess: config.LastTestSuccess,
            LastTestMessage: config.LastTestMessage));
    }

    public async Task<Result<TenantPaymentGatewayConfigDto>> SaveConfigAsync(
        Guid tenantId,
        SaveTenantPaymentGatewayConfigRequest request,
        string baseUrl,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantPaymentGatewayConfigDto>.Failure(
                new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        if (!PaymentGatewayProviderConstants.IsValid(request.Provider))
        {
            return Result<TenantPaymentGatewayConfigDto>.Failure(
                new Error("billing.invalid_provider", $"Provedor de pagamento '{request.Provider}' é inválido.", ErrorType.Validation));
        }

        var config = await configRepository.GetByTenantIdAsync(tenantId, ct);

        string? encryptedSecretKey = null;
        if (!string.IsNullOrWhiteSpace(request.PagarMeSecretKey) && !request.PagarMeSecretKey.Contains('•'))
        {
            encryptedSecretKey = encryptor.Encrypt(request.PagarMeSecretKey.Trim());
        }

        string? encryptedWebhookSecret = null;
        if (!string.IsNullOrWhiteSpace(request.PagarMeWebhookSecret) && !request.PagarMeWebhookSecret.Contains('•'))
        {
            encryptedWebhookSecret = encryptor.Encrypt(request.PagarMeWebhookSecret.Trim());
        }

        if (config is null)
        {
            var createResult = TenantPaymentGatewayConfig.Create(
                tenantId,
                request.Provider,
                encryptedSecretKey,
                request.PagarMePublicKey,
                encryptedWebhookSecret,
                request.IsActive);

            if (!createResult.IsSuccess || createResult.Value is null)
            {
                return Result<TenantPaymentGatewayConfigDto>.Failure(createResult.Error!);
            }

            config = createResult.Value;
            await configRepository.AddAsync(config, ct);
        }
        else
        {
            var updateResult = config.Update(
                request.Provider,
                encryptedSecretKey ?? config.PagarMeSecretKeyEncrypted,
                request.PagarMePublicKey,
                encryptedWebhookSecret ?? config.PagarMeWebhookSecretEncrypted,
                request.IsActive);

            if (!updateResult.IsSuccess)
            {
                return Result<TenantPaymentGatewayConfigDto>.Failure(updateResult.Error!);
            }

            configRepository.Update(config);
        }

        await configRepository.SaveChangesAsync(ct);
        return await GetConfigAsync(tenantId, baseUrl, ct);
    }

    public async Task<Result<TestTenantGatewayConnectionResultDto>> TestConnectionAsync(
        Guid tenantId,
        TestTenantGatewayConnectionRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TestTenantGatewayConnectionResultDto>.Failure(
                new Error("billing.tenant_required", "Tenant é obrigatório.", ErrorType.Validation));
        }

        var config = await configRepository.GetByTenantIdAsync(tenantId, ct);

        string? secretKeyToTest = request.PagarMeSecretKey;
        if (string.IsNullOrWhiteSpace(secretKeyToTest) || secretKeyToTest.Contains('•'))
        {
            if (config is not null && !string.IsNullOrWhiteSpace(config.PagarMeSecretKeyEncrypted))
            {
                secretKeyToTest = encryptor.Decrypt(config.PagarMeSecretKeyEncrypted);
            }
        }

        if (string.Equals(request.Provider, PaymentGatewayProviderConstants.PagarMe, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(secretKeyToTest))
            {
                return Result<TestTenantGatewayConnectionResultDto>.Failure(
                    new Error("pagarme.secret_required", "Informe uma chave secreta da Pagar.me para realizar o teste.", ErrorType.Validation));
            }

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.pagar.me/core/v5/customers?page=1&size=1");
                var authValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{secretKeyToTest.Trim()}:"));
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                var httpResponse = await httpClient.SendAsync(httpRequest, ct);
                bool success = httpResponse.IsSuccessStatusCode;
                string message;

                if (success)
                {
                    message = "Conexão com a Pagar.me v5 autenticada com sucesso! Chave de API válida.";
                }
                else if (httpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    message = "Falha de autenticação: chave secreta inválida (401 Unauthorized).";
                }
                else
                {
                    message = $"A API da Pagar.me retornou status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}).";
                }

                if (config is not null)
                {
                    config.RecordTestResult(success, message);
                    configRepository.Update(config);
                    await configRepository.SaveChangesAsync(ct);
                }

                return Result<TestTenantGatewayConnectionResultDto>.Success(new TestTenantGatewayConnectionResultDto(
                    Success: success,
                    Message: message,
                    TestedAtUtc: DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                var errorMsg = $"Erro ao conectar à API da Pagar.me: {ex.Message}";
                if (config is not null)
                {
                    config.RecordTestResult(false, errorMsg);
                    configRepository.Update(config);
                    await configRepository.SaveChangesAsync(ct);
                }

                return Result<TestTenantGatewayConnectionResultDto>.Success(new TestTenantGatewayConnectionResultDto(
                    Success: false,
                    Message: errorMsg,
                    TestedAtUtc: DateTimeOffset.UtcNow));
            }
        }

        if (string.Equals(request.Provider, PaymentGatewayProviderConstants.Simulated, StringComparison.OrdinalIgnoreCase))
        {
            var simResult = new TestTenantGatewayConnectionResultDto(
                Success: true,
                Message: "Ambiente Simulado ativo e pronto para gerar cobranças de teste.",
                TestedAtUtc: DateTimeOffset.UtcNow);

            if (config is not null)
            {
                config.RecordTestResult(true, simResult.Message);
                configRepository.Update(config);
                await configRepository.SaveChangesAsync(ct);
            }

            return Result<TestTenantGatewayConnectionResultDto>.Success(simResult);
        }

        return Result<TestTenantGatewayConnectionResultDto>.Success(new TestTenantGatewayConnectionResultDto(
            Success: true,
            Message: $"Provedor {request.Provider} validado.",
            TestedAtUtc: DateTimeOffset.UtcNow));
    }

    private static string BuildWebhookUrl(string baseUrl, Guid tenantId, string provider)
    {
        var cleanBase = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.lavaway.com.br" : baseUrl.TrimEnd('/');
        var providerRoute = string.Equals(provider, PaymentGatewayProviderConstants.PagarMe, StringComparison.OrdinalIgnoreCase) ? "pagarme" : "mercadopago";
        return $"{cleanBase}/billing/webhooks/{providerRoute}/{tenantId}";
    }

    private static string? MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (key.Length <= 8)
        {
            return "••••••••";
        }

        var prefixLen = Math.Min(7, key.Length / 3);
        var suffixLen = Math.Min(4, key.Length / 3);
        var prefix = key[..prefixLen];
        var suffix = key[^suffixLen..];
        return $"{prefix}••••••••{suffix}";
    }
}
