using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web.Pages;
using CarWashSaaS.Shared.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashSaaS.ComponentTests;

public sealed class GlobalTenantComponentTests : BunitContext
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "support@lavaway.com"),
                new Claim(ClaimTypes.Role, "PlatformSupport"),
                new Claim("role", "PlatformSupport"),
                new Claim("user_realm", "Platform"),
                new Claim("is_platform_admin", "true")
            ], "TestAuth");

            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    [Fact]
    public void ImpersonationDiagnosticBanner_ShouldNotRender_WhenSessionInactive()
    {
        var sessionState = new ImpersonationSessionState();
        Services.AddSingleton(sessionState);

        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new GlobalTenantApiClient(httpClient));
        Services.AddSingleton<IToastService>(new ToastService());

        var cut = Render<ImpersonationDiagnosticBanner>();

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void ImpersonationDiagnosticBanner_ShouldRenderBannerAndTenantDetails_WhenSessionActive()
    {
        var sessionState = new ImpersonationSessionState();
        var tenantId = Guid.NewGuid();
        sessionState.Start(
            tenantId,
            "Lavaway Paulista",
            "suporte@lavaway.com",
            "Investigação de falha",
            "CHAMADO-991",
            DateTimeOffset.UtcNow);

        Services.AddSingleton(sessionState);

        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new GlobalTenantApiClient(httpClient));
        Services.AddSingleton<IToastService>(new ToastService());

        var cut = Render<ImpersonationDiagnosticBanner>();

        Assert.Contains("MODO DIAGNÓSTICO", cut.Markup);
        Assert.Contains("Lavaway Paulista", cut.Markup);
        Assert.Contains("CHAMADO-991", cut.Markup);
        Assert.Contains("suporte@lavaway.com", cut.Markup);
        Assert.Contains("Encerrar Diagnóstico", cut.Markup);
    }

    [Fact]
    public void StartImpersonationModal_ShouldRenderTenantDataAndAuditWarning_WhenOpen()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new GlobalTenantApiClient(httpClient));
        Services.AddSingleton(new ImpersonationSessionState());
        Services.AddSingleton<IToastService>(new ToastService());

        var tenant = new GlobalTenantSummaryDto(
            Id: Guid.NewGuid(),
            Name: "Estética Automotiva Pro",
            Status: TenantStatus.Active,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StatusChangedAtUtc: DateTimeOffset.UtcNow,
            TrialEndsAtUtc: null,
            StatusReason: null,
            TradeName: "Pro Detailing",
            LegalName: "Pro Detailing LTDA",
            Cnpj: "12.345.678/0001-99",
            Phone: "11999998888",
            City: "São Paulo",
            State: "SP");

        var cut = Render<StartImpersonationModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Tenant, tenant));

        Assert.Contains("Iniciar Modo Diagnóstico (Impersonate)", cut.Markup);
        Assert.Contains("Estética Automotiva Pro", cut.Markup);
        Assert.Contains("AVISO DE AUDITORIA FORENSE", cut.Markup);
        Assert.Contains("Acessar Contexto do Tenant", cut.Markup);
    }

    [Fact]
    public void UpdateTenantStatusModal_ShouldRenderStatusSelector_WhenOpen()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        Services.AddSingleton(new GlobalTenantApiClient(httpClient));
        Services.AddSingleton<IToastService>(new ToastService());

        var tenant = new GlobalTenantSummaryDto(
            Id: Guid.NewGuid(),
            Name: "Lavaway Express",
            Status: TenantStatus.Trial,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StatusChangedAtUtc: DateTimeOffset.UtcNow,
            TrialEndsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            StatusReason: "Novo cadastro",
            TradeName: null,
            LegalName: null,
            Cnpj: null,
            Phone: null,
            City: null,
            State: null);

        var cut = Render<UpdateTenantStatusModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Tenant, tenant));

        Assert.Contains("Alterar Status Comercial", cut.Markup);
        Assert.Contains("Lavaway Express", cut.Markup);
        Assert.Contains("Novo Status Comercial", cut.Markup);
        Assert.Contains("Salvar Alteração de Status", cut.Markup);
    }

    [Fact]
    public void GlobalTenantsPage_ShouldRenderKpisAndTableHeaders()
    {
        var sampleTenants = new List<GlobalTenantSummaryDto>
        {
            new(
                Id: Guid.NewGuid(),
                Name: "Lavaway Jardins",
                Status: TenantStatus.Active,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                StatusChangedAtUtc: DateTimeOffset.UtcNow,
                TrialEndsAtUtc: null,
                StatusReason: null,
                TradeName: "Lavaway Jardins",
                LegalName: "Lavaway Jardins LTDA",
                Cnpj: "00.000.000/0001-00",
                Phone: "11988887777",
                City: "São Paulo",
                State: "SP")
        };

        var httpHandler = new FakeHttpMessageHandler(_ =>
        {
            var paged = new PagedResult<GlobalTenantSummaryDto>(sampleTenants, 1, 1, 15);
            var json = System.Text.Json.JsonSerializer.Serialize(paged);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new GlobalTenantApiClient(httpClient));
        Services.AddSingleton(new ImpersonationSessionState());
        Services.AddSingleton<IToastService>(new ToastService());
        var auth = this.AddAuthorization();
        auth.SetAuthorized("support@lavaway.com");
        auth.SetRoles("PlatformSupport", "SuperAdmin");

        var cut = Render<GlobalTenantsPage>();

        Assert.Contains("Gestão Global de Estabelecimentos (Tenants)", cut.Markup);
        Assert.Contains("TOTAL CADASTRADOS", cut.Markup);
        Assert.Contains("ATIVOS", cut.Markup);
        Assert.Contains("EM TESTES (TRIAL)", cut.Markup);
        Assert.Contains("INADIMPLENTES", cut.Markup);
        Assert.Contains("CANCELADOS", cut.Markup);
        Assert.Contains("Lavaway Jardins", cut.Markup);
        Assert.Contains("ESTABELECIMENTO", cut.Markup);
    }
}
