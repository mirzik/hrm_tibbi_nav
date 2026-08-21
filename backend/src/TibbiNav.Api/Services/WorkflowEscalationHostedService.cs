using TibbiNav.Application.Workflow;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Services;

/// <summary>
/// Раздел 68: периодический обход просроченных (SLA) шагов workflow —
/// продакшен-аналог ручного POST /workflow/instances/escalations/process.
/// Простой таймер, а не полноценный job-scheduler (Hangfire/Quartz) — для
/// одного relatively low-frequency обхода в рамках модульного монолита
/// (раздел 76) этого достаточно; отдельная очередь не оправдана.
/// </summary>
public sealed class WorkflowEscalationHostedService(
    IServiceScopeFactory scopeFactory, ILogger<WorkflowEscalationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<WorkflowEngine>();
                var processed = await engine.ProcessEscalationsAsync(stoppingToken);
                if (processed > 0)
                    logger.LogInformation("Workflow escalation sweep: обработано просроченных шагов: {Count}", processed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Workflow escalation sweep завершился с ошибкой (пропущен, следующий — через {Interval})", Interval);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
