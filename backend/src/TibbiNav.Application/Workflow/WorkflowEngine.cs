using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Workflow;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Workflow;

/// <summary>
/// Раздел 68 ТЗ: движок настраиваемых маршрутов согласования — сам ничего не
/// знает о Vacancy/LeaveRequest, работает только через IWorkflowEntityAdapter
/// и данные (WorkflowDefinition/Steps/Conditions). StartAsync вызывается из
/// контроллера создания сущности (VacanciesController, LeaveRequestsController)
/// сразу после сохранения сущности, в той же транзакции — назначение первого
/// approver-а атомарно с самим созданием.
/// </summary>
public sealed class WorkflowEngine(
    TibbiNavDbContext db,
    WorkflowDefinitionSelector definitionSelector,
    IWorkflowApproverResolver approverResolver,
    IEnumerable<IWorkflowEntityAdapter> adapters)
{
    private IWorkflowEntityAdapter GetAdapter(string entityType) =>
        adapters.FirstOrDefault(a => a.EntityType == entityType)
            ?? throw new InvalidOperationException($"Нет адаптера Workflow Engine для типа сущности '{entityType}'.");

    /// <summary>Не вызывает SaveChanges — рассчитан на вызов внутри уже открытой
    /// транзакции создания сущности. Если для EntityType нет подходящего
    /// активного WorkflowDefinition (условия не совпали или маршрут не
    /// настроен) — молча возвращает null: создание сущности не блокируется
    /// отсутствием согласования, она просто не попадает в workflow.</summary>
    public async Task<WorkflowInstance?> StartAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        var adapter = GetAdapter(entityType);
        var context = await adapter.LoadContextAsync(entityId, ct);

        var definition = await definitionSelector.SelectAsync(entityType, context, ct);
        if (definition is null || definition.Steps.Count == 0) return null;

        var orderedSteps = definition.Steps.OrderBy(s => s.OrderIndex).ToList();
        var firstStep = orderedSteps[0];

        var instance = new WorkflowInstance
        {
            OrganizationId = context.OrganizationId,
            ClinicId = context.ClinicId,
            WorkflowDefinitionId = definition.Id,
            EntityType = entityType,
            EntityId = entityId,
            Status = WorkflowInstanceStatus.InProgress,
            CurrentStepIndex = firstStep.OrderIndex,
        };

        foreach (var stepDef in orderedSteps)
        {
            var isFirst = stepDef.OrderIndex == firstStep.OrderIndex;
            var eligible = isFirst
                ? await approverResolver.ResolveAsync(stepDef.ApproverStrategy, stepDef.ApproverRoleCode, context, ct)
                : [];

            instance.Steps.Add(new WorkflowStepInstance
            {
                WorkflowInstance = instance,
                StepDefinitionId = stepDef.Id,
                OrderIndex = stepDef.OrderIndex,
                Name = stepDef.Name,
                ApproverRoleCode = stepDef.ApproverRoleCode,
                AssignedApproverUserId = eligible.Count > 0 ? eligible[0] : null,
                Status = WorkflowStepStatus.Pending,
                DueAtUtc = isFirst && stepDef.SlaHours > 0 ? DateTime.UtcNow.AddHours(stepDef.SlaHours) : null,
            });
        }

        db.WorkflowInstances.Add(instance);
        return instance;
    }

    /// <summary>Раздел 68: Approve/Reject текущего (Pending) шага. actingUserId
    /// должен входить в свежерезолвленное множество eligible approvers —
    /// см. IWorkflowApproverResolver. Reject сразу завершает весь workflow
    /// (последовательное согласование: отказ на любом шаге останавливает
    /// маршрут). Approve на последнем шаге переводит instance в Approved и
    /// вызывает adapter.ApplyOutcomeAsync — здесь Vacancy.Status становится
    /// Approved.</summary>
    public async Task<WorkflowInstance> DecideAsync(
        Guid instanceId, Guid actingUserId, WorkflowOutcome outcome, string? comment, CancellationToken ct)
    {
        var (instance, step, stepDef, context) = await LoadDecisionContextAsync(instanceId, ct);

        var eligible = await approverResolver.ResolveAsync(stepDef.ApproverStrategy, stepDef.ApproverRoleCode, context, ct);
        if (!eligible.Contains(actingUserId))
            throw new UnauthorizedAccessException("Вы не входите в число согласующих этого шага.");

        await ApplyDecisionAsync(instance, step, context, outcome, actingUserId, comment, ct);
        await db.SaveChangesAsync(ct);
        return instance;
    }

    /// <summary>Раздел 68: находит просроченные (DueAtUtc в прошлом, ещё не
    /// эскалированные) Pending-шаги активных instance-ов и применяет
    /// EscalationAction соответствующего WorkflowStepDefinition. Вызывается
    /// как из WorkflowEscalationHostedService (см. Api-слой), так и вручную
    /// через POST /workflow/escalations/process.</summary>
    public async Task<int> ProcessEscalationsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var overdueSteps = await db.WorkflowStepInstances
            .Include(s => s.WorkflowInstance)
            .Where(s => s.Status == WorkflowStepStatus.Pending
                && s.DueAtUtc != null && s.DueAtUtc < now && s.EscalatedAtUtc == null
                && s.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress)
            .ToListAsync(ct);

        foreach (var step in overdueSteps)
        {
            var instance = step.WorkflowInstance;
            var definition = await db.WorkflowDefinitions.Include(d => d.Steps)
                .FirstAsync(d => d.Id == instance.WorkflowDefinitionId, ct);
            var stepDef = definition.Steps.Single(s => s.Id == step.StepDefinitionId);
            var adapter = GetAdapter(instance.EntityType);
            var context = await adapter.LoadContextAsync(instance.EntityId, ct);

            step.EscalatedAtUtc = now;

            switch (stepDef.EscalationAction)
            {
                case WorkflowEscalationAction.ReassignToRole when stepDef.EscalationRoleCode is not null:
                    var reassigned = await approverResolver.ResolveAsync(
                        WorkflowApproverStrategy.RoleInOrganization, stepDef.EscalationRoleCode, context, ct);
                    step.AssignedApproverUserId = reassigned.Count > 0 ? reassigned[0] : null;
                    step.ApproverRoleCode = stepDef.EscalationRoleCode;
                    step.Comment = AppendNote(step.Comment,
                        $"Эскалировано на роль {stepDef.EscalationRoleCode} по истечении SLA ({stepDef.SlaHours} ч).");
                    break;

                case WorkflowEscalationAction.AutoApprove:
                    await ApplyDecisionAsync(instance, step, context, WorkflowOutcome.Approved, actingUserId: null,
                        AppendNote(step.Comment, "Автоматически одобрено по истечении SLA."), ct);
                    break;

                case WorkflowEscalationAction.Notify:
                case WorkflowEscalationAction.None:
                default:
                    // Реальная отправка — Notification Engine (roadmap-пункт 9 в
                    // README), пока не реализован; шаг остаётся Pending, но
                    // помечен EscalatedAtUtc — виден в API как просроченный.
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
        return overdueSteps.Count;
    }

    /// <summary>Раздел 68: проверяет, входит ли пользователь в число
    /// согласующих текущего (Pending) шага instance-а, без мутации состояния —
    /// для "моя очередь на согласование" (см. WorkflowInstancesController.MyPending).</summary>
    public async Task<bool> IsEligibleForCurrentStepAsync(Guid instanceId, Guid userId, CancellationToken ct)
    {
        var instance = await db.WorkflowInstances.AsNoTracking().Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance is null || instance.Status != WorkflowInstanceStatus.InProgress) return false;

        var step = instance.Steps.SingleOrDefault(s => s.OrderIndex == instance.CurrentStepIndex);
        if (step is null || step.Status != WorkflowStepStatus.Pending) return false;

        var definition = await db.WorkflowDefinitions.AsNoTracking().Include(d => d.Steps)
            .FirstOrDefaultAsync(d => d.Id == instance.WorkflowDefinitionId, ct);
        var stepDef = definition?.Steps.SingleOrDefault(s => s.Id == step.StepDefinitionId);
        if (stepDef is null) return false;

        var adapter = GetAdapter(instance.EntityType);
        var context = await adapter.LoadContextAsync(instance.EntityId, ct);
        var eligible = await approverResolver.ResolveAsync(stepDef.ApproverStrategy, stepDef.ApproverRoleCode, context, ct);
        return eligible.Contains(userId);
    }

    private async Task<(WorkflowInstance Instance, WorkflowStepInstance Step, WorkflowStepDefinition StepDef, WorkflowEntityContext Context)>
        LoadDecisionContextAsync(Guid instanceId, CancellationToken ct)
    {
        var instance = await db.WorkflowInstances.Include(i => i.Steps).FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new KeyNotFoundException("Workflow не найден.");
        if (instance.Status != WorkflowInstanceStatus.InProgress)
            throw new InvalidOperationException($"Workflow уже завершён со статусом {instance.Status}.");

        var step = instance.Steps.SingleOrDefault(s => s.OrderIndex == instance.CurrentStepIndex)
            ?? throw new InvalidOperationException("Текущий шаг workflow не найден.");
        if (step.Status != WorkflowStepStatus.Pending)
            throw new InvalidOperationException("Текущий шаг уже обработан.");

        var definition = await db.WorkflowDefinitions.Include(d => d.Steps)
            .FirstAsync(d => d.Id == instance.WorkflowDefinitionId, ct);
        var stepDef = definition.Steps.Single(s => s.Id == step.StepDefinitionId);

        var adapter = GetAdapter(instance.EntityType);
        var context = await adapter.LoadContextAsync(instance.EntityId, ct);

        return (instance, step, stepDef, context);
    }

    /// <summary>Общая логика применения решения — используется и обычным
    /// DecideAsync (actingUserId задан, уже проверена eligibility), и
    /// ProcessEscalationsAsync при AutoApprove (actingUserId = null —
    /// системное решение). SaveChanges не вызывает — вызывающая сторона
    /// решает, когда сохранять (DecideAsync — сразу, ProcessEscalationsAsync —
    /// одним batch в конце обхода).</summary>
    private async Task ApplyDecisionAsync(
        WorkflowInstance instance, WorkflowStepInstance step, WorkflowEntityContext context,
        WorkflowOutcome outcome, Guid? actingUserId, string? comment, CancellationToken ct)
    {
        step.Status = outcome == WorkflowOutcome.Approved ? WorkflowStepStatus.Approved : WorkflowStepStatus.Rejected;
        step.DecidedAtUtc = DateTime.UtcNow;
        step.DecidedByUserId = actingUserId;
        if (comment is not null) step.Comment = comment;

        var adapter = GetAdapter(instance.EntityType);

        if (outcome == WorkflowOutcome.Rejected)
        {
            instance.Status = WorkflowInstanceStatus.Rejected;
            instance.CompletedAtUtc = DateTime.UtcNow;
            await adapter.ApplyOutcomeAsync(instance.EntityId, WorkflowOutcome.Rejected, ct);
            return;
        }

        var definition = await db.WorkflowDefinitions.Include(d => d.Steps)
            .FirstAsync(d => d.Id == instance.WorkflowDefinitionId, ct);
        var nextStepDef = definition.Steps.OrderBy(s => s.OrderIndex).FirstOrDefault(s => s.OrderIndex > instance.CurrentStepIndex);

        if (nextStepDef is null)
        {
            instance.Status = WorkflowInstanceStatus.Approved;
            instance.CompletedAtUtc = DateTime.UtcNow;
            await adapter.ApplyOutcomeAsync(instance.EntityId, WorkflowOutcome.Approved, ct);
            return;
        }

        instance.CurrentStepIndex = nextStepDef.OrderIndex;
        var nextStep = instance.Steps.Single(s => s.OrderIndex == nextStepDef.OrderIndex);
        if (nextStepDef.SlaHours > 0)
            nextStep.DueAtUtc = DateTime.UtcNow.AddHours(nextStepDef.SlaHours);

        var nextEligible = await approverResolver.ResolveAsync(nextStepDef.ApproverStrategy, nextStepDef.ApproverRoleCode, context, ct);
        nextStep.AssignedApproverUserId = nextEligible.Count > 0 ? nextEligible[0] : null;
    }

    private static string AppendNote(string? existing, string note) => existing is null ? note : $"{existing} | {note}";
}
