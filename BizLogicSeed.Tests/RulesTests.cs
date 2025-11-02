using BizLogicSeed.Domain;
using BizLogicSeed.Rules;
using Xunit;

namespace BizLogicSeed.Tests;

public class RulesTests
{
    [Fact(Skip = "实现任务 A 后去掉 Skip")]
    public void Discount_Should_Apply_Bulk_And_Vip_With_Cap_And_Exclusion()
    {
        var order = new Order
        {
            Currency = "EUR",
            Customer = new Customer { Status = CustomerStatus.VIP },
            Items = new List<LineItem>
            {
                new LineItem { Sku = "A", Quantity = 2, UnitPrice = new Money(100, "EUR"), Cost = new Money(60, "EUR"), Category = Category.General },
                new LineItem { Sku = "B", Quantity = 1, UnitPrice = new Money(50, "EUR"),  Cost = new Money(30, "EUR"), Category = Category.GiftCard }
            }
        };

        var engine = new DiscountEngine(new IDiscountRule[]
        {
            new BulkQuantityRule(),
            new VipRule()
        }, capPercent: 0.30m);

        var discounted = engine.Apply(order);

        // 期望：GiftCard 不打折；其余满足 3 件 8 折，再叠加 VIP 95 折；但总折扣不超过 30%（最低 7 折）
        Assert.Equal("EUR", discounted.Currency);
        Assert.InRange(discounted.Amount, 0.70m * order.Total.Amount, order.Total.Amount);

    }
}
