using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain;
using Stock.Api.Dtos;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/checkout")]
public class CheckoutController(AppDbContext db, IConfiguration config) : ControllerBase
{
    /// <summary>
    /// Turns the guest cart into a Pending order and reserves stock, all in one transaction.
    /// The cart row is locked first so a double submit cannot create two orders, lines are processed in
    /// SKU order so overlapping carts cannot deadlock, and each reservation is a conditional UPDATE so
    /// two buyers can never reserve the last unit.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(CartController.CookieName, out var raw) || !Guid.TryParse(raw, out var cartId))
            return BadRequest(new ProblemDetails { Title = "Cart is empty" });

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var locked = await db.Carts.Where(c => c.Id == cartId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, now), ct);
        if (locked == 0) return BadRequest(new ProblemDetails { Title = "Cart is empty" });

        // Re-read name, price and active flag here; the cart is never trusted for those.
        var lines = await db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cartId)
            .OrderBy(i => i.Sku)
            .Select(i => new { i.Sku, i.Quantity, i.Product.Name, i.Product.Price, i.Product.IsActive })
            .ToListAsync(ct);
        if (lines.Count == 0) return BadRequest(new ProblemDetails { Title = "Cart is empty" });

        var inactive = lines.Where(l => !l.IsActive).Select(l => l.Sku).ToList();
        if (inactive.Count > 0)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Some items are no longer sold",
                Detail = $"Remove these from your cart: {string.Join(", ", inactive)}."
            });
        }

        foreach (var line in lines)
        {
            var qty = line.Quantity;
            var sku = line.Sku;
            var reserved = await db.Stocks
                .Where(s => s.Sku == sku && s.QuantityOnHand - s.QuantityReserved >= qty)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityReserved, x => x.QuantityReserved + qty), ct);
            if (reserved == 0)
            {
                var available = await db.Stocks.AsNoTracking()
                    .Where(s => s.Sku == sku)
                    .Select(s => s.QuantityOnHand - s.QuantityReserved)
                    .FirstOrDefaultAsync(ct);
                // Leaving without commit rolls back every reservation made so far.
                return Conflict(new ProblemDetails
                {
                    Title = "Not enough stock",
                    Detail = $"{line.Name} ({sku}): requested {qty}, only {Math.Max(available, 0)} available.",
                    Extensions = { ["sku"] = sku, ["available"] = Math.Max(available, 0) }
                });
            }
        }

        var ttlMinutes = config.GetValue("Orders:ReservationMinutes", 15.0);
        var order = new Order
        {
            PublicId = Guid.NewGuid(),
            Status = OrderStatus.Pending,
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhone = request.CustomerPhone?.Trim(),
            ShippingAddress = request.ShippingAddress?.Trim(),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ttlMinutes),
            Items = lines.Select(l => new OrderItem
            {
                Sku = l.Sku,
                NameSnapshot = l.Name,
                UnitPriceSnapshot = l.Price,
                Quantity = l.Quantity
            }).ToList()
        };
        order.Total = order.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct); // assigns order.Id for the movement log

        // Movement sign convention: change in available stock, so Reserve is negative.
        foreach (var item in order.Items)
        {
            db.StockMovements.Add(new StockMovement
            {
                Sku = item.Sku,
                Change = -item.Quantity,
                Reason = StockMovementReason.Reserve,
                RefOrderId = order.Id,
                Note = "Checkout reservation",
                CreatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        await db.CartItems.Where(i => i.CartId == cartId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        return CreatedAtAction("Get", "Orders", new { id = order.PublicId }, OrdersController.ToResponse(order));
    }
}
