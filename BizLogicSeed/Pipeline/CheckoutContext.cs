using BizLogicSeed.Domain;

namespace BizLogicSeed.Pipeline;

public sealed class CheckoutContext
{
    public Domain.Order Order { get; init; } = new();
    public bool StockValidated { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentCaptured { get; set; }
    public bool InvoiceGenerated { get; set; }
    public string? InvoiceId { get; set; }

    // 模拟外部依赖结果
    public List<string> Log { get; } = new();
}
