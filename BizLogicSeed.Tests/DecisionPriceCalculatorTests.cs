using System;
using BizLogicSeed.Domain;
using BizLogicSeed.Algorithms;
using Xunit;
using static System.Math;

namespace BizLogicSeed.Tests
{
    public class DecisionPriceCalculatorTests
    {
        // 工具：银行家舍入到 2 位
        private static decimal R2(decimal v) => decimal.Round(v, 2, MidpointRounding.ToEven);

        // 构造一个简单订单：欧元、法国、单一行
        private static Order MakeOrder(decimal unitPrice, int qty, decimal costPerUnit)
        {
            return new Order
            {
                Country = "FR",
                Currency = "EUR",
                Items = new System.Collections.Generic.List<LineItem>
                {
                    new LineItem { Sku = "A", Quantity = qty, UnitPrice = new Money(unitPrice, "EUR"), Cost = new Money(costPerUnit, "EUR") }
                }
            };
        }

        /// <summary>
        /// 场景1：库存低 + 节日（holiday）+ 竞品更高
        /// 期望：在 Task D 的基础价上，库存层 +10%，竞品层 +1%（同向，不冲突，累计 +11%），满意度不变。
        /// </summary>
        [Fact]
        public void LowInventory_Holiday_CompetitorHigher_Should_Up_11_Percent_From_Base()
        {
            var order = MakeOrder(unitPrice: 100m, qty: 1, costPerUnit: 80m);

            var ctx = new MarketContext(
                MarketCondition: "holiday",
                InventoryLevel: 0.20m,    // 低库存 → +10%
                CompetitorPrice: 120m,    // 竞品更高 → +1%
                CustomerSatisfaction: 0.75m // 中性
            );

            var res = DecisionPriceCalculator.CalculateOptimalPrice(order, ctx, includeTax: true, targetCurrency: "EUR");

            // 先取 Task D 的基础价（节日：先含税再9折，且保证最低5%利润）
            var baseRes = DynamicPriceCalculator.CalculateDynamicPrice(order, "holiday", true, "EUR");
            var baseAmt = baseRes.FinalPrice.Amount;

            // 再按 Task E 规则计算期望：+10% 然后 +1%（不冲突）
            var expected = R2(R2(baseAmt * 1.10m) * 1.01m);

            Assert.Equal("EUR", res.FinalPrice.Currency);
            Assert.Equal(expected, res.FinalPrice.Amount);
            Assert.True(res.TotalAdjustmentPercent >= 0.11m - 0.0001m); // 允许极小误差
        }

        /// <summary>
        /// 场景2：库存高 + 满意度低
        /// 期望：库存层 -5%，满意度层 -5%（同向，累计 -10%）
        /// </summary>
        [Fact]
        public void HighInventory_LowSatisfaction_Should_Down_10_Percent_From_Base()
        {
            var order = MakeOrder(200m, 1, 120m);

            var ctx = new MarketContext(
                MarketCondition: "normal",
                InventoryLevel: 0.90m,    // 高库存 → -5%
                CompetitorPrice: 200m,    // 相当
                CustomerSatisfaction: 0.50m // 低满意度 → -5%
            );

            var res = DecisionPriceCalculator.CalculateOptimalPrice(order, ctx, includeTax: true, targetCurrency: "EUR");

            var baseRes = DynamicPriceCalculator.CalculateDynamicPrice(order, "normal", true, "EUR");
            var baseAmt = baseRes.FinalPrice.Amount;

            var expected = R2(R2(baseAmt * 0.95m) * 0.95m); // -5% 再 -5%

            Assert.Equal(expected, res.FinalPrice.Amount);
            Assert.Equal("EUR", res.FinalPrice.Currency);
            Assert.True(res.TotalAdjustmentPercent <= -0.10m + 0.0001m);
        }

        /// <summary>
        /// 场景3：竞品更低 + 高满意度
        /// 为避免“冲突取小”歧义，这里设置：竞品不触发（相等），仅满意度高 → +3%
        /// </summary>
        [Fact]
        public void HighSatisfaction_NoCompetitorEdge_Should_Up_3_Percent_From_Base()
        {
            var order = MakeOrder(99.99m, 2, 60m);

            var ctx = new MarketContext(
                MarketCondition: "normal",
                InventoryLevel: 0.50m,
                CompetitorPrice: 199.98m, // 与当前价同量级，不触发下调（智能体可据实现解释）
                CustomerSatisfaction: 0.95m // 高满意度 → +3%
            );

            var res = DecisionPriceCalculator.CalculateOptimalPrice(order, ctx, includeTax: true, targetCurrency: "EUR");

            var baseRes = DynamicPriceCalculator.CalculateDynamicPrice(order, "normal", true, "EUR");
            var baseAmt = baseRes.FinalPrice.Amount;

            var expected = R2(baseAmt * 1.03m);

            Assert.Equal(expected, res.FinalPrice.Amount);
            Assert.Equal("EUR", res.FinalPrice.Currency);
        }
    }
}
