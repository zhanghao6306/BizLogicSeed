namespace BizLogicSeed.Pipeline;

public sealed class PipelineOrchestrator<TContext>
{
    private readonly IReadOnlyList<IPipelineStep<TContext>> _steps;

    public PipelineOrchestrator(IEnumerable<IPipelineStep<TContext>> steps)
    {
        _steps = steps.ToList();
    }

    public async Task<PipelineResult> RunAsync(TContext ctx, CancellationToken ct)
    {
        var executed = new List<IPipelineStep<TContext>>();
        var executedNames = new List<string>();
        var compensatedNames = new List<string>();

        try
        {
            foreach (var step in _steps)
            {
                ct.ThrowIfCancellationRequested();
                await step.ExecuteAsync(ctx, ct);
                executed.Add(step);
                executedNames.Add(step.GetType().Name);
            }
            return new PipelineResult { Success = true, ExecutedSteps = executedNames };
        }
        catch (Exception ex)
        {
            // 逆序补偿
            foreach (var step in executed.AsEnumerable().Reverse())
            {
                try { await step.CompensateAsync(ctx); compensatedNames.Add(step.GetType().Name); }
                catch { /* 记录后继续尝试补偿 */ }
            }
            return new PipelineResult { Success = false, Error = ex, ExecutedSteps = executedNames, CompensatedSteps = compensatedNames };
        }
    }
}
