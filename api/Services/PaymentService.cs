using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stock.Api.Data;
using Stock.Api.Domain;

namespace Stock.Api.Services;

public record PaymentWebhookEvent(string EventId, string Type, string ProviderRef, decimal Amount);

public enum WebhookResult
{
    /// <summary>The event changed state.</summary>
    Applied,
    /// <summary>The event id was seen before; nothing changed.</summary>
    Duplicate,
    UnknownPayment,
    AmountMismatch,
    UnknownEventType,
    /// <summary>The payment was already finished by an earlier event; this one is recorded and ignored.</summary>
    AlreadyFinal,
    /// <summary>Money arrived for an order that can no longer be fulfilled; flagged for a refund.</summary>
    RefundRequired
}

public enum StartPaymentError { None, OrderNotFound, NotPayable }

public record StartPaymentResult(StartPaymentError Error, Payment? Payment);

public class PaymentService(AppDbContext db, IPaymentGateway gateway, OrderService orders)
{
    public const string Succeeded = "payment.succeeded";
    public const string Failed = "payment.failed";

    public async Task<StartPaymentResult> StartAsync(Guid orderPublicId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == orderPublicId, ct);
        if (order is null) return new(StartPaymentError.OrderNotFound, null);
        if (order.Status != OrderStatus.Pending || order.ExpiresAt <= DateTime.UtcNow)
            return new(StartPaymentError.NotPayable, null);

        // Re-use the open attempt so double clicks and page reloads land on the same payment.
        var existing = await FindOpenAsync(order.Id, ct);
        if (existing is not null) return new(StartPaymentError.None, existing);

        var created = await gateway.CreatePaymentAsync(order, ct);
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = created.Provider,
            ProviderRef = created.ProviderRef,
            PaymentUrl = created.PaymentUrl,
            Amount = order.Total,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request opened the attempt first (ux_payments_order_pending).
            db.ChangeTracker.Clear();
            var winner = await FindOpenAsync(order.Id, ct);
            return winner is null ? new(StartPaymentError.NotPayable, null) : new(StartPaymentError.None, winner);
        }

        return new(StartPaymentError.None, payment);
    }

    private Task<Payment?> FindOpenAsync(long orderId, CancellationToken ct) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Pending, ct);

    /// <summary>
    /// Applies one provider event. The event id is inserted first, in the same transaction as every state
    /// change, so a redelivery hits the primary key and changes nothing, while an event whose processing
    /// fails rolls back and can be retried by the provider.
    /// </summary>
    public async Task<WebhookResult> ProcessAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (evt.Type is not (Succeeded or Failed)) return WebhookResult.UnknownEventType;

        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.ProviderRef == evt.ProviderRef, ct);
        if (payment is null) return WebhookResult.UnknownPayment;
        if (evt.Amount != payment.Amount) return WebhookResult.AmountMismatch;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        db.PaymentEvents.Add(new PaymentEvent { EventId = evt.EventId, PaymentId = payment.Id, Type = evt.Type, ReceivedAt = now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return WebhookResult.Duplicate; // transaction is rolled back on dispose
        }

        var target = evt.Type == Succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed;
        var moved = await db.Payments
            .Where(p => p.Id == payment.Id && p.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, target)
                .SetProperty(p => p.CompletedAt, now), ct);
        if (moved == 0)
        {
            await tx.CommitAsync(ct); // keep the event record; the payment was already settled
            return WebhookResult.AlreadyFinal;
        }

        var result = WebhookResult.Applied;
        if (target == PaymentStatus.Succeeded && !await orders.CommitSaleAsync(payment.OrderId, now, ct))
        {
            await db.Payments
                .Where(p => p.Id == payment.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.RefundRequired), ct);
            result = WebhookResult.RefundRequired;
        }

        await tx.CommitAsync(ct);
        return result;
    }
}
