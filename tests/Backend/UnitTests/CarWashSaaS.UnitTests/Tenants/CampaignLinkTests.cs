using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.Tenants;

public sealed class CampaignLinkTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var result = CampaignLink.Create(
            name: "Campanha Black Friday",
            slug: "black-friday-2026",
            destinationPath: "/authentication/register",
            utmSource: "instagram",
            utmMedium: "stories",
            utmCampaign: "bf2026",
            utmTerm: "lavajato",
            utmContent: "card1",
            expiresAtUtc: null,
            createdByUserId: Guid.NewGuid());

        Assert.True(result.IsSuccess);
        var campaign = result.Value!;
        Assert.NotEqual(Guid.Empty, campaign.Id);
        Assert.Equal("Campanha Black Friday", campaign.Name);
        Assert.Equal("black-friday-2026", campaign.Slug);
        Assert.Equal("/authentication/register", campaign.DestinationPath);
        Assert.Equal("instagram", campaign.UtmSource);
        Assert.Equal("stories", campaign.UtmMedium);
        Assert.Equal("bf2026", campaign.UtmCampaign);
        Assert.Equal("lavajato", campaign.UtmTerm);
        Assert.Equal("card1", campaign.UtmContent);
        Assert.True(campaign.IsActive);
        Assert.Equal(0, campaign.ClickCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldFail(string? invalidName)
    {
        var result = CampaignLink.Create(
            name: invalidName!,
            slug: "bf2026",
            destinationPath: "/",
            utmSource: "ig",
            utmMedium: "cpc",
            utmCampaign: "bf",
            utmTerm: null,
            utmContent: null,
            expiresAtUtc: null,
            createdByUserId: Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("campaign.invalid_name", result.Error.Code);
    }

    [Theory]
    [InlineData("inv@lid!slug")]
    [InlineData("slug com espaco")]
    [InlineData("slug_underscore")]
    [InlineData("slug--")]
    [InlineData("-slug")]
    public void Create_WithInvalidSlug_ShouldFail(string invalidSlug)
    {
        var result = CampaignLink.Create(
            name: "Campanha Teste",
            slug: invalidSlug,
            destinationPath: "/",
            utmSource: "ig",
            utmMedium: "cpc",
            utmCampaign: "bf",
            utmTerm: null,
            utmContent: null,
            expiresAtUtc: null,
            createdByUserId: Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("campaign.invalid_slug", result.Error.Code);
    }

    [Theory]
    [InlineData("https://externo.com")]
    [InlineData("http://evil.com")]
    [InlineData("//phishing.com")]
    [InlineData("sem-barra-inicial")]
    public void Create_WithUnsafeDestinationPath_ShouldFail(string unsafePath)
    {
        var result = CampaignLink.Create(
            name: "Campanha Teste",
            slug: "slug-ok",
            destinationPath: unsafePath,
            utmSource: "ig",
            utmMedium: "cpc",
            utmCampaign: "bf",
            utmTerm: null,
            utmContent: null,
            expiresAtUtc: null,
            createdByUserId: Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("campaign.unsafe_destination", result.Error.Code);
    }

    [Fact]
    public void BuildTrackingQueryString_ShouldGenerateExpectedQueryParameters()
    {
        var campaign = CampaignLink.Create(
            name: "Feira Auto",
            slug: "feira-auto",
            destinationPath: "/planos",
            utmSource: "google ads",
            utmMedium: "banner display",
            utmCampaign: "campanha sp",
            utmTerm: "estética automotiva",
            utmContent: "btn azul",
            expiresAtUtc: null,
            createdByUserId: Guid.NewGuid()).Value!;

        var fullUrl = campaign.BuildFullTrackingUrl("https://app.lavaway.com.br");

        Assert.StartsWith("https://app.lavaway.com.br/planos?", fullUrl);
        Assert.Contains("utm_source=google%20ads", fullUrl);
        Assert.Contains("utm_medium=banner%20display", fullUrl);
        Assert.Contains("utm_campaign=campanha%20sp", fullUrl);
        Assert.Contains("utm_term=est%C3%A9tica%20automotiva", fullUrl);
        Assert.Contains("utm_content=btn%20azul", fullUrl);
    }

    [Fact]
    public void RegisterClick_WhenActiveAndNotExpired_ShouldIncrementCount()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = CampaignLink.Create(
            name: "Teste",
            slug: "teste-click",
            destinationPath: "/",
            utmSource: "ig",
            utmMedium: "cpc",
            utmCampaign: "bf",
            utmTerm: null,
            utmContent: null,
            expiresAtUtc: now.AddDays(1),
            createdByUserId: Guid.NewGuid()).Value!;

        var result = campaign.RegisterClick(now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, campaign.ClickCount);
        Assert.NotNull(campaign.UpdatedAtUtc);
    }

    [Fact]
    public void RegisterClick_WhenInactive_ShouldFail()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = CampaignLink.Create(
            name: "Teste Inativo",
            slug: "teste-inativo",
            destinationPath: "/",
            utmSource: "ig",
            utmMedium: "cpc",
            utmCampaign: "bf",
            utmTerm: null,
            utmContent: null,
            expiresAtUtc: null,
            createdByUserId: Guid.NewGuid()).Value!;

        campaign.SetActive(false);
        var result = campaign.RegisterClick(now);

        Assert.False(result.IsSuccess);
        Assert.Equal("campaign.inactive", result.Error!.Code);
        Assert.Equal(0, campaign.ClickCount);
    }

    [Fact]
    public void RegisterClick_WhenExpired_ShouldFail()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = CampaignLink.Create(
            name: "Teste Expirado",
            slug: "teste-expirado",
            destinationPath: "/",
            utmSource: "ig",
            utmMedium: "cpc",
            utmCampaign: "bf",
            utmTerm: null,
            utmContent: null,
            expiresAtUtc: now.AddDays(-1),
            createdByUserId: Guid.NewGuid()).Value!;

        var result = campaign.RegisterClick(now);

        Assert.False(result.IsSuccess);
        Assert.Equal("campaign.expired", result.Error!.Code);
        Assert.Equal(0, campaign.ClickCount);
    }
}
