using System.Text.Json;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;
using Stripe.Checkout;

namespace CarWashSaaS.Billing.Infrastructure;

public sealed class StripeSaasBillingGatewayProvider : ISaasBillingGatewayProvider
{
    private readonly StripeBillingOptions _options;
    private readonly ILogger<StripeSaasBillingGatewayProvider> _logger;
    private readonly IStripeClient _stripeClient;

    public StripeSaasBillingGatewayProvider(
        IOptions<StripeBillingOptions> options,
        ILogger<StripeSaasBillingGatewayProvider> logger,
        IStripeClient? stripeClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _stripeClient = stripeClient ?? (string.IsNullOrWhiteSpace(_options.SecretKey) ? null! : new StripeClient(_options.SecretKey));
    }

    public async Task<Result<GatewayCheckoutSessionResult>> CreateSubscriptionCheckoutSessionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<GatewayCheckoutSessionResult>.Failure(new Error("stripe.tenant.required", "TenantId é obrigatório.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return Result<GatewayCheckoutSessionResult>.Failure(new Error("stripe.config.missing_secret", "Chave secreta da Stripe não está configurada.", ErrorType.Conflict));
        }

        try
        {
            var customerId = await GetOrCreateCustomerAsync(tenantId, customerName, customerEmail, ct);

            var sessionService = new Stripe.Checkout.SessionService(_stripeClient);
            var sessionOptions = new Stripe.Checkout.SessionCreateOptions
            {
                Customer = customerId,
                ClientReferenceId = tenantId.ToString(),
                Mode = "subscription",
                LineItems =
                [
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                        {
                            Currency = _options.Currency,
                            UnitAmount = (long)(plan.MonthlyPrice * 100),
                            Recurring = new Stripe.Checkout.SessionLineItemPriceDataRecurringOptions
                            {
                                Interval = "month"
                            },
                            ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Plano {plan.Name} — Lavaway",
                                Description = plan.Description
                            }
                        },
                        Quantity = 1
                    }
                ],
                Metadata = new Dictionary<string, string>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["plan_tier"] = plan.Tier.ToString()
                },
                SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["tenant_id"] = tenantId.ToString(),
                        ["plan_tier"] = plan.Tier.ToString()
                    }
                },
                SuccessUrl = string.IsNullOrWhiteSpace(successUrl) ? _options.DefaultSuccessUrl ?? "https://lavaway.com/settings/subscription?session_id={CHECKOUT_SESSION_ID}" : successUrl,
                CancelUrl = string.IsNullOrWhiteSpace(cancelUrl) ? _options.DefaultCancelUrl ?? "https://lavaway.com/settings/subscription?canceled=true" : cancelUrl
            };

            var session = await sessionService.CreateAsync(sessionOptions, cancellationToken: ct);
            return Result<GatewayCheckoutSessionResult>.Success(new GatewayCheckoutSessionResult(session.Id, session.Url));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao criar Checkout Session na Stripe para o tenant {TenantId}: {Message}", tenantId, ex.Message);
            return Result<GatewayCheckoutSessionResult>.Failure(new Error("stripe.checkout_creation_failed", ex.Message, ErrorType.Validation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao criar Checkout Session na Stripe para o tenant {TenantId}", tenantId);
            return Result<GatewayCheckoutSessionResult>.Failure(new Error("stripe.unexpected_error", ex.Message, ErrorType.Conflict));
        }
    }

    public async Task<Result<GatewayCheckoutSessionDetails>> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result<GatewayCheckoutSessionDetails>.Failure(new Error("stripe.session_id.required", "SessionId é obrigatório.", ErrorType.Validation));
        }

        try
        {
            var sessionService = new Stripe.Checkout.SessionService(_stripeClient);
            var session = await sessionService.GetAsync(sessionId, cancellationToken: ct);
            if (session is null)
            {
                return Result<GatewayCheckoutSessionDetails>.Failure(new Error("stripe.session.not_found", "Sessão de checkout não encontrada.", ErrorType.NotFound));
            }

            var tenantId = Guid.Empty;
            if (Guid.TryParse(session.ClientReferenceId, out var parsedRefId))
            {
                tenantId = parsedRefId;
            }
            else if (session.Metadata != null && session.Metadata.TryGetValue("tenant_id", out var tenantMeta) && Guid.TryParse(tenantMeta, out var parsedMetaId))
            {
                tenantId = parsedMetaId;
            }

            SaasPlanTier? planTier = null;
            if (session.Metadata != null && session.Metadata.TryGetValue("plan_tier", out var planMeta) && Enum.TryParse<SaasPlanTier>(planMeta, true, out var parsedTier))
            {
                planTier = parsedTier;
            }

            return Result<GatewayCheckoutSessionDetails>.Success(new GatewayCheckoutSessionDetails(
                SessionId: session.Id,
                TenantId: tenantId,
                PlanTier: planTier,
                GatewayCustomerId: session.CustomerId,
                GatewaySubscriptionId: session.SubscriptionId,
                PaymentStatus: session.PaymentStatus ?? string.Empty,
                Status: session.Status ?? string.Empty));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao consultar Checkout Session {SessionId} na Stripe: {Message}", sessionId, ex.Message);
            return Result<GatewayCheckoutSessionDetails>.Failure(new Error("stripe.session_query_failed", ex.Message, ErrorType.Validation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao consultar Checkout Session {SessionId} na Stripe", sessionId);
            return Result<GatewayCheckoutSessionDetails>.Failure(new Error("stripe.unexpected_error", ex.Message, ErrorType.Conflict));
        }
    }

    public async Task<Result<GatewayPortalSessionResult>> CreateCustomerPortalSessionAsync(
        string gatewayCustomerId,
        string returnUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            return Result<GatewayPortalSessionResult>.Failure(new Error("stripe.customer_id.required", "GatewayCustomerId é obrigatório para abrir o portal do cliente.", ErrorType.Validation));
        }

        try
        {
            var portalService = new Stripe.BillingPortal.SessionService(_stripeClient);
            var portalOptions = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = gatewayCustomerId,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? _options.DefaultSuccessUrl ?? "https://lavaway.com/settings/subscription" : returnUrl
            };

            var session = await portalService.CreateAsync(portalOptions, cancellationToken: ct);
            return Result<GatewayPortalSessionResult>.Success(new GatewayPortalSessionResult(session.Url));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao criar Customer Portal Session na Stripe para o cliente {CustomerId}: {Message}", gatewayCustomerId, ex.Message);
            return Result<GatewayPortalSessionResult>.Failure(new Error("stripe.portal_creation_failed", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        CancellationToken ct = default)
    {
        // Criação direta (quando aplicável sem checkout interativo)
        try
        {
            var customerId = await GetOrCreateCustomerAsync(tenantId, customerName, customerEmail, ct);
            var subService = new Stripe.SubscriptionService(_stripeClient);
            var productId = await GetOrCreateProductIdAsync(plan, ct);
            var subOptions = new Stripe.SubscriptionCreateOptions
            {
                Customer = customerId,
                Items =
                [
                    new Stripe.SubscriptionItemOptions
                    {
                        PriceData = new Stripe.SubscriptionItemPriceDataOptions
                        {
                            Currency = _options.Currency,
                            UnitAmount = (long)(plan.MonthlyPrice * 100),
                            Recurring = new Stripe.SubscriptionItemPriceDataRecurringOptions
                            {
                                Interval = "month"
                            },
                            Product = productId
                        }
                    }
                ],
                PaymentBehavior = "default_incomplete",
                PaymentSettings = new Stripe.SubscriptionPaymentSettingsOptions
                {
                    PaymentMethodTypes = ["card"],
                    SaveDefaultPaymentMethod = "on_subscription"
                },
                Metadata = new Dictionary<string, string>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["plan_tier"] = plan.Tier.ToString()
                }
            };

            var sub = await subService.CreateAsync(subOptions, cancellationToken: ct);
            return Result<GatewaySubscriptionResult>.Success(new GatewaySubscriptionResult(
                GatewayCustomerId: customerId,
                GatewaySubscriptionId: sub.Id,
                InitialInvoiceId: sub.LatestInvoiceId ?? string.Empty,
                PaymentUrl: null,
                PixQrCode: null,
                PixCopiaECola: null));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao criar assinatura direta na Stripe: {Message}", ex.Message);
            return Result<GatewaySubscriptionResult>.Failure(new Error("stripe.subscription_creation_failed", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<bool>> ChangeSubscriptionPlanAsync(
        string gatewaySubscriptionId,
        SaasPlan newPlan,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gatewaySubscriptionId))
        {
            return Result<bool>.Failure(new Error("stripe.subscription_id.required", "GatewaySubscriptionId é obrigatório.", ErrorType.Validation));
        }

        try
        {
            var subService = new Stripe.SubscriptionService(_stripeClient);
            var subscription = await subService.GetAsync(gatewaySubscriptionId, cancellationToken: ct);
            if (subscription is null || subscription.Items.Data.Count == 0)
            {
                return Result<bool>.Failure(new Error("stripe.subscription.not_found", "Assinatura não encontrada na Stripe.", ErrorType.NotFound));
            }

            var newProductId = await GetOrCreateProductIdAsync(newPlan, ct);
            var currentItem = subscription.Items.Data[0];
            var updateOptions = new Stripe.SubscriptionUpdateOptions
            {
                Items =
                [
                    new Stripe.SubscriptionItemOptions
                    {
                        Id = currentItem.Id,
                        PriceData = new Stripe.SubscriptionItemPriceDataOptions
                        {
                            Currency = _options.Currency,
                            UnitAmount = (long)(newPlan.MonthlyPrice * 100),
                            Recurring = new Stripe.SubscriptionItemPriceDataRecurringOptions
                            {
                                Interval = "month"
                            },
                            Product = newProductId
                        }
                    }
                ],
                ProrationBehavior = "always_invoice",
                Metadata = new Dictionary<string, string>
                {
                    ["plan_tier"] = newPlan.Tier.ToString()
                }
            };

            await subService.UpdateAsync(gatewaySubscriptionId, updateOptions, cancellationToken: ct);
            return Result<bool>.Success(true);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao atualizar plano da assinatura {SubscriptionId} na Stripe: {Message}", gatewaySubscriptionId, ex.Message);
            return Result<bool>.Failure(new Error("stripe.plan_update_failed", ex.Message, ErrorType.Validation));
        }
    }

    public async Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(
        Guid tenantId,
        string gatewayCustomerId,
        decimal amount,
        DateTimeOffset dueDateUtc,
        CancellationToken ct = default)
    {
        try
        {
            var invoiceService = new Stripe.InvoiceService(_stripeClient);
            var invoiceItemService = new Stripe.InvoiceItemService(_stripeClient);

            await invoiceItemService.CreateAsync(new Stripe.InvoiceItemCreateOptions
            {
                Customer = gatewayCustomerId,
                Amount = (long)(amount * 100),
                Currency = _options.Currency,
                Description = "Mensalidade Lavaway SaaS"
            }, cancellationToken: ct);

            var invoice = await invoiceService.CreateAsync(new Stripe.InvoiceCreateOptions
            {
                Customer = gatewayCustomerId,
                AutoAdvance = true,
                DueDate = dueDateUtc.UtcDateTime
            }, cancellationToken: ct);

            return Result<GatewayInvoiceResult>.Success(new GatewayInvoiceResult(
                GatewayInvoiceId: invoice.Id,
                Amount: amount,
                DueDateUtc: dueDateUtc,
                PaymentUrl: invoice.HostedInvoiceUrl,
                PixQrCode: null,
                PixCopiaECola: null));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao gerar fatura na Stripe para o cliente {CustomerId}: {Message}", gatewayCustomerId, ex.Message);
            return Result<GatewayInvoiceResult>.Failure(new Error("stripe.invoice_generation_failed", ex.Message, ErrorType.Validation));
        }
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        var effectiveSecret = !string.IsNullOrWhiteSpace(secret) ? secret : _options.WebhookSecret;
        if (string.IsNullOrWhiteSpace(effectiveSecret))
        {
            _logger.LogWarning("Segredo do webhook Stripe não está configurado.");
            return false;
        }

        try
        {
            // Valida cabeçalho Stripe-Signature
            var stripeEvent = Stripe.EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                effectiveSecret,
                throwOnApiVersionMismatch: false);

            return stripeEvent is not null;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Assinatura de webhook Stripe inválida: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar assinatura de webhook Stripe.");
            return false;
        }
    }

    private async Task<string> GetOrCreateCustomerAsync(Guid tenantId, string name, string email, CancellationToken ct)
    {
        var customerService = new Stripe.CustomerService(_stripeClient);
        var searchResult = await customerService.SearchAsync(new Stripe.CustomerSearchOptions
        {
            Query = $"metadata['tenant_id']:'{tenantId}'"
        }, cancellationToken: ct);

        if (searchResult.Data.Count > 0)
        {
            return searchResult.Data[0].Id;
        }

        var newCustomer = await customerService.CreateAsync(new Stripe.CustomerCreateOptions
        {
            Name = name,
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                ["tenant_id"] = tenantId.ToString()
            }
        }, cancellationToken: ct);

        return newCustomer.Id;
    }

    private async Task<string> GetOrCreateProductIdAsync(SaasPlan plan, CancellationToken ct)
    {
        var productService = new Stripe.ProductService(_stripeClient);
        var searchResult = await productService.SearchAsync(new Stripe.ProductSearchOptions
        {
            Query = $"metadata['plan_tier']:'{plan.Tier}'"
        }, cancellationToken: ct);

        if (searchResult.Data.Count > 0)
        {
            return searchResult.Data[0].Id;
        }

        var newProduct = await productService.CreateAsync(new Stripe.ProductCreateOptions
        {
            Name = $"Plano {plan.Name} — Lavaway",
            Description = plan.Description,
            Metadata = new Dictionary<string, string>
            {
                ["plan_tier"] = plan.Tier.ToString()
            }
        }, cancellationToken: ct);

        return newProduct.Id;
    }
}

