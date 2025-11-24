using BizLogicSeed.Domain;
using BizLogicSeed.Pipeline;
using Xunit;

namespace BizLogicSeed.Tests;

public class PipelineTests
{
    [Fact]
    public async Task Pipeline_Should_Compensate_On_Failure()
    {
        var order = new Order
        {
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(10, "EUR"), Cost = new Money(5, "EUR") } }
        };
        var ctx = new CheckoutContext { Order = order };

        var inv = new FakeInventoryService(fail:false);
        var pay = new FakePaymentService(fail:true); // 故意让支付失败
        var invoice = new FakeInvoiceService();

        var orchestrator = new PipelineOrchestrator<CheckoutContext>(new IPipelineStep<CheckoutContext>[]
        {
            new ValidateStockStep(),
            new ReserveInventoryStep(inv),
            new ChargePaymentStep(pay),
            new GenerateInvoiceStep(invoice)
        });

        var result = await orchestrator.RunAsync(ctx, CancellationToken.None);


        Assert.True(ctx.InventoryReserved);
        Assert.False(ctx.PaymentCaptured);
        Assert.True(inv.Released);
        Assert.Equal(3, ctx.Log.Count);
        Assert.Contains("Execute: ValidateStockStep", ctx.Log[0]);
        Assert.Contains("Execute: ReserveInventoryStep", ctx.Log[1]);
        Assert.Contains("Compensate: ReserveInventoryStep", ctx.Log[2]);
    }

    [Fact]
    public async Task Pipeline_Should_Complete_Successfully_When_All_Steps_Pass()
    {
        var order = new Order
        {
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(10, "EUR"), Cost = new Money(5, "EUR") } }
        };
        var ctx = new CheckoutContext { Order = order };

        var inv = new FakeInventoryService(fail: false);
        var pay = new FakePaymentService(fail: false);
        var invoice = new FakeInvoiceService();

        var orchestrator = new PipelineOrchestrator<CheckoutContext>(new IPipelineStep<CheckoutContext>[]
        {
            new ValidateStockStep(),
            new ReserveInventoryStep(inv),
            new ChargePaymentStep(pay),
            new GenerateInvoiceStep(invoice)
        });

        var result = await orchestrator.RunAsync(ctx, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.CompensatedSteps);
        Assert.Equal(4, ctx.Log.Count);
        Assert.True(ctx.StockValidated);
        Assert.True(ctx.InventoryReserved);
        Assert.True(ctx.PaymentCaptured);
        Assert.True(ctx.InvoiceGenerated);
        Assert.NotNull(ctx.InvoiceId);
    }

    [Fact]
    public async Task Pipeline_Should_Handle_Cancellation()
    {
        var order = new Order
        {
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(10, "EUR"), Cost = new Money(5, "EUR") } }
        };
        var ctx = new CheckoutContext { Order = order };

        var inv = new FakeInventoryService(fail: false);
        var pay = new FakePaymentService(fail: false);
        var invoice = new FakeInvoiceService();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var orchestrator = new PipelineOrchestrator<CheckoutContext>(new IPipelineStep<CheckoutContext>[]
        {
            new ValidateStockStep(),
            new ReserveInventoryStep(inv),
            new ChargePaymentStep(pay),
            new GenerateInvoiceStep(invoice)
        });

        var result = await orchestrator.RunAsync(ctx, cts.Token);

        Assert.False(result.Success);
        Assert.IsType<OperationCanceledException>(result.Error);
        Assert.Empty(result.CompensatedSteps);
        Assert.Empty(ctx.Log);
        Assert.False(ctx.StockValidated);
        Assert.False(ctx.InventoryReserved);
        Assert.False(ctx.PaymentCaptured);
        Assert.False(ctx.InvoiceGenerated);
    }

    [Fact]
    public async Task Pipeline_Should_Compensate_All_Steps_When_Last_Step_Fails()
    {
        var order = new Order
        {
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(10, "EUR"), Cost = new Money(5, "EUR") } }
        };
        var ctx = new CheckoutContext { Order = order };

        var inv = new FakeInventoryService(fail: false);
        var pay = new FakePaymentService(fail: false);
        var invoice = new FakeInvoiceService(failCreate: true);

        var orchestrator = new PipelineOrchestrator<CheckoutContext>(new IPipelineStep<CheckoutContext>[]
        {
            new ValidateStockStep(),
            new ReserveInventoryStep(inv),
            new ChargePaymentStep(pay),
            new GenerateInvoiceStep(invoice)
        });

        var result = await orchestrator.RunAsync(ctx, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(3, result.CompensatedSteps.Count);
        Assert.Equal(3, result.ExecutedSteps.Count); // GenerateInvoiceStep fails before it's added to executed steps
        Assert.Contains("ChargePaymentStep", result.CompensatedSteps);
        Assert.Contains("ReserveInventoryStep", result.CompensatedSteps);
        Assert.Contains("ValidateStockStep", result.CompensatedSteps); // ValidateStockStep is in compensated steps but has no logic
    }

    [Fact]
    public async Task ChargePaymentStep_Should_Be_Idempotent()
    {
        var order = new Order
        {
            Items = new List<LineItem> { new LineItem { Sku = "A", Quantity = 1, UnitPrice = new Money(10, "EUR"), Cost = new Money(5, "EUR") } }
        };
        var ctx = new CheckoutContext { Order = order };

        var pay = new FakePaymentService(fail: false);
        var step = new ChargePaymentStep(pay);

        // First successful execution
        await step.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(ctx.PaymentCaptured);
        Assert.Equal(1, pay.CaptureCount);

        // Second execution should not throw and should not change state
        await step.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(ctx.PaymentCaptured);
        Assert.Equal(1, pay.CaptureCount); // Capture should only be called once
        Assert.Contains("Already captured, skipping", ctx.Log[1]);
    }

    private sealed class FakeInventoryService : IInventoryService
    {
        private readonly bool _fail;
        public bool Reserved { get; private set; }
        public bool Released { get; private set; }
        public FakeInventoryService(bool fail) => _fail = fail;
        public Task ReserveAsync(Order order, CancellationToken ct)
        {
            if (_fail) throw new InvalidOperationException("reserve failed");
            Reserved = true; return Task.CompletedTask;
        }
        public Task ReleaseAsync(Order order, CancellationToken ct) { Released = true; return Task.CompletedTask; }
    }

    private sealed class FakePaymentService : IPaymentService
    {
        private readonly bool _fail;
        public int CaptureCount { get; private set; }
        public FakePaymentService(bool fail) => _fail = fail;
        public Task CaptureAsync(Order order, CancellationToken ct)
        {
            if (_fail) throw new TimeoutException("payment timeout");
            CaptureCount++;
            return Task.CompletedTask;
        }
        public Task RefundAsync(Order order, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeInvoiceService : IInvoiceService
    {
        private readonly bool _failCreate;
        public FakeInvoiceService(bool failCreate = false) => _failCreate = failCreate;
        public Task<string> CreateAsync(Order order, CancellationToken ct)
        {
            if (_failCreate) throw new InvalidOperationException("invoice creation failed");
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }
        public Task VoidAsync(string invoiceId, CancellationToken ct) => Task.CompletedTask;
    }
}
