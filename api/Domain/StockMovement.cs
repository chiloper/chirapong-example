namespace Stock.Api.Domain;

public enum StockMovementReason
{
    In = 1,
    Out = 2,
    Reserve = 3,
    Release = 4,
    Adjust = 5,
    /// <summary>Paid order consumed its reservation: on-hand drops by n, available is unchanged.</summary>
    Sale = 6
}

public class StockMovement
{
    public long Id { get; set; }
    public string Sku { get; set; } = null!;
    public int Change { get; set; }
    public StockMovementReason Reason { get; set; }
    public long? RefOrderId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
