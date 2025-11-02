using BizLogicSeed.Domain;
using BizLogicSeed.Algorithms;
using Xunit;

namespace BizLogicSeed.Tests;

public class AlgorithmsTests
{
    [Fact(Skip = "实现任务 C 后去掉 Skip")]
    public void Should_Apply_Country_Tax_And_Bankers_Rounding()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(12.345m, "EUR"), Cost = new Money(8m, "EUR") } }
        };
        var total = PriceCalculator.TotalWithTax(order);
        // 银行家舍入：12.345 * 1.20 = 14.814 → 14.81（.5 进偶）
        Assert.Equal(14.81m, total.Amount);
        Assert.Contains("€", PriceCalculator.Format(total));

    }
}
