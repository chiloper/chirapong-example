using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stock.Api.Data;
using Stock.Api.Domain;
using Stock.Api.Dtos;

namespace Stock.Api.Controllers;

/// <summary>
/// Guest cart. The cart id lives in an HttpOnly cookie; there are no accounts.
/// A cart never reserves stock: reservation happens at checkout only.
/// </summary>
[ApiController]
[Route("api/cart")]
public class CartController(AppDbContext db) : ControllerBase
{
    public const string CookieName = "cart_id";

    private Guid? ReadCartId() =>
        Request.Cookies.TryGetValue(CookieName, out var raw) && Guid.TryParse(raw, out var id) ? id : null;

    private void WriteCartCookie(Guid id) =>
        Response.Cookies.Append(CookieName, id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });

    private static readonly CartResponse EmptyCart = new([], 0, 0m);

    private async Task<CartResponse> BuildAsync(Guid cartId, CancellationToken ct)
    {
        var rows = await db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cartId)
            .OrderBy(i => i.AddedAt).ThenBy(i => i.Sku)
            .Select(i => new
            {
                i.Sku,
                i.Product.Name,
                i.Product.ImageUrl,
                i.Product.Price,
                i.Product.IsActive,
                i.Quantity,
                Available = i.Product.Stock != null ? i.Product.Stock.QuantityOnHand - i.Product.Stock.QuantityReserved : 0
            })
            .ToListAsync(ct);

        var lines = rows
            .Select(r => new CartLineResponse(
                r.Sku, r.Name, r.ImageUrl, r.Price, r.Quantity, r.Available, r.Price * r.Quantity,
                r.IsActive && r.Available >= r.Quantity))
            .ToList();
        return new CartResponse(lines, lines.Sum(l => l.Quantity), lines.Sum(l => l.LineTotal));
    }

    [HttpGet]
    public async Task<CartResponse> Get(CancellationToken ct)
    {
        var id = ReadCartId();
        return id is null ? EmptyCart : await BuildAsync(id.Value, ct);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> Add(AddCartItemRequest request, CancellationToken ct)
    {
        var sku = request.Sku.Trim();
        var product = await db.Products.AsNoTracking().Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Sku == sku && p.IsActive, ct);
        if (product is null) return NotFound(new ProblemDetails { Title = "Product not found" });
        var available = product.Stock is null ? 0 : product.Stock.QuantityOnHand - product.Stock.QuantityReserved;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var cartId = ReadCartId() ?? Guid.NewGuid();
            var cart = await db.Carts.FirstOrDefaultAsync(c => c.Id == cartId, ct);
            if (cart is null)
            {
                // Unknown or missing cookie: start a fresh cart under a new id.
                cartId = Guid.NewGuid();
                cart = new Cart { Id = cartId, CreatedAt = DateTime.UtcNow };
                db.Carts.Add(cart);
            }

            var item = await db.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.Sku == sku, ct);
            var newQuantity = (item?.Quantity ?? 0) + request.Quantity;
            if (newQuantity > available)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Not enough stock",
                    Detail = $"Only {available} available for {sku}; you already have {item?.Quantity ?? 0} in your cart."
                });
            }

            if (item is null)
                db.CartItems.Add(new CartItem { CartId = cartId, Sku = sku, Quantity = newQuantity, AddedAt = DateTime.UtcNow });
            else
                item.Quantity = newQuantity;
            cart.UpdatedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } && attempt == 0)
            {
                // A concurrent add created the same line; reload and merge on the retry.
                db.ChangeTracker.Clear();
                continue;
            }

            WriteCartCookie(cartId);
            return await BuildAsync(cartId, ct);
        }

        return StatusCode(StatusCodes.Status409Conflict, new ProblemDetails { Title = "Cart changed, please retry" });
    }

    [HttpPut("items/{sku}")]
    public async Task<ActionResult<CartResponse>> SetQuantity(string sku, SetCartItemRequest request, CancellationToken ct)
    {
        var cartId = ReadCartId();
        if (cartId is null) return NotFound();

        var item = await db.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.Sku == sku, ct);
        if (item is null) return NotFound();

        var available = await db.Stocks.AsNoTracking()
            .Where(s => s.Sku == sku)
            .Select(s => s.QuantityOnHand - s.QuantityReserved)
            .FirstOrDefaultAsync(ct);
        if (request.Quantity > available)
        {
            return Conflict(new ProblemDetails { Title = "Not enough stock", Detail = $"Only {available} available for {sku}." });
        }

        item.Quantity = request.Quantity;
        await db.Carts.Where(c => c.Id == cartId).ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        return await BuildAsync(cartId.Value, ct);
    }

    [HttpDelete("items/{sku}")]
    public async Task<ActionResult<CartResponse>> Remove(string sku, CancellationToken ct)
    {
        var cartId = ReadCartId();
        if (cartId is null) return EmptyCart;

        await db.CartItems.Where(i => i.CartId == cartId && i.Sku == sku).ExecuteDeleteAsync(ct);
        return await BuildAsync(cartId.Value, ct);
    }
}
