using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain;
using Stock.Api.Dtos;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(AppDbContext db, OrderService orders) : ControllerBase
{
    public static OrderResponse ToResponse(Order o) => new(
        o.PublicId, o.Status, o.Total, o.CustomerName, o.CustomerEmail, o.CustomerPhone, o.ShippingAddress,
        o.CreatedAt, o.ExpiresAt, o.PaidAt, o.CancelledAt,
        o.Items.OrderBy(i => i.Sku)
            .Select(i => new OrderItemResponse(i.Sku, i.NameSnapshot, i.UnitPriceSnapshot, i.Quantity, i.UnitPriceSnapshot * i.Quantity))
            .ToList());

    /// <summary>Guests have no account, so the random public id in the URL is the only access key.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> Get(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.PublicId == id, ct);
        return order is null ? NotFound() : ToResponse(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancellationToken ct)
    {
        var orderId = await db.Orders.AsNoTracking().Where(o => o.PublicId == id).Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);
        if (orderId is null) return NotFound();

        if (!await orders.ReleasePendingAsync(orderId.Value, OrderStatus.Cancelled, ct))
            return Conflict(new ProblemDetails { Title = "Order cannot be cancelled", Detail = "Only pending orders can be cancelled." });

        return await Get(id, ct);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderSummaryResponse>>> List(
        OrderStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Orders.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(o => o.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderSummaryResponse(
                o.PublicId, o.Status, o.Total, o.CustomerName, o.CustomerEmail,
                o.Items.Sum(i => i.Quantity), o.CreatedAt, o.ExpiresAt))
            .ToListAsync(ct);

        return new PagedResponse<OrderSummaryResponse>(items, page, pageSize, total);
    }
}
