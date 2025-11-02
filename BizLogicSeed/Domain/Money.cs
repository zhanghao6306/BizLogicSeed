namespace BizLogicSeed.Domain;

public readonly struct Money : IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Amount = amount;
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";

    public Money Add(Money other)
    {
        if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Currency mismatch");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new Money(Math.Round(Amount * factor, 4, MidpointRounding.AwayFromZero), Currency);

    public int CompareTo(Money other)
    {
        if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Currency mismatch");
        return Amount.CompareTo(other.Amount);
    }
}
