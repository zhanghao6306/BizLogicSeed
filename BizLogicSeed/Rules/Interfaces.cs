using BizLogicSeed.Domain;

namespace BizLogicSeed.Rules;

public interface IDiscountRule
{
    /// <summary>
    /// 返回折扣系数（如 0.8 表示 8 折；1.0 表示无折扣）。
    /// 规则可叠乘，DiscountEngine 会按顺序应用。
    /// </summary>
    decimal Apply(Order order);
}

public sealed class DiscountEngine
{
    private readonly IReadOnlyList<IDiscountRule> _rules;
    private readonly decimal _capFactor;

    /// <param name="capPercent">封顶百分比（如 0.30 表示最多打 7 折）</param>
    public DiscountEngine(IEnumerable<IDiscountRule> rules, decimal capPercent = 0.30m)
    {
        _rules = rules.ToList();
        _capFactor = 1m - capPercent;
    }

    public Money Apply(Order order)
    {
        var factor = 1m;
        foreach (var r in _rules)
        {
            var f = r.Apply(order);
            if (f <= 0m || f > 1.0m) throw new InvalidOperationException($"Invalid discount factor: {f}");
            factor *= f;
        }

        // 封顶：factor 不得小于 _capFactor
        if (factor < _capFactor) factor = _capFactor;

        // GiftCard 不参与折扣：将 GiftCard 小计加回去（近似处理：先对总额打折，再加回 GiftCard 的折扣差额）
        var giftCardTotal = order.Items.Where(i => i.Category == Category.GiftCard)
                                       .Select(i => i.Subtotal)
                                       .Aggregate(new Money(0, order.Currency), (acc, x) => acc.Add(x));
        var discounted = new Money(order.Total.Amount * factor, order.Currency);
        var withoutGiftCardDiscount = new Money(discounted.Amount + giftCardTotal.Amount * (1 - factor), order.Currency);

        // 最低价保护：不得低于成本的 90%
        var minAllowed = new Money(order.TotalCost.Amount * 0.90m, order.Currency);
        if (withoutGiftCardDiscount.Amount < minAllowed.Amount)
            return minAllowed;

        return withoutGiftCardDiscount;
    }
}
