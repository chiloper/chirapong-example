using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

public record MockPaymentInfo(string ProviderRef, decimal Amount, PaymentStatus Status, Guid OrderId);

public record SimulateRequest([Required, RegularExpression("succeeded|failed")] string Outcome);

/// <summary>
/// Development-only stand-in for the provider's hosted payment page backend. "Simulate" plays the part of
/// the provider delivering a webhook; it hands the same event to the same processor the real webhook uses.
/// Disabled (404) unless Payments:MockEnabled is true.
/// </summary>
[ApiController]
[Route("api/mock-provider/payments")]
public class MockProviderController(AppDbContext db, PaymentService payments, IConfiguration config) : ControllerBase
{
    private bool Enabled => config.GetValue("Payments:MockEnabled", false);

    [HttpGet("{providerRef}")]
    public async Task<ActionResult<MockPaymentInfo>> Get(string providerRef, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var info = await db.Payments.AsNoTracking()
            .Where(p => p.ProviderRef == providerRef && p.Provider == MockPaymentGateway.ProviderName)
            .Select(p => new MockPaymentInfo(p.ProviderRef, p.Amount, p.Status, p.Order.PublicId))
            .FirstOrDefaultAsync(ct);
        return info is null ? NotFound() : info;
    }

    [HttpPost("{providerRef}/simulate")]
    public async Task<ActionResult<MockPaymentInfo>> Simulate(string providerRef, SimulateRequest request, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderRef == providerRef && p.Provider == MockPaymentGateway.ProviderName, ct);
        if (payment is null) return NotFound();

        var type = request.Outcome == "succeeded" ? PaymentService.Succeeded : PaymentService.Failed;
        await payments.ProcessAsync(new PaymentWebhookEvent($"mock_evt_{Guid.NewGuid():N}", type, providerRef, payment.Amount), ct);
        return await Get(providerRef, ct);
    }
}
