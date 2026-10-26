using System.ComponentModel.DataAnnotations;
using Stock.Api.Domain;

namespace Stock.Api.Dtos;

public record StockResponse(string Sku, string ProductName, int QuantityOnHand, int QuantityReserved, int Available);

public record AdjustStockRequest(
    [Range(-1_000_000, 1_000_000)] int Change,
    StockMovementReason Reason,
    [StringLength(500)] string? Note);

public record StockMovementResponse(long Id, string Sku, int Change, StockMovementReason Reason, long? RefOrderId, string? Note, DateTime CreatedAt);
