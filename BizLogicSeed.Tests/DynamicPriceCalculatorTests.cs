using BizLogicSeed.Domain;
using BizLogicSeed.Algorithms;
using Xunit;

namespace BizLogicSeed.Tests;

public class DynamicPriceCalculatorTests
{
    [Fact]
    public void Should_Return_Normal_Price_When_Market_Condition_Is_Normal()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(100m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        var result = DynamicPriceCalculator.CalculateDynamicPrice(order, "normal", includeTax: true);
        
        // 计算过程：100 * 1.20 = 120 → 120
        Assert.Equal(120.00m, result.FinalPrice.Amount);
        Assert.Equal("EUR", result.FinalPrice.Currency);
        Assert.Equal(0.20m, result.AppliedTaxRate);
        Assert.Equal(0m, result.AppliedDiscount);
        Assert.Contains("正常市场条件", result.StrategyNote);
    }
    
    [Fact]
    public void Should_Apply_90Percent_Discount_When_Market_Condition_Is_Holiday()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(100m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        var result = DynamicPriceCalculator.CalculateDynamicPrice(order, "holiday", includeTax: true);
        
        // 计算过程：100 * 1.20 = 120 → 120 * 0.9 = 108
        Assert.Equal(108.00m, result.FinalPrice.Amount);
        Assert.Equal("EUR", result.FinalPrice.Currency);
        Assert.Equal(0.20m, result.AppliedTaxRate);
        Assert.Equal(0.10m, result.AppliedDiscount);
        Assert.Contains("节假日促销", result.StrategyNote);
    }
    
    [Fact]
    public void Should_Apply_75Percent_Discount_And_No_Tax_When_Market_Condition_Is_Clearance()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(120m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        var result = DynamicPriceCalculator.CalculateDynamicPrice(order, "clearance", includeTax: true);
        
        // 计算过程：120 * 0.75 = 90 → 90
        // 利润率为 (90 - 80)/80 = 0.125 → 12.5%，高于 5%，不需要调整
        Assert.Equal(90.00m, result.FinalPrice.Amount);
        Assert.Equal("EUR", result.FinalPrice.Currency);
        Assert.Equal(0m, result.AppliedTaxRate);
        Assert.Equal(0.25m, result.AppliedDiscount);
        Assert.Contains("清仓处理", result.StrategyNote);
    }
    
    [Fact]
    public void Should_Adjust_To_Minimum_Profit_Margin_When_Profit_Is_Less_Than_5_Percent()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(85m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        var result = DynamicPriceCalculator.CalculateDynamicPrice(order, "normal", includeTax: true);
        
        // 计算过程：85 * 1.20 = 102 → 利润率为 (102 - 80)/80 = 0.275 → 27.5%，不需要调整
        // 但如果 UnitPrice 是 83m，那么 83 * 1.20 = 99.6 → 利润率为 (99.6 - 80)/80 = 0.245 → 24.5%，还是不需要调整
        // 让我把 UnitPrice 设为 80m，那么 80 * 1.20 = 96 → 利润率为 (96 - 80)/80 = 0.2 → 20%，还是不需要调整
        // 让我把 UnitPrice 设为 70m，那么 70 * 1.20 = 84 → 利润率为 (84 - 80)/80 = 0.05 → 5%，刚好达到
        // 让我把 UnitPrice 设为 69m，那么 69 * 1.20 = 82.8 → 利润率为 (82.8 - 80)/80 = 0.035 → 3.5%，需要调整到 5%
        var order2 = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(69m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        var result2 = DynamicPriceCalculator.CalculateDynamicPrice(order2, "normal", includeTax: true);
        
        // 计算过程：80 * 1.05 = 84 → 需要的最低收入
        // 所以最终价格应该是 84m
        Assert.Equal(84.00m, result2.FinalPrice.Amount);
        Assert.Equal("EUR", result2.FinalPrice.Currency);
        Assert.Equal(0.20m, result2.AppliedTaxRate);
        Assert.True(result2.AppliedDiscount < 0m); // 负折扣表示提价
        Assert.Contains("利润率低于 5%", result2.StrategyNote);
    }
    
    [Fact]
    public void Should_Convert_Currency_When_Target_Currency_Is_Specified()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(100m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        var result = DynamicPriceCalculator.CalculateDynamicPrice(order, "normal", includeTax: true, targetCurrency: "CNY");
        
        // 计算过程：100 * 1.20 = 120 → 120 * 7.8 = 936
        Assert.Equal(936.00m, result.FinalPrice.Amount);
        Assert.Equal("CNY", result.FinalPrice.Currency);
        Assert.Equal(0.20m, result.AppliedTaxRate);
        Assert.Equal(0m, result.AppliedDiscount);
        Assert.Contains("正常市场条件", result.StrategyNote);
    }
    
    [Fact]
    public void Should_Throw_ArgumentException_When_Market_Condition_Is_Invalid()
    {
        var order = new Order
        {
            Country = "FR",
            Currency = "EUR",
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(100m, "EUR"), Cost = new Money(80m, "EUR") } }
        };
        
        Assert.Throws<ArgumentException>(() => DynamicPriceCalculator.CalculateDynamicPrice(order, "invalid", includeTax: true));
    }
}