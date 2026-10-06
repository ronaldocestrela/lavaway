using Bunit;
using CarWashSaaS.Client.Components;
using Xunit;

namespace CarWashSaaS.ComponentTests;

public sealed class PageHeadingTests : BunitContext
{
    [Fact]
    public void PageHeading_ShouldRenderTitleEyebrowSubtitleAndActions()
    {
        var cut = Render<PageHeading>(parameters => parameters
            .Add(p => p.Eyebrow, "GESTÃO")
            .Add(p => p.Title, "Clientes")
            .Add(p => p.Subtitle, "Gerencie seus clientes.")
            .Add(p => p.Actions, builder => builder.AddMarkupContent(0, "<button>Novo cliente</button>")));

        Assert.Equal("Clientes", cut.Find("h1.page-title").TextContent.Trim());
        Assert.Equal("GESTÃO", cut.Find(".eyebrow").TextContent.Trim());
        Assert.Equal("Gerencie seus clientes.", cut.Find(".page-subtitle").TextContent.Trim());
        Assert.Equal("Novo cliente", cut.Find(".page-heading-actions button").TextContent.Trim());
    }

    [Fact]
    public void PageHeading_ShouldOmitOptionalSectionsWhenNotProvided()
    {
        var cut = Render<PageHeading>(parameters => parameters
            .Add(p => p.Title, "Recepção"));

        Assert.Equal("Recepção", cut.Find("h1.page-title").TextContent.Trim());
        Assert.Empty(cut.FindAll(".eyebrow"));
        Assert.Empty(cut.FindAll(".page-subtitle"));
        Assert.Empty(cut.FindAll(".page-heading-actions"));
    }
}
