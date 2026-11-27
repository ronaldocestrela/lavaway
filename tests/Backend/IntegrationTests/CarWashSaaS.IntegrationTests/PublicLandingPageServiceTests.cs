using CarWashSaaS.Api.Services;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.IntegrationTests;

public class PublicLandingPageServiceTests
{
    [Fact]
    public void InjectLivePlansIntoHtml_ReplacesPricingFooterAndJsonLdWithLivePlans()
    {
        var template = $$"""
            <html>
            <head>
            <script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[{"@type":"SoftwareApplication","offers":[{"@type":"Offer","name":"Plano Básico","price":"149.00","priceCurrency":"BRL"}]}]}
            </script>
            </head>
            <body>
            {{PublicLandingPageService.PricingStartMarker}}<div class="pricing-grid">OLD</div>{{PublicLandingPageService.PricingEndMarker}}
            {{PublicLandingPageService.FooterPlansStartMarker}}<button>OLD</button>{{PublicLandingPageService.FooterPlansEndMarker}}
            </body>
            </html>
            """;

        var plans = SaasPlan.GetStandardPlans().ToList();
        plans[0].UpdateDetails("Básico", "Desc Básico", 179.00m, 200, 400, 4, 3, false, false, false, false, true);
        plans[1].UpdateDetails("Pro", "Desc Pro", 349.00m, 800, 2000, 12, 6, true, true, true, true, true);
        plans[2].UpdateDetails("Enterprise", "Desc Enterprise", 699.00m, 0, 0, 0, 0, true, true, true, true, true);

        var result = PublicLandingPageService.InjectLivePlansIntoHtml(template, plans);

        Assert.Contains("179", result);
        Assert.Contains("349", result);
        Assert.Contains("699", result);
        Assert.Contains("\"price\":\"349.00\"", result);
        Assert.Contains("Equipe e usuários ilimitados", result);
        Assert.DoesNotContain("Até 0 membros na equipe", result);
        Assert.DoesNotContain(">OLD<", result);
    }

    [Fact]
    public void InjectLivePlansIntoLlmsTxt_UpdatesPricesAndQuotas()
    {
        var llmsTemplate = """
            # Lavaway (Lava Aê)

            ## Planos e Preços

            - **Plano Básico (R$ 149,00 / mês)**: Antigo

            ## Contato e Acesso
            - **Site**: https://lavaae.app
            """;

        var proPlan = SaasPlan.GetStandardPlans().Single(p => p.Tier == SaasPlanTier.Pro);
        proPlan.UpdateDetails("Pro", "Plano atualizado", 349.00m, 800, 2000, 12, 6, true, true, true, true, true);
        var plans = new List<SaasPlan> { proPlan };

        var result = PublicLandingPageService.InjectLivePlansIntoLlmsTxt(llmsTemplate, plans);

        Assert.Contains("349,00", result);
        Assert.Contains("Plano atualizado", result);
        Assert.Contains("2000 msgs WhatsApp/mês", result);
        Assert.Contains("## Contato e Acesso", result);
        Assert.DoesNotContain("149,00", result);
    }
}
