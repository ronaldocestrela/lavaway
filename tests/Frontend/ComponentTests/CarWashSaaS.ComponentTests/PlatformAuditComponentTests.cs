using Bunit;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web.Pages;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashSaaS.ComponentTests;

public sealed class PlatformAuditComponentTests : BunitContext
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }


    [Fact]
    public void AuditEventDetailsModal_ShouldRenderDetails_WhenOpen()
    {
        var evt = new AuditEventDto(
            Id: Guid.NewGuid(),
            TimestampUtc: DateTimeOffset.UtcNow,
            ActorId: Guid.NewGuid(),
            ActorEmail: "admin@lavaway.com",
            ActorRole: "SuperAdmin",
            ActorRealm: "Platform",
            Action: PlatformActionConstants.TenantCreated,
            TargetType: PlatformTargetTypeConstants.Tenant,
            TargetId: "tenant-123",
            TenantId: Guid.NewGuid(),
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0",
            DetailsJson: "{\"name\":\"Auto Clean\"}",
            Outcome: "Success",
            ErrorMessage: null);

        var cut = Render<AuditEventDetailsModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Event, evt));

        Assert.Contains(PlatformActionConstants.TenantCreated, cut.Markup);
        Assert.Contains("admin@lavaway.com", cut.Markup);
        Assert.Contains("SuperAdmin", cut.Markup);
        Assert.Contains("Auto Clean", cut.Markup);
    }

    [Fact]
    public void AuditEventDetailsModal_ShouldNotRender_WhenClosed()
    {
        var cut = Render<AuditEventDetailsModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.Event, null));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void PlatformAuditPage_ShouldRenderHeaderAndFilterSection()
    {
        var httpHandler = new FakeHttpMessageHandler(_ =>
        {
            var paged = new PagedResult<AuditEventDto>([], 0, 1, 20);
            var json = System.Text.Json.JsonSerializer.Serialize(paged);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new PlatformAuditApiClient(httpClient));
        Services.AddSingleton<IToastService>(new ToastService());

        var cut = Render<PlatformAuditPage>();


        Assert.Contains("Trilha de Auditoria Administrativa", cut.Markup);
        Assert.Contains("Filtros de Consulta", cut.Markup);
        Assert.Contains("Total de Eventos", cut.Markup);
    }
}
