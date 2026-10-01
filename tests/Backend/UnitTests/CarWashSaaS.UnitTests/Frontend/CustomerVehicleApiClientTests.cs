using System.Net;
using System.Text;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Frontend;

public sealed class CustomerVehicleApiClientTests
{
    [Fact]
    public async Task SearchAsync_ShouldSendBothSearchCriteriaAndDeserializeMatches()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/customers/search", request.RequestUri!.AbsolutePath);
            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            Assert.Contains("plate=ABC-1D23", query);
            Assert.Contains("phone=+55 (11) 99999-9999", query);

            var response = new[]
            {
                new CustomerVehicleMatchDto(
                    Guid.CreateVersion7(),
                    "Maria Silva",
                    "+55 (11) 99999-9999",
                    [new VehicleSummaryDto(Guid.CreateVersion7(), "ABC1D23", "HatchSedan")])
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
            });
        });
        var api = CreateClient(handler);

        var result = await api.SearchAsync("ABC-1D23", "+55 (11) 99999-9999");

        Assert.Single(result);
        Assert.Equal("Maria Silva", result.Single().CustomerName);
        Assert.Equal("ABC1D23", result.Single().Vehicles.Single().Plate);
    }

    [Fact]
    public async Task CreateAsync_ShouldExposeConflictStatusAndDescription()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                "{\"Code\":\"vehicle.plate.duplicate\",\"Description\":\"A placa já está cadastrada.\"}",
                Encoding.UTF8,
                "application/json")
        }));
        var api = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<CustomerVehicleApiException>(() => api.CreateAsync(
            new CreateCustomerWithVehicleRequest("Maria Silva", "11999999999", "ABC-1D23", "HatchSedan")));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("A placa já está cadastrada.", exception.Message);
    }

    private static CustomerVehicleApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}