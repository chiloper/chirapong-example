using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

public record StartPaymentResponse(string ProviderRef, string PaymentUrl, decimal Amount);

[ApiController]
public class PaymentsController(PaymentService payments, IConfiguration config, ILogger<PaymentsController> log) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("api/orders/{id:guid}/pay")]
    public async Task<ActionResult<StartPaymentResponse>> Start(Guid id, CancellationToken ct)
    {
        var result = await payments.StartAsync(id, ct);
        return result.Error switch
        {
            StartPaymentError.OrderNotFound => NotFound(),
            StartPaymentError.NotPayable => Conflict(new ProblemDetails
            {
                Title = "Order cannot be paid",
                Detail = "Only pending orders whose reservation has not expired can be paid."
            }),
            _ => new StartPaymentResponse(result.Payment!.ProviderRef, result.Payment.PaymentUrl, result.Payment.Amount)
        };
    }

    /// <summary>
    /// Provider callback. The body is signed (HMAC-SHA256 of the raw bytes, hex, header
    /// <c>X-Signature: sha256=...</c>); anything unsigned or wrongly signed is rejected before it is parsed.
    /// Redeliveries of the same event id are acknowledged with 200 and change nothing.
    /// </summary>
    [HttpPost("api/payments/webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var secret = config["Payments:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            log.LogError("Payments:WebhookSecret is not configured; refusing webhook");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();

        if (!IsValidSignature(secret, body, Request.Headers["X-Signature"].ToString()))
            return Unauthorized();

        WebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        if (payload is null || string.IsNullOrWhiteSpace(payload.EventId) || string.IsNullOrWhiteSpace(payload.ProviderRef)
            || string.IsNullOrWhiteSpace(payload.Type) || payload.EventId.Length > 100)
            return BadRequest();

        var result = await payments.ProcessAsync(new PaymentWebhookEvent(payload.EventId, payload.Type, payload.ProviderRef, payload.Amount), ct);
        if (result == WebhookResult.RefundRequired)
            log.LogWarning("Payment {ProviderRef} succeeded for an order that can no longer be fulfilled; refund required", payload.ProviderRef);

        return result switch
        {
            WebhookResult.UnknownPayment => NotFound(),
            WebhookResult.AmountMismatch => UnprocessableEntity(new ProblemDetails { Title = "Amount does not match the payment" }),
            WebhookResult.UnknownEventType => Ok(new { result = "ignored" }),
            _ => Ok(new { result = result.ToString() })
        };
    }

    public static string Sign(string secret, byte[] body) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

    private static bool IsValidSignature(string secret, byte[] body, string header)
    {
        if (string.IsNullOrEmpty(header)) return false;
        var expected = Encoding.ASCII.GetBytes(Sign(secret, body));
        var actual = Encoding.ASCII.GetBytes(header.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private record WebhookPayload(string? EventId, string? Type, string? ProviderRef, decimal Amount);
}
