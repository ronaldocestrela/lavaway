using System.Net.Http.Json;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class SaasBillingApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // 1. Visão do Lojista
    public async Task<Result<TenantSubscriptionOverviewDto>> GetTenantOverviewAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("settings/subscription", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TenantSubscriptionOverviewDto>(JsonOptions, ct);
                return data is not null
                    ? Result<TenantSubscriptionOverviewDto>.Success(data)
                    : Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<TenantSubscriptionOverviewDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<IReadOnlyList<SaasPlanDto>>> ListPlansAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("settings/subscription/plans", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<SaasPlanDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<IReadOnlyList<SaasPlanDto>>.Success(data)
                    : Result<IReadOnlyList<SaasPlanDto>>.Success([]);
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<IReadOnlyList<SaasPlanDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SaasPlanDto>>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<IReadOnlyList<SaasPlanDto>>> ListPublicPlansAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("public/saas-plans", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<SaasPlanDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<IReadOnlyList<SaasPlanDto>>.Success(data)
                    : Result<IReadOnlyList<SaasPlanDto>>.Success([]);
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<IReadOnlyList<SaasPlanDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SaasPlanDto>>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> ChangePlanAsync(SaasPlanTier targetTier, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("settings/subscription/change-plan", new ChangePlanRequest(targetTier), JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TenantSubscriptionOverviewDto>(JsonOptions, ct);
                return data is not null
                    ? Result<TenantSubscriptionOverviewDto>.Success(data)
                    : Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<TenantSubscriptionOverviewDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<StripeCheckoutSessionDto>> CreateCheckoutSessionAsync(
        SaasPlanTier targetTier,
        string? successUrl = null,
        string? cancelUrl = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new CreateStripeCheckoutSessionRequest(targetTier, successUrl, cancelUrl);
            using var response = await httpClient.PostAsJsonAsync("settings/subscription/checkout-session", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<StripeCheckoutSessionDto>(JsonOptions, ct);
                return data is not null
                    ? Result<StripeCheckoutSessionDto>.Success(data)
                    : Result<StripeCheckoutSessionDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<StripeCheckoutSessionDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<StripeCheckoutSessionDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> SyncCheckoutSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new SyncStripeCheckoutSessionRequest(sessionId);
            using var response = await httpClient.PostAsJsonAsync("settings/subscription/checkout-session/sync", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TenantSubscriptionOverviewDto>(JsonOptions, ct);
                return data is not null
                    ? Result<TenantSubscriptionOverviewDto>.Success(data)
                    : Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<TenantSubscriptionOverviewDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<StripeCustomerPortalSessionDto>> CreateCustomerPortalSessionAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsync("settings/subscription/portal-session", null, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<StripeCustomerPortalSessionDto>(JsonOptions, ct);
                return data is not null
                    ? Result<StripeCustomerPortalSessionDto>.Success(data)
                    : Result<StripeCustomerPortalSessionDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<StripeCustomerPortalSessionDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<StripeCustomerPortalSessionDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> SettleInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync($"settings/subscription/invoices/{invoiceId}/settle", new SettleInvoiceRequest(), JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TenantSubscriptionOverviewDto>(JsonOptions, ct);
                return data is not null
                    ? Result<TenantSubscriptionOverviewDto>.Success(data)
                    : Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<TenantSubscriptionOverviewDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    // 2. Visão do Backoffice da Plataforma
    public async Task<Result<IReadOnlyList<TenantSubscriptionOverviewDto>>> ListAllSubscriptionsForPlatformAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("platform/billing/subscriptions", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<TenantSubscriptionOverviewDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<IReadOnlyList<TenantSubscriptionOverviewDto>>.Success(data)
                    : Result<IReadOnlyList<TenantSubscriptionOverviewDto>>.Success([]);
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<IReadOnlyList<TenantSubscriptionOverviewDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TenantSubscriptionOverviewDto>>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<TenantSubscriptionOverviewDto>> AdminOverridePlanAsync(
        Guid tenantId,
        AdminOverridePlanRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync($"platform/billing/subscriptions/{tenantId}/override", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TenantSubscriptionOverviewDto>(JsonOptions, ct);
                return data is not null
                    ? Result<TenantSubscriptionOverviewDto>.Success(data)
                    : Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<TenantSubscriptionOverviewDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<TenantSubscriptionOverviewDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<IReadOnlyList<SaasPlanDto>>> ListAllPlansForPlatformAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("platform/billing/plans", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<SaasPlanDto>>(JsonOptions, ct);
                return data is not null
                    ? Result<IReadOnlyList<SaasPlanDto>>.Success(data)
                    : Result<IReadOnlyList<SaasPlanDto>>.Success([]);
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<IReadOnlyList<SaasPlanDto>>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SaasPlanDto>>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<SaasPlanDto>> UpdatePlanAsync(
        SaasPlanTier tier,
        UpdateSaasPlanRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PutAsJsonAsync($"platform/billing/plans/{tier}", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<SaasPlanDto>(JsonOptions, ct);
                return data is not null
                    ? Result<SaasPlanDto>.Success(data)
                    : Result<SaasPlanDto>.Failure(new Error("saas_billing.empty_response", "Resposta vazia do servidor.", ErrorType.Validation));
            }

            var error = await ReadErrorAsync(response, ct);
            return Result<SaasPlanDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<SaasPlanDto>.Failure(new Error("saas_billing.network_error", ex.Message, ErrorType.Validation));
        }
    }

    private static async Task<Error> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("code", out var codeProp) && root.TryGetProperty("description", out var descProp))
                {
                    return new Error(codeProp.GetString() ?? "error", descProp.GetString() ?? "Erro desconhecido", ErrorType.Validation);
                }
            }
        }
        catch
        {
            // fallback
        }

        return new Error("saas_billing.http_error", $"Falha na requisição: {(int)response.StatusCode} {response.ReasonPhrase}", ErrorType.Validation);
    }
}
