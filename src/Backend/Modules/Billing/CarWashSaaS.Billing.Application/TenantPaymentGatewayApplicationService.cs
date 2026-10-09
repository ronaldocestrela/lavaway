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
        var activeProvider = config?.Provider ?? PaymentGatewayProviderConstants.PagarMe;
        var pagarmeWebhookUrl = BuildWebhookUrl(baseUrl, tenantId, PaymentGatewayProviderConstants.PagarMe);
        var mercadopagoWebhookUrl = BuildWebhookUrl(baseUrl, tenantId, PaymentGatewayProviderConstants.MercadoPago);
        var activeWebhookUrl = BuildWebhookUrl(baseUrl, tenantId, activeProvider);

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
                MercadoPagoPublicKey: null,
                MercadoPagoAccessTokenMasked: null,
                HasMercadoPagoAccessToken: false,
                MercadoPagoWebhookSecretMasked: null,
                HasMercadoPagoWebhookSecret: false,
                IsActive: false,
                WebhookUrl: activeWebhookUrl,
                PagarMeWebhookUrl: pagarmeWebhookUrl,
                MercadoPagoWebhookUrl: mercadopagoWebhookUrl,
                LastTestedAtUtc: null,
                LastTestSuccess: null,
                LastTestMessage: null));
        }

        var decryptedPagarMeSecret = encryptor.Decrypt(config.PagarMeSecretKeyEncrypted ?? string.Empty);
        var decryptedPagarMeWebhookSecret = encryptor.Decrypt(config.PagarMeWebhookSecretEncrypted ?? string.Empty);
        var decryptedMpToken = encryptor.Decrypt(config.MercadoPagoAccessTokenEncrypted ?? string.Empty);
        var decryptedMpWebhookSecret = encryptor.Decrypt(config.MercadoPagoWebhookSecretEncrypted ?? string.Empty);

        var maskedPagarMeSecret = MaskKey(decryptedPagarMeSecret);
        var maskedPagarMeWebhookSecret = MaskKey(decryptedPagarMeWebhookSecret);
        var maskedMpToken = MaskKey(decryptedMpToken);
        var maskedMpWebhookSecret = MaskKey(decryptedMpWebhookSecret);

        return Result<TenantPaymentGatewayConfigDto>.Success(new TenantPaymentGatewayConfigDto(
            TenantId: tenantId,
            Provider: config.Provider,
            PagarMePublicKey: config.PagarMePublicKey,
            PagarMeSecretKeyMasked: maskedPagarMeSecret,
            HasSecretKey: !string.IsNullOrWhiteSpace(decryptedPagarMeSecret),
            PagarMeWebhookSecretMasked: maskedPagarMeWebhookSecret,
            HasWebhookSecret: !string.IsNullOrWhiteSpace(decryptedPagarMeWebhookSecret),
            MercadoPagoPublicKey: config.MercadoPagoPublicKey,
            MercadoPagoAccessTokenMasked: maskedMpToken,
            HasMercadoPagoAccessToken: !string.IsNullOrWhiteSpace(decryptedMpToken),
            MercadoPagoWebhookSecretMasked: maskedMpWebhookSecret,
            HasMercadoPagoWebhookSecret: !string.IsNullOrWhiteSpace(decryptedMpWebhookSecret),
            IsActive: config.IsActive,
            WebhookUrl: activeWebhookUrl,
            PagarMeWebhookUrl: pagarmeWebhookUrl,
            MercadoPagoWebhookUrl: mercadopagoWebhookUrl,
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

        string? encryptedPagarMeSecretKey = null;
        if (!string.IsNullOrWhiteSpace(request.PagarMeSecretKey) && !request.PagarMeSecretKey.Contains('•'))
        {
            encryptedPagarMeSecretKey = encryptor.Encrypt(request.PagarMeSecretKey.Trim());
        }

        string? encryptedPagarMeWebhookSecret = null;
        if (!string.IsNullOrWhiteSpace(request.PagarMeWebhookSecret) && !request.PagarMeWebhookSecret.Contains('•'))
        {
            encryptedPagarMeWebhookSecret = encryptor.Encrypt(request.PagarMeWebhookSecret.Trim());
        }

        string? encryptedMpAccessToken = null;
        if (!string.IsNullOrWhiteSpace(request.MercadoPagoAccessToken) && !request.MercadoPagoAccessToken.Contains('•'))
        {
            encryptedMpAccessToken = encryptor.Encrypt(request.MercadoPagoAccessToken.Trim());
        }

        string? encryptedMpWebhookSecret = null;
        if (!string.IsNullOrWhiteSpace(request.MercadoPagoWebhookSecret) && !request.MercadoPagoWebhookSecret.Contains('•'))
        {
            encryptedMpWebhookSecret = encryptor.Encrypt(request.MercadoPagoWebhookSecret.Trim());
        }

        if (config is null)
        {
            var createResult = TenantPaymentGatewayConfig.Create(
                tenantId,
                request.Provider,
                encryptedPagarMeSecretKey,
                request.PagarMePublicKey,
                encryptedPagarMeWebhookSecret,
                encryptedMpAccessToken,
                request.MercadoPagoPublicKey,
                encryptedMpWebhookSecret,
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
                encryptedPagarMeSecretKey ?? config.PagarMeSecretKeyEncrypted,
                request.PagarMePublicKey,
                encryptedPagarMeWebhookSecret ?? config.PagarMeWebhookSecretEncrypted,
                encryptedMpAccessToken ?? config.MercadoPagoAccessTokenEncrypted,
                request.MercadoPagoPublicKey,
                encryptedMpWebhookSecret ?? config.MercadoPagoWebhookSecretEncrypted,
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

        if (string.Equals(request.Provider, PaymentGatewayProviderConstants.PagarMe, StringComparison.OrdinalIgnoreCase))
        {
            string? secretKeyToTest = request.PagarMeSecretKey;
            if (string.IsNullOrWhiteSpace(secretKeyToTest) || secretKeyToTest.Contains('•'))
            {
                if (config is not null && !string.IsNullOrWhiteSpace(config.PagarMeSecretKeyEncrypted))
                {
                    secretKeyToTest = encryptor.Decrypt(config.PagarMeSecretKeyEncrypted);
                }
            }

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

        if (string.Equals(request.Provider, PaymentGatewayProviderConstants.MercadoPago, StringComparison.OrdinalIgnoreCase))
        {
            string? tokenToTest = request.MercadoPagoAccessToken;
            if (string.IsNullOrWhiteSpace(tokenToTest) || tokenToTest.Contains('•'))
            {
                if (config is not null && !string.IsNullOrWhiteSpace(config.MercadoPagoAccessTokenEncrypted))
                {
                    tokenToTest = encryptor.Decrypt(config.MercadoPagoAccessTokenEncrypted);
                }
            }

            if (string.IsNullOrWhiteSpace(tokenToTest))
            {
                return Result<TestTenantGatewayConnectionResultDto>.Failure(
                    new Error("mercadopago.token_required", "Informe um Access Token do Mercado Pago para realizar o teste.", ErrorType.Validation));
            }

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.mercadopago.com/users/me");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenToTest.Trim());

                var httpResponse = await httpClient.SendAsync(httpRequest, ct);
                bool success = httpResponse.IsSuccessStatusCode;
                string message;

                if (success)
                {
                    message = "Conexão com a API do Mercado Pago autenticada com sucesso! Access Token válido.";
                }
                else if (httpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    message = "Falha de autenticação: Access Token do Mercado Pago inválido ou expirado (401 Unauthorized).";
                }
                else
                {
                    message = $"A API do Mercado Pago retornou status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}).";
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
                var errorMsg = $"Erro ao conectar à API do Mercado Pago: {ex.Message}";
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
