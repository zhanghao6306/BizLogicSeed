namespace BizLogicSeed.Pipeline;

public interface IPipelineStep<TContext>
{
    Task ExecuteAsync(TContext ctx, CancellationToken ct);
    Task CompensateAsync(TContext ctx);
}

public sealed class PipelineResult
{
    public bool Success { get; init; }
    public Exception? Error { get; init; }
    public IReadOnlyList<string> ExecutedSteps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CompensatedSteps { get; init; } = Array.Empty<string>();
}
