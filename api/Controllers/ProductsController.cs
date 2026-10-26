using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stock.Api.Data;
using Stock.Api.Domain;
using Stock.Api.Dtos;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    private static ProductResponse ToResponse(Product p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Price, p.Category, p.ImageUrl, p.IsActive,
        p.Stock != null ? p.Stock.QuantityOnHand : 0,
        p.Stock != null ? p.Stock.QuantityReserved : 0,
        p.Stock != null ? p.Stock.QuantityOnHand - p.Stock.QuantityReserved : 0,
        p.CreatedAt, p.UpdatedAt);

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> List(
        string? search, string? category, bool includeInactive = false,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Products.AsNoTracking().Include(p => p.Stock).AsQueryable();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, term) || EF.Functions.ILike(p.Sku, term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<ProductResponse>(items.Select(ToResponse).ToList(), page, pageSize, total);
    }

    [HttpGet("{sku}")]
    public async Task<ActionResult<ProductResponse>> Get(string sku, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);
        return product is null ? NotFound() : ToResponse(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var sku = request.Sku.Trim();
        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Stock = new StockItem { Sku = sku, QuantityOnHand = request.InitialQuantity }
        };
        db.Products.Add(product);
        if (request.InitialQuantity > 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                Sku = sku,
                Change = request.InitialQuantity,
                Reason = StockMovementReason.In,
                Note = "Initial stock",
                CreatedAt = DateTime.UtcNow
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new ProblemDetails { Title = "SKU already exists", Detail = $"Product with sku '{sku}' already exists." });
        }

        return CreatedAtAction(nameof(Get), new { sku }, ToResponse(product));
    }

    [HttpPut("{sku}")]
    public async Task<ActionResult<ProductResponse>> Update(string sku, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Stock).FirstOrDefaultAsync(p => p.Sku == sku, ct);
        if (product is null) return NotFound();

        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.Category = request.Category;
        product.ImageUrl = request.ImageUrl;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    /// <summary>Soft delete: the product is deactivated so order history and stock movements stay valid.</summary>
    [HttpDelete("{sku}")]
    public async Task<IActionResult> Delete(string sku, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);
        if (product is null) return NotFound();

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
