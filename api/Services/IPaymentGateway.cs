using Stock.Api.Domain;

namespace Stock.Api.Services;

public record GatewayPayment(string Provider, string ProviderRef, string PaymentUrl);

/// <summary>
/// A payment provider. Starting a payment returns where the customer pays; the outcome is reported back
/// only through the signed webhook, never through the customer's browser.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayPayment> CreatePaymentAsync(Order order, CancellationToken ct);
}

/// <summary>Stand-in provider for development: the "hosted page" is /pay/{ref} in the web app.</summary>
public class MockPaymentGateway : IPaymentGateway
{
    public const string ProviderName = "mock";

    public Task<GatewayPayment> CreatePaymentAsync(Order order, CancellationToken ct)
    {
        var providerRef = $"mock_{Guid.NewGuid():N}";
        return Task.FromResult(new GatewayPayment(ProviderName, providerRef, $"/pay/{providerRef}"));
    }
}
