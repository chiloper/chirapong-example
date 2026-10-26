using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain;

namespace Stock.Api.Services;

public class OrderService(AppDbContext db)
{
    /// <summary>
    /// Marks a Pending order Paid and consumes its reservation: on-hand and reserved both drop by the line quantity,
    /// so available is unchanged. Returns false (and changes nothing) if the order is no longer Pending.
    /// Must run inside the caller's transaction so the sale commits together with the payment record.
    /// Uses the same Pending-only conditional flip as <see cref="ReleasePendingAsync"/>, so pay, cancel and expiry
    /// can race and exactly one of them wins.
    /// </summary>
    public async Task<bool> CommitSaleAsync(long orderId, DateTime now, CancellationToken ct)
    {
        var flipped = await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.PaidAt, now), ct);
        if (flipped == 0) return false;

        var items = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.Sku)
            .Select(i => new { i.Sku, i.Quantity })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            var qty = item.Quantity;
            var sku = item.Sku;
            var consumed = await db.Stocks
                .Where(s => s.Sku == sku && s.QuantityReserved >= qty && s.QuantityOnHand >= qty)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand - qty)
                    .SetProperty(x => x.QuantityReserved, x => x.QuantityReserved - qty), ct);
            if (consumed == 0)
            {
                // The reservation made at checkout is gone: never sell stock we cannot account for.
                throw new InvalidOperationException($"Reservation for {sku} on order {orderId} is missing or smaller than {qty}.");
            }

            db.StockMovements.Add(new StockMovement
            {
                Sku = sku,
                Change = -qty,
                Reason = StockMovementReason.Sale,
                RefOrderId = orderId,
                Note = "Order paid",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Moves a Pending order to <paramref name="target"/> and returns its reserved stock.
    /// The status flip is a conditional UPDATE (Pending only) and the release happens in the same
    /// transaction only when that UPDATE hit a row, so cancel, expiry and any future pay step can race
    /// without releasing (or consuming) the same reservation twice.
    /// Movement sign convention: values are changes in available stock (Reserve = -n, Release = +n).
    /// </summary>
    public async Task<bool> ReleasePendingAsync(long orderId, OrderStatus target, CancellationToken ct)
    {
        if (target is not (OrderStatus.Cancelled or OrderStatus.Expired))
            throw new ArgumentOutOfRangeException(nameof(target));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        var flipped = await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, target)
                .SetProperty(o => o.CancelledAt, now), ct);
        if (flipped == 0) return false;

        var items = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.Sku)
            .Select(i => new { i.Sku, i.Quantity })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            var qty = item.Quantity;
            var sku = item.Sku;
            await db.Stocks
                .Where(s => s.Sku == sku)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityReserved, x => x.QuantityReserved - qty), ct);

            db.StockMovements.Add(new StockMovement
            {
                Sku = sku,
                Change = qty,
                Reason = StockMovementReason.Release,
                RefOrderId = orderId,
                Note = target == OrderStatus.Expired ? "Reservation expired" : "Order cancelled",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
