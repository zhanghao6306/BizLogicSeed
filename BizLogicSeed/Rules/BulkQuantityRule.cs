using BizLogicSeed.Domain;

namespace BizLogicSeed.Rules;

/// <summary>
/// 满量折扣：总数量 >= 3 → 8 折
/// </summary>
public sealed class BulkQuantityRule : IDiscountRule
{
    public decimal Apply(Order order) => order.TotalQuantity >= 3 ? 0.80m : 1.00m;
}
