# BizLogicSeed — 业务逻辑理解基准（.NET 8）

本仓库用于测试 **Code 智能体对“业务流程/规则/算法”的理解与实现能力**。通过三类任务（折扣规则、流程编排、定价算法），评估智能体是否能读懂需求、做出正确设计并交付可运行代码。

## 目录
- `BizLogicSeed/`：核心业务库（规则引擎/流程管道/算法骨架）
- `BizLogicSeed.Tests/`：xUnit 测试（部分用例 **Skip**，需要智能体实现后再解除）

## 快速开始
```bash
dotnet restore
dotnet build
dotnet test
```
> 初始状态下，测试会全部 **跳过（Skipped）**，以便你让智能体逐步实现后再打开用例。

---

# 任务 A：折扣规则（规则引擎 / 策略模式）
**目标**：实现并组合以下折扣规则，最终折扣 **封顶 30%**：
1. **满量折扣**：同一订单总数量 ≥ 3 件 → **8 折（20% off）**
2. **VIP 叠加折扣**：顾客是 VIP → 在当前折扣上再 **95 折**（叠乘，不是相加）
3. **品类排除**：`GiftCard` 类目 **不参与任何折扣**
4. **最低价保护**：折扣后总价 **不得低于所有商品成本合计的 90%**（近似模拟）

**限制**：
- 需以 **规则集合** 形式实现，可插拔（实现 `IDiscountRule` 并由 `DiscountEngine` 按顺序执行）。
- 不修改 `Order/LineItem/Customer` 的公开字段语义。

**验收**：解除 `RulesTests` 中对应用例的 Skip，全部通过。

---

# 任务 B：流程编排（带补偿的管道）
**目标**：实现一个事务性流程：
- 步骤顺序：**ValidateStock → ReserveInventory → ChargePayment → GenerateInvoice**。
- 若任一步骤失败，**需要按已执行步骤的逆序调用 Compensate**（如已预留库存需释放、已创建发票需作废等）。
- `ChargePayment` 需支持 **超时/取消**，并按配置决定是否重试。

**限制**：
- 使用 `IPipelineStep` 接口（`Execute` 与 `Compensate`）。
- 通过 `PipelineOrchestrator` 统一执行与回滚。

**验收**：解除 `PipelineTests` 中对应用例的 Skip，全部通过。

---

# 任务 C：定价算法（税/四舍五入/本地化）
**目标**：完成 `PriceCalculator`：
- 税率：DE=19%，FR=20%，CN=13%（示例）
- 四舍五入：**银行家舍入**到 2 位小数（.5 进偶）
- 币种：支持 `EUR/CNY`，并给出金额格式化结果（如 `€12.34`, `¥12.34`）

**限制**：
- `Money` 类型需要保持**不可变**，支持不同货币相加前需校验同币种。
- 保证浮点误差可控（建议使用 `decimal`）。

**验收**：解除 `AlgorithmsTests` 对应用例 Skip，全部通过。

---

# 任务 D：动态定价（节日 / 清仓 / 利润保护）

# 目标：
实现 DynamicPriceCalculator，在基础定价规则上增加市场状态（normal、holiday、clearance）逻辑，
并支持最低利润保护，验证智能体的策略推理与多层计算能力。

逻辑要求：

基础价来源于 PriceCalculator.Calculate()。

"normal"：不变；"holiday"：先含税再 9折；"clearance"：税率 0 且 75折。

若折扣后利润率低于 5%，自动调整至 5% 利润。

所有计算过程使用 decimal，并采用 MidpointRounding.ToEven 保留两位小数。

输出结构体：

public record PriceResult(
    Money FinalPrice,
    decimal AppliedTaxRate,
    decimal AppliedDiscount,
    string StrategyNote
);


**限制**：

仅允许修改 Algorithms 目录，禁止修改 Domain。

保留 PriceCalculator 原有逻辑，不得改动税率或汇率定义。

**验收**：

通过 DynamicPriceCalculatorTests 中的全部测试。

输出结果需包含：税率、折扣、最终价格、策略说明。

验证顺序：加税 → 折扣 → 利润保护 → ToEven。

# 任务 E：综合决策（库存 / 竞品 / 满意度 / 冲突规则）

# 目标：
实现 DecisionPriceCalculator，在任务 D 的基础价上叠加库存、竞品、满意度决策层，
并生成结构化报告，验证智能体的多因素推理与冲突处理能力。

逻辑要求：

调用 DynamicPriceCalculator.CalculateDynamicPrice() 获取基础价。

按以下顺序逐层应用：

库存层：库存 <0.3 → +10%；>0.8 → −5%。

竞品层：竞品价 < 当前价 → −2%；> 当前价 → +1%。

满意度层：满意度 <0.6 → −5%；>0.9 → +3%。

若连续两层调整方向相反 → 仅保留幅度较小的一方。

所有金额用 decimal + MidpointRounding.ToEven 保留两位。

输出结构体：

public record DecisionResult(
    Money FinalPrice,
    decimal EffectiveTaxRate,
    decimal TotalAdjustmentPercent,
    string DecisionSummary,
    string[] KeyFactors
);


**限制**：

禁止修改 Domain，仅在 Algorithms 目录下扩展。

所有决策逻辑需具备可解释性，不得硬编码假值。

**验收**：

通过 DecisionPriceCalculatorTests 全部测试。

报告中需列出：市场状态、税率、调整比例、主要因子。

验证顺序：基础价 → 库存层 → 竞品层 → 满意度层 → 冲突裁决 → ToEven。

输出需可复现、可追踪、可扩展。

## 评分建议（供你评 Code 智能体）
- **上下文理解**：读懂规则/流程/算法的业务含义（1-5）
- **工具调用**：.NET/集合/异步/取消/日志/测试正确使用（1-5）
- **错误修复**：能否在失败路径下提出并实现补偿/边界保护（1-5）
- **交付完整性**：是否提供实现说明、测试、清理与文档（1-5）

> 建议让智能体按 **计划→设计→实现→测试→结果** 输出，并逐个解除测试 Skip。
