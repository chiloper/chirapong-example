using System.ComponentModel.DataAnnotations;
using Stock.Api.Domain;

namespace Stock.Api.Dtos;

public record CartLineResponse(
    string Sku,
    string Name,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    int Available,
    decimal LineTotal,
    bool IsAvailable);

public record CartResponse(IReadOnlyList<CartLineResponse> Items, int ItemCount, decimal Total);

public record AddCartItemRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Sku,
    [Range(1, 999)] int Quantity = 1);

public record SetCartItemRequest([Range(1, 999)] int Quantity);

public record CheckoutRequest(
    [Required, StringLength(200, MinimumLength = 1)] string CustomerName,
    [Required, EmailAddress, StringLength(255)] string CustomerEmail,
    [StringLength(50)] string? CustomerPhone,
    [StringLength(1000)] string? ShippingAddress);

public record OrderItemResponse(string Sku, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderResponse(
    Guid Id,
    OrderStatus Status,
    decimal Total,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string? ShippingAddress,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    IReadOnlyList<OrderItemResponse> Items);

public record OrderSummaryResponse(
    Guid Id,
    OrderStatus Status,
    decimal Total,
    string CustomerName,
    string CustomerEmail,
    int ItemCount,
    DateTime CreatedAt,
    DateTime ExpiresAt);
