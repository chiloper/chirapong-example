using System.ComponentModel.DataAnnotations;

namespace Stock.Api.Dtos;

public record ProductResponse(
    int Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? Category,
    string? ImageUrl,
    bool IsActive,
    int QuantityOnHand,
    int QuantityReserved,
    int Available,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateProductRequest(
    [Required, StringLength(50, MinimumLength = 1), RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Sku may contain only letters, digits, '.', '_' and '-'.")]
    string Sku,
    [Required, StringLength(255, MinimumLength = 1)] string Name,
    [StringLength(2000)] string? Description,
    [Range(0, 9999999999.99)] decimal Price,
    [StringLength(100)] string? Category,
    [StringLength(500)] string? ImageUrl,
    [Range(0, int.MaxValue)] int InitialQuantity = 0);

public record UpdateProductRequest(
    [Required, StringLength(255, MinimumLength = 1)] string Name,
    [StringLength(2000)] string? Description,
    [Range(0, 9999999999.99)] decimal Price,
    [StringLength(100)] string? Category,
    [StringLength(500)] string? ImageUrl,
    bool IsActive = true);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
