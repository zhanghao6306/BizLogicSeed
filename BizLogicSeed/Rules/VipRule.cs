using BizLogicSeed.Domain;

namespace BizLogicSeed.Rules;

/// <summary>
/// VIP 叠加折扣：当前折扣基础上再 95 折（叠乘）
/// </summary>
public sealed class VipRule : IDiscountRule
{
    public decimal Apply(Order order) => order.Customer.Status == CustomerStatus.VIP ? 0.95m : 1.00m;
}
