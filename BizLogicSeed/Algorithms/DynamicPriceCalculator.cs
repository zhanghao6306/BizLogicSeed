using BizLogicSeed.Domain;
using System;

namespace BizLogicSeed.Algorithms;

public static class DynamicPriceCalculator
{
    public record PriceResult(Money FinalPrice, decimal AppliedTaxRate, decimal AppliedDiscount, string StrategyNote);
    
    public static PriceResult CalculateDynamicPrice(
        Order order,
        string marketCondition,
        bool includeTax,
        string? targetCurrency = null)
    {
        // 验证市场条件
        marketCondition = marketCondition.ToLowerInvariant();
        if (marketCondition != "normal" && marketCondition != "holiday" && marketCondition != "clearance")
        {
            throw new ArgumentException("Invalid market condition. Must be 'normal', 'holiday', or 'clearance'.", nameof(marketCondition));
        }
        
        // 计算总成本
        decimal totalCost = order.TotalCost.Amount;
        
        // 初始化结果变量
        decimal appliedTaxRate = 0m;
        decimal appliedDiscount = 0m;
        string strategyNote = string.Empty;
        
        // 根据市场条件应用不同的策略
        switch (marketCondition)
        {
            case "normal":
                {
                    // 复用 PriceCalculator.Calculate() 的结果
                    var basePrice = PriceCalculator.Calculate(order, includeTax, targetCurrency);
                    appliedTaxRate = includeTax ? GetTaxRate(order.Country) : 0m;
                    appliedDiscount = 0m;
                    strategyNote = "正常市场条件，无折扣。";
                    
                    // 检查利润率
                    decimal profitMargin = CalculateProfitMargin(basePrice.Amount, totalCost);
                    if (profitMargin < 0.05m)
                    {
                        return AdjustToMinimumProfitMargin(order, basePrice.Currency, totalCost, includeTax, targetCurrency);
                    }
                    
                    return new PriceResult(basePrice, appliedTaxRate, appliedDiscount, strategyNote);
                }
            case "holiday":
                {
                    // 先加税
                    var taxedPrice = PriceCalculator.Calculate(order, includeTax: true, targetCurrency);
                    appliedTaxRate = GetTaxRate(order.Country);
                    
                    // 再打 9 折
                    decimal discountedAmount = Math.Round(taxedPrice.Amount * 0.9m, 2, MidpointRounding.ToEven);
                    appliedDiscount = 0.1m;
                    strategyNote = "节假日促销，先加税再打 9 折。";
                    
                    // 检查利润率
                    decimal profitMargin = CalculateProfitMargin(discountedAmount, totalCost);
                    if (profitMargin < 0.05m)
                    {
                        return AdjustToMinimumProfitMargin(order, taxedPrice.Currency, totalCost, includeTax: true, targetCurrency);
                    }
                    
                    return new PriceResult(new Money(discountedAmount, taxedPrice.Currency), appliedTaxRate, appliedDiscount, strategyNote);
                }
            case "clearance":
                {
                    // 税率 0
                    var noTaxPrice = PriceCalculator.Calculate(order, includeTax: false, targetCurrency);
                    appliedTaxRate = 0m;
                    
                    // 75 折
                    decimal discountedAmount = Math.Round(noTaxPrice.Amount * 0.75m, 2, MidpointRounding.ToEven);
                    appliedDiscount = 0.25m;
                    strategyNote = "清仓处理，税率 0 且 75 折。";
                    
                    // 检查利润率
                    decimal profitMargin = CalculateProfitMargin(discountedAmount, totalCost);
                    if (profitMargin < 0.05m)
                    {
                        return AdjustToMinimumProfitMargin(order, noTaxPrice.Currency, totalCost, includeTax: false, targetCurrency);
                    }
                    
                    return new PriceResult(new Money(discountedAmount, noTaxPrice.Currency), appliedTaxRate, appliedDiscount, strategyNote);
                }
            default:
                throw new InvalidOperationException("Unexpected market condition.");
        }
    }
    
    private static decimal GetTaxRate(string country)
    {
        return PriceCalculator.Tax.TryGetValue(country, out var rate) ? rate : 0m;
    }
    
    private static decimal CalculateProfitMargin(decimal revenue, decimal cost)
    {
        if (cost == 0m)
        {
            return 1m; // 成本为 0 时，利润率为 100%
        }
        
        return (revenue - cost) / cost;
    }
    
    private static PriceResult AdjustToMinimumProfitMargin(Order order, string currency, decimal totalCost, bool includeTax, string? targetCurrency)
    {
        // 计算至少需要的收入
        decimal minimumRevenue = Math.Round(totalCost * 1.05m, 2, MidpointRounding.ToEven);
        
        // 计算应用的税率
        decimal appliedTaxRate = includeTax ? GetTaxRate(order.Country) : 0m;
        
        // 计算折扣
        // 首先计算税前价格
        decimal preTaxPrice = minimumRevenue;
        if (includeTax && appliedTaxRate > 0m)
        {
            preTaxPrice = Math.Round(minimumRevenue / (1 + appliedTaxRate), 2, MidpointRounding.ToEven);
        }
        
        // 计算原始价格
        var originalPrice = PriceCalculator.Calculate(order, includeTax, targetCurrency);
        
        // 计算折扣
        decimal appliedDiscount = 0m;
        if (originalPrice.Amount > 0m)
        {
            appliedDiscount = Math.Round((originalPrice.Amount - minimumRevenue) / originalPrice.Amount, 4, MidpointRounding.ToEven);
            // 折扣可以是负数，表示提价
        }
        
        string strategyNote = "利润率低于 5%，已调整到最低 5% 利润率。";
        
        return new PriceResult(new Money(minimumRevenue, currency), appliedTaxRate, appliedDiscount, strategyNote);
    }
}