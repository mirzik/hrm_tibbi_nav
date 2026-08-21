using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Workflow;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Workflow;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record DecideWorkflowInstanceRequest(string? Comment);

/// <summary>
/// Раздел 68: запущенные экземпляры маршрутов согласования — просмотр
/// прогресса и Approve/Reject текущего шага (движение на следующий шаг —
/// см. WorkflowEngine.DecideAsync). Scope (раздел 65) — обычный
/// Organization/Clinic через ApplyScope, WorkflowInstance сам по себе
/// организационно-скоупед (см. Domain).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/workflow/instances")]
public class WorkflowInstancesController(TibbiNavDbContext db, WorkflowEngine workflowEngine, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("WorkflowInstance", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? entityType, [FromQuery] WorkflowInstanceStatus? status, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var query = db.WorkflowInstances.AsNoTracking().Include(i => i.Steps).ApplyScope(scope);
        if (entityType is not null) query = query.Where(i => i.EntityType == entityType);
        if (status is not null) query = query.Where(i => i.Status == status);

        var instances = await query.OrderByDescending(i => i.StartedAtUtc).Take(200).ToListAsync(ct);
        return Ok(instances.Select(ToSummaryDto));
    }

    [HttpGet("by-entity/{entityType}/{entityId:guid}")]
    [RequirePermission("WorkflowInstance", PermissionAction.View)]
    public async Task<IActionResult> GetByEntity(string entityType, Guid entityId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var instance = await db.WorkflowInstances.AsNoTracking().Include(i => i.Steps).ApplyScope(scope)
            .Where(i => i.EntityType == entityType && i.EntityId == entityId)
            .OrderByDescending(i => i.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        return instance is null ? NotFound() : Ok(ToDetailDto(instance));
    }

    /// <summary>"Моя очередь на согласование" — InProgress-инстансы, где текущий
    /// пользователь входит в число согласующих текущего шага (пересчитывается
    /// на лету, см. WorkflowEngine.IsEligibleForCurrentStepAsync).</summary>
    [HttpGet("my-pending")]
    [RequirePermission("WorkflowInstance", PermissionAction.View)]
    public async Task<IActionResult> MyPending(CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var candidates = await db.WorkflowInstances.AsNoTracking().Include(i => i.Steps).ApplyScope(scope)
            .Where(i => i.Status == WorkflowInstanceStatus.InProgress)
            .OrderBy(i => i.StartedAtUtc)
            .ToListAsync(ct);

        var mine = new List<WorkflowInstance>();
        foreach (var instance in candidates)
        {
            if (await workflowEngine.IsEligibleForCurrentStepAsync(instance.Id, scope.UserId, ct))
                mine.Add(instance);
        }

        return Ok(mine.Select(ToSummaryDto));
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission("WorkflowInstance", PermissionAction.Approve)]
    public Task<IActionResult> Approve(Guid id, [FromBody] DecideWorkflowInstanceRequest? req, CancellationToken ct) =>
        DecideAsync(id, WorkflowOutcome.Approved, req?.Comment, ct);

    [HttpPost("{id:guid}/reject")]
    [RequirePermission("WorkflowInstance", PermissionAction.Approve)]
    public Task<IActionResult> Reject(Guid id, [FromBody] DecideWorkflowInstanceRequest? req, CancellationToken ct) =>
        DecideAsync(id, WorkflowOutcome.Rejected, req?.Comment, ct);

    /// <summary>Раздел 68: ручной запуск обхода просроченных (SLA) шагов — в
    /// проде вызывается по расписанию (см. WorkflowEscalationHostedService),
    /// этот endpoint — для ops/тестирования "прямо сейчас".</summary>
    [HttpPost("escalations/process")]
    [RequirePermission("WorkflowInstance", PermissionAction.Approve)]
    public async Task<IActionResult> ProcessEscalations(CancellationToken ct)
    {
        var count = await workflowEngine.ProcessEscalationsAsync(ct);
        return Ok(new { processed = count });
    }

    private async Task<IActionResult> DecideAsync(Guid id, WorkflowOutcome outcome, string? comment, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        try
        {
            var instance = await workflowEngine.DecideAsync(id, scope.UserId, outcome, comment, ct);
            return Ok(ToDetailDto(instance));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object ToSummaryDto(WorkflowInstance i) => new
    {
        i.Id,
        i.EntityType,
        i.EntityId,
        i.Status,
        i.CurrentStepIndex,
        i.StartedAtUtc,
        i.CompletedAtUtc,
        TotalSteps = i.Steps.Count,
        CurrentStep = i.Steps.SingleOrDefault(s => s.OrderIndex == i.CurrentStepIndex) is { } cs
            ? new { cs.Name, cs.ApproverRoleCode, cs.AssignedApproverUserId, cs.DueAtUtc, cs.EscalatedAtUtc }
            : null,
    };

    private static object ToDetailDto(WorkflowInstance i) => new
    {
        i.Id,
        i.EntityType,
        i.EntityId,
        i.WorkflowDefinitionId,
        i.Status,
        i.CurrentStepIndex,
        i.StartedAtUtc,
        i.CompletedAtUtc,
        Steps = i.Steps.OrderBy(s => s.OrderIndex).Select(s => new
        {
            s.Id,
            s.OrderIndex,
            s.Name,
            s.ApproverRoleCode,
            s.AssignedApproverUserId,
            s.Status,
            s.DueAtUtc,
            s.EscalatedAtUtc,
            s.DecidedAtUtc,
            s.DecidedByUserId,
            s.Comment,
        }),
    };
}
