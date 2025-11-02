using BizLogicSeed.Domain;

namespace BizLogicSeed.Pipeline;

public sealed class ValidateStockStep : IPipelineStep<CheckoutContext>
{
    public Task ExecuteAsync(CheckoutContext ctx, CancellationToken ct)
    {
        // 简化：数量为 0 视为无库存
        if (ctx.Order.Items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Invalid quantity");
        ctx.StockValidated = true;
        ctx.Log.Add("Stock validated");
        return Task.CompletedTask;
    }
    public Task CompensateAsync(CheckoutContext ctx, CancellationToken ct) => Task.CompletedTask;
}

public interface IInventoryService
{
    Task ReserveAsync(Order order, CancellationToken ct);
    Task ReleaseAsync(Order order, CancellationToken ct);
}

public interface IPaymentService
{
    Task CaptureAsync(Order order, CancellationToken ct);
    Task RefundAsync(Order order, CancellationToken ct);
}

public interface IInvoiceService
{
    Task<string> CreateAsync(Order order, CancellationToken ct);
    Task VoidAsync(string invoiceId, CancellationToken ct);
}

public sealed class ReserveInventoryStep : IPipelineStep<CheckoutContext>
{
    private readonly IInventoryService _svc;
    public ReserveInventoryStep(IInventoryService svc) => _svc = svc;
    public async Task ExecuteAsync(CheckoutContext ctx, CancellationToken ct)
    {
        await _svc.ReserveAsync(ctx.Order, ct);
        ctx.InventoryReserved = true;
        ctx.Log.Add("Inventory reserved");
    }
    public async Task CompensateAsync(CheckoutContext ctx, CancellationToken ct)
    {
        if (ctx.InventoryReserved)
            await _svc.ReleaseAsync(ctx.Order, ct);
    }
}

public sealed class ChargePaymentStep : IPipelineStep<CheckoutContext>
{
    private readonly IPaymentService _svc;
    public ChargePaymentStep(IPaymentService svc) => _svc = svc;
    public async Task ExecuteAsync(CheckoutContext ctx, CancellationToken ct)
    {
        await _svc.CaptureAsync(ctx.Order, ct);
        ctx.PaymentCaptured = true;
        ctx.Log.Add("Payment captured");
    }
    public async Task CompensateAsync(CheckoutContext ctx, CancellationToken ct)
    {
        if (ctx.PaymentCaptured)
            await _svc.RefundAsync(ctx.Order, ct);
    }
}

public sealed class GenerateInvoiceStep : IPipelineStep<CheckoutContext>
{
    private readonly IInvoiceService _svc;
    public GenerateInvoiceStep(IInvoiceService svc) => _svc = svc;
    public async Task ExecuteAsync(CheckoutContext ctx, CancellationToken ct)
    {
        var id = await _svc.CreateAsync(ctx.Order, ct);
        ctx.InvoiceGenerated = true;
        ctx.Log.Add($"Invoice created: {id}");
    }
    public async Task CompensateAsync(CheckoutContext ctx, CancellationToken ct)
    {
        if (ctx.InvoiceGenerated)
            await _svc.VoidAsync("unknown", ct); // 简化：测试中用 Fake 记录
    }
}
