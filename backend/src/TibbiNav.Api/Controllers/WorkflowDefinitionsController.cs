using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Workflow;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateWorkflowConditionRequest(string FieldName, WorkflowConditionOperator Operator, string Value);

public record CreateWorkflowStepRequest(
    int OrderIndex, string Name, WorkflowApproverStrategy ApproverStrategy, string? ApproverRoleCode,
    int SlaHours, WorkflowEscalationAction EscalationAction, string? EscalationRoleCode);

public record CreateWorkflowDefinitionRequest(
    string Name, string EntityType, int Priority,
    List<CreateWorkflowConditionRequest> Conditions, List<CreateWorkflowStepRequest> Steps);

/// <summary>Раздел 68: администрирование маршрутов согласования — Trigger
/// (EntityType) + Conditions + Steps (approvers по роли/scope) + SLA/Escalation,
/// целиком данные. Подключённые EntityType на сегодня — "Vacancy" (раздел 16)
/// и "LeaveRequest" (раздел 37), см. соответствующие IWorkflowEntityAdapter.</summary>
[ApiController]
[Authorize]
[Route("api/v1/workflow/definitions")]
public class WorkflowDefinitionsController(TibbiNavDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("WorkflowDefinition", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? entityType, CancellationToken ct)
    {
        var query = db.WorkflowDefinitions.AsNoTracking().Include(d => d.Conditions).Include(d => d.Steps).AsQueryable();
        if (entityType is not null) query = query.Where(d => d.EntityType == entityType);

        var definitions = await query.OrderBy(d => d.EntityType).ThenByDescending(d => d.Priority).ToListAsync(ct);
        return Ok(definitions.Select(ToDto));
    }

    [HttpPost]
    [RequirePermission("WorkflowDefinition", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest req, CancellationToken ct)
    {
        if (req.Steps is null || req.Steps.Count == 0)
            return BadRequest(new { error = "В маршруте должен быть хотя бы один шаг." });

        var definition = new WorkflowDefinition { Name = req.Name, EntityType = req.EntityType, Priority = req.Priority };

        foreach (var c in req.Conditions ?? [])
        {
            definition.Conditions.Add(new WorkflowCondition
            {
                WorkflowDefinition = definition,
                FieldName = c.FieldName,
                Operator = c.Operator,
                Value = c.Value,
            });
        }

        foreach (var s in req.Steps)
        {
            definition.Steps.Add(new WorkflowStepDefinition
            {
                WorkflowDefinition = definition,
                OrderIndex = s.OrderIndex,
                Name = s.Name,
                ApproverStrategy = s.ApproverStrategy,
                ApproverRoleCode = s.ApproverRoleCode,
                SlaHours = s.SlaHours,
                EscalationAction = s.EscalationAction,
                EscalationRoleCode = s.EscalationRoleCode,
            });
        }

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { }, ToDto(definition));
    }

    private static object ToDto(WorkflowDefinition d) => new
    {
        d.Id,
        d.Name,
        d.EntityType,
        d.IsActive,
        d.Priority,
        Conditions = d.Conditions.Select(c => new { c.Id, c.FieldName, c.Operator, c.Value }),
        Steps = d.Steps.OrderBy(s => s.OrderIndex).Select(s => new
        {
            s.Id,
            s.OrderIndex,
            s.Name,
            s.ApproverStrategy,
            s.ApproverRoleCode,
            s.SlaHours,
            s.EscalationAction,
            s.EscalationRoleCode,
        }),
    };
}
