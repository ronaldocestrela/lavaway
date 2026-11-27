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
            var decodedHomeHtml = System.Net.WebUtility.HtmlDecode(homeHtml);

            // 1. Hero section
            Assert.Contains("<h1 class=\"hero-title\"", homeHtml);
            Assert.Contains("Transforme o seu lava-jato", homeHtml);
            Assert.Contains("O SISTEMA OPERACIONAL DA ESTÉTICA AUTOMOTIVA", decodedHomeHtml);
            Assert.Contains("Iniciar Teste Grátis de 14 Dias", decodedHomeHtml);

            // 2. Funcionalidades / Recursos section
            Assert.Contains("RECURSOS COMPLETOS", decodedHomeHtml);
            Assert.Contains("Tudo o que sua estética precisa em um só lugar", decodedHomeHtml);
            Assert.Contains("Check-in Relâmpago", decodedHomeHtml);
            Assert.Contains("Vistoria Digital Anti-Avaria", decodedHomeHtml);
            Assert.Contains("Automação de WhatsApp", decodedHomeHtml);
            Assert.Contains("Tabela de Preços por Porte", decodedHomeHtml);
            Assert.Contains("Gestão de Pátio e Boxes", decodedHomeHtml);
            Assert.Contains("Equipe e Comissões", decodedHomeHtml);

            // 3. Planos e Preços section
            Assert.Contains("PLANOS E PREÇOS", decodedHomeHtml);
            Assert.Contains("Investimento claro, sem pegadinhas", decodedHomeHtml);
            Assert.Contains("PLANO BÁSICO", decodedHomeHtml.ToUpperInvariant());
            Assert.Contains("PLANO PRO", decodedHomeHtml.ToUpperInvariant());
            Assert.Contains("PLANO ENTERPRISE", decodedHomeHtml.ToUpperInvariant());
            Assert.Contains("Equipe e usuários ilimitados", decodedHomeHtml);
            Assert.DoesNotContain("Até 0 membros na equipe", decodedHomeHtml);

            // 4. FAQ section
            Assert.Contains("PERGUNTAS FREQUENTES", decodedHomeHtml);
            Assert.Contains("Tire suas dúvidas antes de contratar", decodedHomeHtml);
            Assert.Contains("Preciso comprar computador ou maquininha especial para usar?", decodedHomeHtml);
            Assert.Contains("Não! O Lava Aê funciona 100% no navegador", decodedHomeHtml);

            // 5. Validar que blazor-error-ui permanece estritamente vazio e oculto no HTML inicial
            Assert.Contains("<div id=\"blazor-error-ui\" hidden aria-hidden=\"true\"></div>", homeHtml);
            Assert.DoesNotContain("<div id=\"blazor-error-ui\" style=\"display: block\"", homeHtml);
            Assert.DoesNotContain("<div id=\"blazor-error-ui\">Ocorreu um erro", homeHtml);
            Assert.DoesNotContain("Iniciando aplicação...", homeHtml);
            Assert.DoesNotContain("An unhandled error has occurred.", homeHtml);
            Assert.Contains("<link rel=\"canonical\" href=\"https://lavaae.app/\" />", homeHtml);

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
