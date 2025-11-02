namespace BizLogicSeed.Domain;

public sealed class Customer
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public CustomerStatus Status { get; init; } = CustomerStatus.Regular;
}

public sealed class LineItem
{
    public string Sku { get; init; } = "";
    public int Quantity { get; init; }
    public Money UnitPrice { get; init; } = new Money(0m, "EUR");
    public Category Category { get; init; } = Category.General;
    public Money Cost { get; init; } = new Money(0m, "EUR");
    public Money Subtotal => UnitPrice.Multiply(Quantity);
    public Money CostSubtotal => Cost.Multiply(Quantity);
}

public sealed class Order
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Country { get; init; } = "DE";
    public string Currency { get; init; } = "EUR";
    public Customer Customer { get; init; } = new Customer();
    public IReadOnlyList<LineItem> Items { get; init; } = new List<LineItem>();
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public Money Total => Items.Select(i => i.Subtotal).Aggregate(new Money(0, Currency), (acc, x) => acc.Add(x));
    public Money TotalCost => Items.Select(i => i.CostSubtotal).Aggregate(new Money(0, Currency), (acc, x) => acc.Add(x));
}
