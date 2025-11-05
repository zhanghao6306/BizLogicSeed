using System;
using System.Globalization;
using BizLogicSeed.Domain;
using BizLogicSeed.Algorithms;

namespace BizLogicSeed.Algorithms
{
    public record MarketContext(
        string MarketCondition,      // "normal" | "holiday" | "clearance"
        decimal InventoryLevel,      // 0.0 ~ 1.0
        decimal CompetitorPrice,     // 市场平均竞品价（与订单同币种语义）
        decimal CustomerSatisfaction // 0.0 ~ 1.0
    );

    public record DecisionResult(
        Money FinalPrice,
        decimal EffectiveTaxRate,
        decimal TotalAdjustmentPercent,
        string DecisionSummary,
        string[] KeyFactors
    );

    public static class DecisionPriceCalculator
    {
        /// <summary>
        /// 目标：在 Task D 的基础价上，叠加库存/竞品/满意度策略，并处理“连续两层冲突取较小幅度”的规则。
        /// 计算与返回值均需使用 decimal，最终金额按 ToEven 四舍五入至两位。
        /// </summary>
        public static DecisionResult CalculateOptimalPrice(
            Order order,
            MarketContext context,
            bool includeTax = true,
            string? targetCurrency = null)
        {
            // 提示：可复用 Task D 的结果作为基础价：
            // var baseRes = DynamicPriceCalculator.CalculateDynamicPrice(order, context.MarketCondition, includeTax, targetCurrency);
            // 然后依次应用库存层、竞品层、满意度层，并在“连续两层出现涨/降冲突”时，仅保留 |幅度更小| 的那个调整，舍弃另一个。
            // ……实现留空，交给智能体……
            throw new NotImplementedException("Implement in Task E.");
        }
    }
}
