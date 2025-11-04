using BizLogicSeed.Domain;
using System.Globalization;

namespace BizLogicSeed.Algorithms;

public static class PriceCalculator
{
    private static readonly Dictionary<string, decimal> Tax = new()
    {
        ["DE"] = 0.19m,
        ["FR"] = 0.20m,
        ["CN"] = 0.13m,
        ["US"] = 0.085m,
        ["JP"] = 0.10m
    };
    
    private static readonly Dictionary<(string FromCurrency, string ToCurrency), decimal> ExchangeRates = new()
    {
        { ("EUR", "CNY"), 7.8m },
        { ("CNY", "EUR"), 0.128m },
        { ("EUR", "USD"), 1.08m },
        { ("USD", "EUR"), 0.925m }
    };

    public static Money TotalWithTax(Order order)
    {
        return Calculate(order, includeTax: true);
    }
    
    public static Money Calculate(Order order, bool includeTax, string? targetCurrency = null)
    {
        var amount = order.Total.Amount;
        var currency = order.Total.Currency;
        
        // 应用税率
        if (includeTax)
        {
            var rate = Tax.TryGetValue(order.Country, out var r) ? r : 0m;
            amount *= (1 + rate);
        }
        
        // 转换货币
        if (targetCurrency != null && targetCurrency != currency)
        {
            if (ExchangeRates.TryGetValue((currency, targetCurrency), out var rate))
            {
                amount *= rate;
                currency = targetCurrency;
            }
            // 如果没有找到汇率，保持原货币
        }
        
        // 银行家舍入到 2 位（.5 进偶）
        amount = Math.Round(amount, 2, MidpointRounding.ToEven);
        
        return new Money(amount, currency);
    }

    public static string FormatWithCulture(Money money, string? culture = null)
    {
        // 如果指定了文化，则使用指定的文化
        if (!string.IsNullOrEmpty(culture))
        {
            return string.Format(CultureInfo.GetCultureInfo(culture), "{0:C2}", money.Amount);
        }
        
        // 否则根据货币选择默认文化
        var defaultCulture = money.Currency.ToUpperInvariant() switch
        {
            "EUR" => "fr-FR",
            "CNY" => "zh-CN",
            "USD" => "en-US",
            _ => "en-US"
        };
        
        return string.Format(CultureInfo.GetCultureInfo(defaultCulture), "{0:C2}", money.Amount);
    }
}
