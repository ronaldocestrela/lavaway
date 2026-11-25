using System.Net.Http.Json;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Client.Core;

public sealed class PlatformCampaignsApiClient(HttpClient httpClient)
{
    public async Task<Result<IReadOnlyList<CampaignLinkDto>>> ListAsync(
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        try
        {
            var uri = "platform/campaigns";
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(searchTerm))
                queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
            if (isActive.HasValue)
                queryParams.Add($"isActive={isActive.Value}");

            if (queryParams.Count > 0)
                uri += $"?{string.Join("&", queryParams)}";

            var response = await httpClient.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: ct);
                return Result<IReadOnlyList<CampaignLinkDto>>.Failure(error ?? new Error("api.error", "Erro ao carregar campanhas.", ErrorType.Validation));
            }

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<CampaignLinkDto>>(cancellationToken: ct);
            return Result<IReadOnlyList<CampaignLinkDto>>.Success(items ?? []);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<CampaignLinkDto>>.Failure(new Error("http.error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<CampaignLinkDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"platform/campaigns/{id}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: ct);
                return Result<CampaignLinkDto>.Failure(error ?? new Error("api.error", "Erro ao obter campanha.", ErrorType.Validation));
            }

            var item = await response.Content.ReadFromJsonAsync<CampaignLinkDto>(cancellationToken: ct);
            return item is not null
                ? Result<CampaignLinkDto>.Success(item)
                : Result<CampaignLinkDto>.Failure(new Error("campaign.null", "Resposta vazia.", ErrorType.NotFound));
        }
        catch (Exception ex)
        {
            return Result<CampaignLinkDto>.Failure(new Error("http.error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<CampaignLinkDto>> CreateAsync(CreateCampaignLinkRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("platform/campaigns", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: ct);
                return Result<CampaignLinkDto>.Failure(error ?? new Error("api.error", "Erro ao criar campanha.", ErrorType.Validation));
            }

            var item = await response.Content.ReadFromJsonAsync<CampaignLinkDto>(cancellationToken: ct);
            return item is not null
                ? Result<CampaignLinkDto>.Success(item)
                : Result<CampaignLinkDto>.Failure(new Error("campaign.null", "Falha ao deserializar campanha criada.", ErrorType.Validation));
        }
        catch (Exception ex)
        {
            return Result<CampaignLinkDto>.Failure(new Error("http.error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<CampaignLinkDto>> UpdateAsync(Guid id, UpdateCampaignLinkRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"platform/campaigns/{id}", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: ct);
                return Result<CampaignLinkDto>.Failure(error ?? new Error("api.error", "Erro ao atualizar campanha.", ErrorType.Validation));
            }

            var item = await response.Content.ReadFromJsonAsync<CampaignLinkDto>(cancellationToken: ct);
            return item is not null
                ? Result<CampaignLinkDto>.Success(item)
                : Result<CampaignLinkDto>.Failure(new Error("campaign.null", "Falha ao deserializar campanha atualizada.", ErrorType.Validation));
        }
        catch (Exception ex)
        {
            return Result<CampaignLinkDto>.Failure(new Error("http.error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<CampaignLinkDto>> ToggleStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PatchAsJsonAsync($"platform/campaigns/{id}/status", new UpdateCampaignStatusRequest(isActive), ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: ct);
                return Result<CampaignLinkDto>.Failure(error ?? new Error("api.error", "Erro ao alterar status da campanha.", ErrorType.Validation));
            }

            var item = await response.Content.ReadFromJsonAsync<CampaignLinkDto>(cancellationToken: ct);
            return item is not null
                ? Result<CampaignLinkDto>.Success(item)
                : Result<CampaignLinkDto>.Failure(new Error("campaign.null", "Falha ao deserializar retorno.", ErrorType.Validation));
        }
        catch (Exception ex)
        {
            return Result<CampaignLinkDto>.Failure(new Error("http.error", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<CampaignQrCodePreviewDto>> GetQrCodePreviewAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"platform/campaigns/{id}/qrcode?format=svg", ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: ct);
                return Result<CampaignQrCodePreviewDto>.Failure(error ?? new Error("api.error", "Erro ao obter QR Code SVG.", ErrorType.Validation));
            }

            var item = await response.Content.ReadFromJsonAsync<CampaignQrCodePreviewDto>(cancellationToken: ct);
            return item is not null
                ? Result<CampaignQrCodePreviewDto>.Success(item)
                : Result<CampaignQrCodePreviewDto>.Failure(new Error("campaign.null", "Falha ao deserializar QR Code.", ErrorType.Validation));
        }
        catch (Exception ex)
        {
            return Result<CampaignQrCodePreviewDto>.Failure(new Error("http.error", ex.Message, ErrorType.Validation));
        }
    }

    public string GetQrCodeDownloadUrl(Guid id, string format = "svg")
    {
        var cleanBase = (httpClient.BaseAddress?.ToString() ?? string.Empty).TrimEnd('/');
        return $"{cleanBase}/platform/campaigns/{id}/qrcode?format={format}&download=true";
    }
}
