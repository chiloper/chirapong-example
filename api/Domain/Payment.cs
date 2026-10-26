namespace Stock.Api.Domain;

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    /// <summary>The provider reported success but the order was no longer payable (cancelled or expired); money must be returned.</summary>
    RefundRequired = 4
}

public class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string Provider { get; set; } = null!;
    public string ProviderRef { get; set; } = null!;
    public string PaymentUrl { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Order Order { get; set; } = null!;
}

/// <summary>Every webhook event id we have processed; the primary key is what makes redelivery harmless.</summary>
public class PaymentEvent
{
    public string EventId { get; set; } = null!;
    public long PaymentId { get; set; }
    public string Type { get; set; } = null!;
    public DateTime ReceivedAt { get; set; }

    public Payment Payment { get; set; } = null!;
}
