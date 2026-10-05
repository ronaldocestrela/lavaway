using System.Net;
using System.Text.Json;
using Bunit;
using CarWashSaaS.Client.Components;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PhotoComparisonGalleryTests : BunitContext
{
    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private void RegisterMockWorkOrderClient(WorkOrderComparisonGalleryDto galleryDto)
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(galleryDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new WorkOrderApiClient(httpClient));
    }

    [Fact]
    public void PhotoComparisonGallery_ShouldRenderGalleryAndPairWithSlider()
    {
        var workOrderId = Guid.NewGuid();
        var workOrderItemId = Guid.NewGuid();
        var beforePhotoId = Guid.NewGuid();
        var afterPhotoId = Guid.NewGuid();

        var beforePhoto = new InspectionPhotoDto(
            beforePhotoId,
            InspectionPhotoCategory.Front,
            "front_before.jpg",
            "image/jpeg",
            2048,
            null,
            DateTimeOffset.UtcNow.AddHours(-2));

        var afterPhoto = new PostServicePhotoDto(
            afterPhotoId,
            workOrderId,
            workOrderItemId,
            "Vitrificação 9H",
            beforePhotoId,
            InspectionPhotoCategory.Front,
            "Capô com Vitrificação 9H",
            "front_after.jpg",
            "image/jpeg",
            4096,
            DateTimeOffset.UtcNow,
            "Brilho espelhado");

        var pair = new PostServiceComparisonPairDto(
            workOrderItemId,
            "Vitrificação 9H",
            InspectionPhotoCategory.Front,
            "Capô com Vitrificação 9H",
            beforePhoto,
            afterPhoto,
            "Brilho espelhado");

        var eligibleService = new WorkOrderItemDto(
            workOrderItemId,
            Guid.NewGuid(),
            "Vitrificação 9H",
            800m,
            180,
            1,
            800m,
            180);

        var gallery = new WorkOrderComparisonGalleryDto(
            workOrderId,
            "Amanda Rocha",
            "BRA2E19",
            "Suv",
            "Finishing",
            true,
            [eligibleService],
            [pair],
            [],
            []);

        RegisterMockWorkOrderClient(gallery);

        var cut = Render<PhotoComparisonGallery>(parameters => parameters
            .Add(p => p.WorkOrderId, workOrderId));

        cut.WaitForState(() => !cut.Markup.Contains("Carregando fotos comparativas..."));

        Assert.Contains("Antes e Depois — Galeria Comparativa", cut.Markup);
        Assert.Contains("BRA-2E19", cut.Markup);
        Assert.Contains("Amanda Rocha", cut.Markup);
        Assert.Contains("Vitrificação 9H", cut.Markup);
        Assert.Contains("Capô com Vitrificação 9H", cut.Markup);
        Assert.Contains("Brilho espelhado", cut.Markup);
        Assert.Contains("ANTES", cut.Markup);
        Assert.Contains("DEPOIS", cut.Markup);

        // Slider controls exist
        Assert.NotNull(cut.Find("input.range-slider"));

        // Switch to side-by-side mode
        var sideBySideBtn = cut.FindAll("button.btn-toggle").First(b => b.TextContent.Contains("Lado a Lado"));
        sideBySideBtn.Click();

        Assert.NotNull(cut.Find(".side-by-side-wrapper"));
    }

    [Fact]
    public void PhotoComparisonGallery_ShouldRenderEmptyState_WhenNoPhotosExist()
    {
        var workOrderId = Guid.NewGuid();
        var gallery = new WorkOrderComparisonGalleryDto(
            workOrderId,
            "Amanda Rocha",
            "BRA2E19",
            "Suv",
            "Finishing",
            true,
            [],
            [],
            [],
            []);

        RegisterMockWorkOrderClient(gallery);

        var cut = Render<PhotoComparisonGallery>(parameters => parameters
            .Add(p => p.WorkOrderId, workOrderId));

        cut.WaitForState(() => !cut.Markup.Contains("Carregando fotos comparativas..."));

        Assert.Contains("Nenhuma foto pós-serviço registrada", cut.Markup);
    }

    [Fact]
    public void YardKanbanCard_ShouldShowAntesDepoisButton_WhenServiceIsEligible()
    {
        var card = new WorkOrderKanbanCardDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Amanda Rocha",
            Guid.NewGuid(),
            "BRA2E19",
            "Suv",
            "Finishing",
            500m,
            120,
            DateTimeOffset.UtcNow.AddMinutes(-40),
            DateTimeOffset.UtcNow.AddMinutes(40),
            null,
            null,
            null,
            ["Polimento Técnico"],
            1,
            DateTimeOffset.UtcNow.AddMinutes(-10));

        WorkOrderKanbanCardDto? clickedCard = null;

        var cut = Render<YardKanbanCard>(parameters => parameters
            .Add(p => p.Card, card)
            .Add(p => p.OnComparisonGalleryClicked, c => clickedCard = c));

        var galleryBtn = cut.Find("button.btn-gallery-trigger");
        Assert.Contains("Antes/Depois", galleryBtn.TextContent);

        galleryBtn.Click();
        Assert.NotNull(clickedCard);
        Assert.Equal(card.Id, clickedCard.Id);
    }

    [Fact]
    public async Task PhotoComparisonGallery_ShouldRenderSendWhatsAppButton_AndTriggerSend()
    {
        var workOrderId = Guid.NewGuid();
        var afterPhotoId = Guid.NewGuid();

        var afterPhoto = new PostServicePhotoDto(
            afterPhotoId,
            workOrderId,
            null,
            null,
            null,
            InspectionPhotoCategory.Front,
            "Capô Polido",
            "after.jpg",
            "image/jpeg",
            2048,
            DateTimeOffset.UtcNow,
            "Espelhamento");

        var galleryDto = new WorkOrderComparisonGalleryDto(
            workOrderId,
            "Paulo Lima",
            "BRA2E19",
            "Suv",
            "Finishing",
            true,
            [],
            [],
            [],
            [afterPhoto]);

        var whatsAppCalled = false;
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath.Contains("comparison-gallery") == true)
            {
                var json = JsonSerializer.Serialize(galleryDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsolutePath.Contains("notifications/comparison-photos") == true)
            {
                whatsAppCalled = true;
                var msgDto = new WhatsAppMessageDto(
                    Guid.NewGuid(), "11988887777", "Caption", "sent", null, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
                var json = JsonSerializer.Serialize(msgDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        Services.AddSingleton(new WorkOrderApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));

        var cut = Render<PhotoComparisonGallery>(parameters => parameters
            .Add(p => p.WorkOrderId, workOrderId));

        var sendWhatsAppBtn = cut.Find("button.btn-action-whatsapp");
        Assert.NotNull(sendWhatsAppBtn);
        Assert.Contains("WhatsApp", sendWhatsAppBtn.TextContent);

        await cut.InvokeAsync(() => sendWhatsAppBtn.Click());

        Assert.True(whatsAppCalled);
        Assert.Contains("WhatsApp Enviado!", cut.Markup);
    }
}
