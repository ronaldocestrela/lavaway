using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.Tenants;

public sealed class CampaignLinkApplicationServiceTests
{
    private sealed class FakeCampaignLinkRepository : ICampaignLinkRepository
    {
        public readonly List<CampaignLink> Items = [];
        public int IncrementCountCalls = 0;

        public Task<CampaignLink?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<CampaignLink?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Items.Any(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<CampaignLink>> ListAsync(string? searchTerm = null, bool? isActive = null, CancellationToken ct = default)
        {
            var query = Items.AsEnumerable();
            if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(x => x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || x.Slug.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IReadOnlyList<CampaignLink>>(query.ToList());
        }

        public Task AddAsync(CampaignLink campaign, CancellationToken ct = default)
        {
            Items.Add(campaign);
            return Task.CompletedTask;
        }

        public void Update(CampaignLink campaign) { }

        public Task<bool> IncrementClickCountAsync(Guid id, DateTimeOffset utcNow, CancellationToken ct = default)
        {
            IncrementCountCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeQrCodeGenerator : IQrCodeGenerator
    {
        public string GenerateSvg(string content, int pixelsPerModule = 10) => "<svg>qrcode</svg>";
        public byte[] GeneratePng(string content, int pixelsPerModule = 10) => [0x89, 0x50, 0x4E, 0x47];
    }

    private sealed class FakeUrlResolver : IAppUrlResolver
    {
        public string GetBaseUrl() => "https://app.lavaway.com.br";
        public string GetDisplayDomain() => "app.lavaway.com.br";
    }

    [Fact]
    public async Task CreateCampaignAsync_WhenSlugAlreadyExists_ShouldReturnConflict()
    {
        var repo = new FakeCampaignLinkRepository();
        var existing = CampaignLink.Create("Promo Natal", "promo-natal", "/", "ig", "cpc", "natal", null, null, null, Guid.NewGuid()).Value!;
        await repo.AddAsync(existing);

        var service = new CampaignLinkApplicationService(repo, new FakeQrCodeGenerator(), new FakeUrlResolver());

        var request = new CreateCampaignLinkRequest(
            "Promoção de Natal Nova",
            "promo-natal",
            "/",
            "meta",
            "feed",
            "natal",
            null,
            null,
            null);

        var result = await service.CreateCampaignAsync(request, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("campaign.slug_already_exists", result.Error.Code);
    }

    [Fact]
    public async Task CreateCampaignAsync_WhenValid_ShouldPersistAndReturnDto()
    {
        var repo = new FakeCampaignLinkRepository();
        var service = new CampaignLinkApplicationService(repo, new FakeQrCodeGenerator(), new FakeUrlResolver());

        var request = new CreateCampaignLinkRequest(
            "Promoção de Natal",
            "promo-natal",
            "/planos",
            "meta",
            "feed",
            "natal",
            null,
            null,
            null);

        var result = await service.CreateCampaignAsync(request, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("promo-natal", result.Value!.Slug);
        Assert.Equal("https://app.lavaway.com.br/c/promo-natal", result.Value.ShortUrl);
        Assert.Contains("utm_campaign=natal", result.Value.FullTrackingUrl);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task ResolveRedirectTargetAsync_WhenCampaignExists_ShouldIncrementClickAndReturnUrl()
    {
        var repo = new FakeCampaignLinkRepository();
        var campaign = CampaignLink.Create(
            "Feira SP",
            "feira-sp",
            "/cadastro",
            "totem",
            "qrcode",
            "feirasp",
            null,
            null,
            null,
            Guid.NewGuid()).Value!;
        await repo.AddAsync(campaign);

        var service = new CampaignLinkApplicationService(repo, new FakeQrCodeGenerator(), new FakeUrlResolver());

        var result = await service.ResolveRedirectTargetAsync("feira-sp");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("https://app.lavaway.com.br/cadastro?", result.Value);
        Assert.Contains("utm_source=totem", result.Value);
        Assert.Equal(1, campaign.ClickCount);
        Assert.Equal(1, repo.IncrementCountCalls);
    }

    [Fact]
    public async Task ResolveRedirectTargetAsync_WhenNotFound_ShouldReturnNotFound()
    {
        var repo = new FakeCampaignLinkRepository();
        var service = new CampaignLinkApplicationService(repo, new FakeQrCodeGenerator(), new FakeUrlResolver());

        var result = await service.ResolveRedirectTargetAsync("nao-existe");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }
}
