using BizLogicSeed.Domain;
using BizLogicSeed.Pipeline;
using Xunit;

namespace BizLogicSeed.Tests;

public class PipelineTests
{
    [Fact(Skip = "实现任务 B 后去掉 Skip")]
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
        public FakePaymentService(bool fail) => _fail = fail;
        public Task CaptureAsync(Order order, CancellationToken ct)
        {
            if (_fail) throw new TimeoutException("payment timeout");
            return Task.CompletedTask;
        }
        public Task RefundAsync(Order order, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeInvoiceService : IInvoiceService
    {
        public Task<string> CreateAsync(Order order, CancellationToken ct) => Task.FromResult(Guid.NewGuid().ToString("N"));
        public Task VoidAsync(string invoiceId, CancellationToken ct) => Task.CompletedTask;
    }
}
