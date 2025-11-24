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
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Execute: ValidateStockStep");
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
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Execute: ReserveInventoryStep");
    }
    public async Task CompensateAsync(CheckoutContext ctx, CancellationToken ct)
    {
        if (ctx.InventoryReserved)
        {
            await _svc.ReleaseAsync(ctx.Order, ct);
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Compensate: ReserveInventoryStep (Released inventory)");
        }
    }
}

public sealed class ChargePaymentStep : IPipelineStep<CheckoutContext>
{
    private readonly IPaymentService _svc;
    public ChargePaymentStep(IPaymentService svc) => _svc = svc;
    public async Task ExecuteAsync(CheckoutContext ctx, CancellationToken ct)
    {
        // 幂等性：如果已经扣款成功，直接返回
        if (ctx.PaymentCaptured)
        {
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Execute: ChargePaymentStep (Already captured, skipping)");
            return;
        }
        
        await _svc.CaptureAsync(ctx.Order, ct);
        ctx.PaymentCaptured = true;
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Execute: ChargePaymentStep");
    }
    public async Task CompensateAsync(CheckoutContext ctx, CancellationToken ct)
    {
        if (ctx.PaymentCaptured)
        {
            await _svc.RefundAsync(ctx.Order, ct);
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Compensate: ChargePaymentStep (Refunded payment)");
        }
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
        ctx.InvoiceId = id;
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Execute: GenerateInvoiceStep (Invoice ID: {id})");
    }
    public async Task CompensateAsync(CheckoutContext ctx, CancellationToken ct)
    {
        if (ctx.InvoiceGenerated && ctx.InvoiceId is not null)
        {
            await _svc.VoidAsync(ctx.InvoiceId, ct);
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Compensate: GenerateInvoiceStep (Voided invoice: {ctx.InvoiceId})");
        }
    }
}
