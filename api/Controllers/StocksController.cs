using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain;
using Stock.Api.Dtos;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/stocks")]
public class StocksController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<StockResponse>>> List(
        bool lowStockOnly = false, int lowStockThreshold = 5,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Stocks.AsNoTracking().Include(s => s.Product).AsQueryable();
        if (lowStockOnly)
            query = query.Where(s => s.QuantityOnHand - s.QuantityReserved <= lowStockThreshold);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Sku)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new StockResponse(s.Sku, s.Product.Name, s.QuantityOnHand, s.QuantityReserved, s.QuantityOnHand - s.QuantityReserved))
            .ToListAsync(ct);

        return new PagedResponse<StockResponse>(items, page, pageSize, total);
    }

    [HttpGet("{sku}")]
    public async Task<ActionResult<StockResponse>> Get(string sku, CancellationToken ct)
    {
        var stock = await db.Stocks.AsNoTracking().Include(s => s.Product)
            .Where(s => s.Sku == sku)
            .Select(s => new StockResponse(s.Sku, s.Product.Name, s.QuantityOnHand, s.QuantityReserved, s.QuantityOnHand - s.QuantityReserved))
            .FirstOrDefaultAsync(ct);
        return stock is null ? NotFound() : stock;
    }

    /// <summary>
    /// Adjust on-hand quantity by a signed delta. The update is a single conditional UPDATE,
    /// so concurrent adjustments cannot push on-hand below zero or below the reserved quantity.
    /// </summary>
    [HttpPost("{sku}/adjust")]
    public async Task<ActionResult<StockResponse>> Adjust(string sku, AdjustStockRequest request, CancellationToken ct)
    {
        if (request.Change == 0)
            return BadRequest(new ProblemDetails { Title = "Change must not be zero." });
        if (request.Reason is not (StockMovementReason.In or StockMovementReason.Out or StockMovementReason.Adjust))
            return BadRequest(new ProblemDetails { Title = "Reason must be In, Out or Adjust." });
        if (request.Reason == StockMovementReason.In && request.Change < 0)
            return BadRequest(new ProblemDetails { Title = "'In' requires a positive change." });
        if (request.Reason == StockMovementReason.Out && request.Change > 0)
            return BadRequest(new ProblemDetails { Title = "'Out' requires a negative change." });

        var change = request.Change;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var updated = await db.Stocks
            .Where(s => s.Sku == sku && s.QuantityOnHand + change >= s.QuantityReserved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand + change), ct);

        if (updated == 0)
        {
            var current = await db.Stocks.AsNoTracking().FirstOrDefaultAsync(s => s.Sku == sku, ct);
            return current is null
                ? NotFound()
                : Conflict(new ProblemDetails
                {
                    Title = "Insufficient stock",
                    Detail = $"Cannot apply {change:+#;-#}: on hand {current.QuantityOnHand}, reserved {current.QuantityReserved}. On hand cannot drop below reserved."
                });
        }

        db.StockMovements.Add(new StockMovement
        {
            Sku = sku,
            Change = change,
            Reason = request.Reason,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await Get(sku, ct);
    }

    [HttpGet("{sku}/movements")]
    public async Task<ActionResult<PagedResponse<StockMovementResponse>>> Movements(
        string sku, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.StockMovements.AsNoTracking().Where(m => m.Sku == sku);
        var total = await query.CountAsync(ct);
        if (total == 0 && !await db.Stocks.AnyAsync(s => s.Sku == sku, ct)) return NotFound();

        var items = await query
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new StockMovementResponse(m.Id, m.Sku, m.Change, m.Reason, m.RefOrderId, m.Note, m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResponse<StockMovementResponse>(items, page, pageSize, total);
    }
}
