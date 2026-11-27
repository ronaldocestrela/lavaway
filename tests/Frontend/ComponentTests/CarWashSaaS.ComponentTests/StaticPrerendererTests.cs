using CarWashSaaS.Client.Prerender;

namespace CarWashSaaS.ComponentTests;

public class StaticPrerendererTests
{
    [Fact]
    public async Task PrerenderAllAsync_GeneratesSeoAndGeoReadyHtml_WithoutLoadingOrErrorText()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lavaway-prerender-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceIndexHtml = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "..",
                "src", "Frontend", "CarWashSaaS.Client.Web", "wwwroot", "index.html"));

            Assert.True(File.Exists(sourceIndexHtml), $"index.html não encontrado em {sourceIndexHtml}");
            File.Copy(sourceIndexHtml, Path.Combine(tempRoot, "index.html"));

            var count = await StaticPrerenderer.PrerenderAllAsync(tempRoot, "https://lavaae.app");
            Assert.True(count >= 5);

            var homeHtml = await File.ReadAllTextAsync(Path.Combine(tempRoot, "prerendered", "index.html"));
            Assert.Contains("<h1 class=\"hero-title\"", homeHtml);
            Assert.Contains("Transforme o seu lava-jato", homeHtml);
            Assert.Contains("<link rel=\"canonical\" href=\"https://lavaae.app/\" />", homeHtml);
            Assert.DoesNotContain("Iniciando aplicação...", homeHtml);
            Assert.DoesNotContain("An unhandled error has occurred.", homeHtml);

            var privacyHtml = await File.ReadAllTextAsync(Path.Combine(tempRoot, "privacidade", "index.html"));
            Assert.Contains("<h1", privacyHtml);
            Assert.Contains("Política de Privacidade e LGPD | Lava Aê", privacyHtml);
            Assert.Contains("<link rel=\"canonical\" href=\"https://lavaae.app/privacidade\" />", privacyHtml);
            Assert.DoesNotContain("Iniciando aplicação...", privacyHtml);
            Assert.DoesNotContain("An unhandled error has occurred.", privacyHtml);

            var termsHtml = await File.ReadAllTextAsync(Path.Combine(tempRoot, "termos", "index.html"));
            Assert.Contains("<h1", termsHtml);
            Assert.Contains("<link rel=\"canonical\" href=\"https://lavaae.app/termos\" />", termsHtml);
            Assert.DoesNotContain("An unhandled error has occurred.", termsHtml);

            var dpoHtml = await File.ReadAllTextAsync(Path.Combine(tempRoot, "dpo", "index.html"));
            Assert.Contains("<h1", dpoHtml);
            Assert.Contains("<link rel=\"canonical\" href=\"https://lavaae.app/dpo\" />", dpoHtml);
            Assert.DoesNotContain("An unhandled error has occurred.", dpoHtml);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
