using System;
using System.Globalization;
using System.Text;
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
            // 复用 DynamicPriceCalculator 的结果作为基础价
            var baseRes = DynamicPriceCalculator.CalculateDynamicPrice(order, context.MarketCondition, includeTax, targetCurrency);
            
            // 初始化调整参数
            decimal baseAmount = baseRes.FinalPrice.Amount;
            decimal currentAmount = baseAmount;
            decimal inventoryAdjustment = 0m;
            decimal competitorAdjustment = 0m;
            decimal satisfactionAdjustment = 0m;
            
            // 计算各层调整比例
            if (context.InventoryLevel < 0.3m)
            {
                inventoryAdjustment = 0.10m; // 涨 10%
            } 
            else if (context.InventoryLevel > 0.8m)
            {
                inventoryAdjustment = -0.05m; // 降 5%
            } 
            
            if (context.CompetitorPrice < baseAmount)
            {
                competitorAdjustment = -0.02m; // 降 2%
            }
            else if (context.CompetitorPrice > baseAmount)
            {
                competitorAdjustment = 0.01m; // 涨 1%
            } 
            
            if (context.CustomerSatisfaction < 0.6m)
            {
                satisfactionAdjustment = -0.05m; // 降 5%
            }
            else if (context.CustomerSatisfaction > 0.9m)
            {
                satisfactionAdjustment = 0.03m; // 涨 3%
            } 
            
            // 处理连续两层调整方向相反的情况
            // 检查库存和竞品调整
            if (inventoryAdjustment * competitorAdjustment < 0)
            {
                // 方向相反，只保留幅度较小的调整
                if (Math.Abs(inventoryAdjustment) < Math.Abs(competitorAdjustment))
                {
                    competitorAdjustment = 0m;
                }
                else
                {
                    inventoryAdjustment = 0m;
                }
            } 
            
            // 检查竞品和满意度调整
            if (competitorAdjustment * satisfactionAdjustment < 0)
            {
                // 方向相反，只保留幅度较小的调整
                if (Math.Abs(competitorAdjustment) < Math.Abs(satisfactionAdjustment))
                {
                    satisfactionAdjustment = 0m;
                }
                else
                {
                    competitorAdjustment = 0m;
                }
            } 
            
            // 应用调整
            currentAmount = baseAmount;
            if (inventoryAdjustment != 0m)
            {
                currentAmount = Math.Round(currentAmount * (1 + inventoryAdjustment), 2, MidpointRounding.ToEven);
            } 
            
            if (competitorAdjustment != 0m)
            {
                currentAmount = Math.Round(currentAmount * (1 + competitorAdjustment), 2, MidpointRounding.ToEven);
            } 
            
            if (satisfactionAdjustment != 0m)
            {
                currentAmount = Math.Round(currentAmount * (1 + satisfactionAdjustment), 2, MidpointRounding.ToEven);
            }
            
            // 计算总调整比例
            decimal totalAdjustmentPercent = 0m;
            if (baseAmount > 0m)
            {
                totalAdjustmentPercent = Math.Round((currentAmount - baseAmount) / baseAmount * 100, 2, MidpointRounding.ToEven);
            }
            
            // 生成决策摘要和关键因素
            var keyFactors = new List<string>();
            var summaryBuilder = new StringBuilder();
            
            summaryBuilder.AppendLine($"基础价格: {baseRes.FinalPrice.Amount:F2} {baseRes.FinalPrice.Currency}");
            summaryBuilder.AppendLine($"市场条件策略: {baseRes.StrategyNote}");
            
            if (inventoryAdjustment != 0m)
            {
                var direction = inventoryAdjustment > 0 ? "上涨" : "下降";
                var percentage = Math.Abs(inventoryAdjustment) * 100;
                summaryBuilder.AppendLine($"库存调整: {direction} {percentage:F2}% (库存水平: {context.InventoryLevel:F2})");
                keyFactors.Add($"库存{direction}{percentage:F2}%");
            }
            
            if (competitorAdjustment != 0m)
            {
                var direction = competitorAdjustment > 0 ? "上涨" : "下降";
                var percentage = Math.Abs(competitorAdjustment) * 100;
                summaryBuilder.AppendLine($"竞品价格调整: {direction} {percentage:F2}% (竞品价格: {context.CompetitorPrice:F2})");
                keyFactors.Add($"竞品价格{direction}{percentage:F2}%");
            }
            
            if (satisfactionAdjustment != 0m)
            {
                var direction = satisfactionAdjustment > 0 ? "上涨" : "下降";
                var percentage = Math.Abs(satisfactionAdjustment) * 100;
                summaryBuilder.AppendLine($"用户满意度调整: {direction} {percentage:F2}% (满意度: {context.CustomerSatisfaction:F2})");
                keyFactors.Add($"用户满意度{direction}{percentage:F2}%");
            }
            
            summaryBuilder.AppendLine($"最终价格: {currentAmount:F2} {baseRes.FinalPrice.Currency}");
            summaryBuilder.AppendLine($"总调整比例: {totalAdjustmentPercent:F2}%");
            
            // 创建最终结果
            return new DecisionResult(
                new Money(currentAmount, baseRes.FinalPrice.Currency),
                baseRes.AppliedTaxRate,
                totalAdjustmentPercent,
                summaryBuilder.ToString().Trim(),
                keyFactors.ToArray()
            );
        }
    }
}
