namespace Stock.Api.Domain;

public class StockItem
{
    public string Sku { get; set; } = null!;
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public uint Version { get; set; }

    public int Available => QuantityOnHand - QuantityReserved;

    public Product Product { get; set; } = null!;
}
