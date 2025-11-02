using BizLogicSeed.Domain;
using System.Globalization;

namespace BizLogicSeed.Algorithms;

public static class PriceCalculator
{
    private static readonly Dictionary<string, decimal> Tax = new()
    {
        ["DE"] = 0.19m,
        ["FR"] = 0.20m,
        ["CN"] = 0.13m
    };

    public static Money TotalWithTax(Order order)
    {
        var subtotal = order.Total;
        var rate = Tax.TryGetValue(order.Country, out var r) ? r : 0m;
        var taxed = new Money(subtotal.Amount * (1 + rate), subtotal.Currency);

        // 银行家舍入到 2 位（.5 进偶）
        var rounded = new Money(Math.Round(taxed.Amount, 2, MidpointRounding.ToEven), taxed.Currency);
        return rounded;
    }

    public static string Format(Money money)
    {
        // 简化：根据货币选择文化
        var culture = money.Currency.ToUpperInvariant() switch
        {
            "EUR" => CultureInfo.GetCultureInfo("fr-FR"),
            "CNY" => CultureInfo.GetCultureInfo("zh-CN"),
            _ => CultureInfo.InvariantCulture
        };
        return string.Format(culture, "{0:C2}", money.Amount);
    }
}
