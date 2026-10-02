using System.Net;
using System.Text;
using System.Text.Json;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Frontend;

public sealed class StoreProfileApiClientTests
{
    [Fact]
    public async Task GetProfileAsync_ShouldReturnProfile_When200Ok()
    {
        var tenantId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var expected = new StoreProfileDto(
            profileId,
            tenantId,
            "LavaJato Alpha Ltda",
            "Alpha Car Wash",
            "11222333000181",
            "+5511999999999",
            "Rua Augusta, 100",
            "São Paulo",
            "SP",
            "01305-000",
            "/storage/logo.png",
            "#0066FF",
            "#111827");

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/tenants/profile", request.RequestUri!.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expected), Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler);
        var result = await client.GetProfileAsync();

        Assert.NotNull(result);
        Assert.Equal("LavaJato Alpha Ltda", result.LegalName);
        Assert.Equal("11222333000181", result.Cnpj);
    }

    [Fact]
    public async Task GetProfileAsync_ShouldReturnNull_When404NotFound()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/tenants/profile", request.RequestUri!.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"code\":\"store_profile.not_found\"}", Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler);
        var result = await client.GetProfileAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProfileAsync_ShouldSendPayload_AndReturnCreatedProfile()
    {
        var requestPayload = new CreateStoreProfileRequest(
            "LavaJato Alpha Ltda",
            "Alpha Car Wash",
            "11222333000181",
            "+5511999999999",
            "Rua Augusta, 100",
            "São Paulo",
            "SP",
            "01305-000");

        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/tenants/profile", request.RequestUri!.AbsolutePath);
            var content = await request.Content!.ReadAsStringAsync();
            Assert.Contains("11222333000181", content);

            var created = new StoreProfileDto(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                requestPayload.LegalName,
                requestPayload.TradeName,
                requestPayload.Cnpj,
                requestPayload.Phone,
                requestPayload.Street,
                requestPayload.City,
                requestPayload.State,
                requestPayload.PostalCode,
                null,
                null,
                null);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(created), Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var result = await client.CreateProfileAsync(requestPayload);

        Assert.NotNull(result);
        Assert.Equal("LavaJato Alpha Ltda", result.LegalName);
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldThrowException_WhenValidationFails()
    {
        var updatePayload = new UpdateStoreProfileRequest(
            "",
            "Alpha Car Wash",
            "11222333000181",
            "+5511999999999",
            "Rua Augusta, 100",
            "São Paulo",
            "SP",
            "01305-000");

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                "{\"Code\":\"store_profile.legal_name.invalid\",\"Description\":\"A legal name is required.\"}",
                Encoding.UTF8,
                "application/json")
        }));

        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<StoreProfileApiException>(() => client.UpdateProfileAsync(updatePayload));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("A legal name is required.", ex.Message);
    }

    private static StoreProfileApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
