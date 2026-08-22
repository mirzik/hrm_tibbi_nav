using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.PerformanceReviews;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateIdpGoalRequest(string Description, DateOnly TargetDate, string? Comment);
public record CreateDevelopmentPlanRequest(Guid EmployeeId, Guid? ReviewParticipantId, List<CreateIdpGoalRequest> Goals);
public record SetGoalStatusRequest(IdpGoalStatus Status);

/// <summary>
/// Раздел 47 ТЗ: Individual Development Plan — простая структура (цели
/// развития/сроки/комментарии), обычно создаётся по итогам ReviewParticipant
/// (см. ReviewParticipantId), но не обязана существовать только вместе с ним.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/development-plans")]
public class DevelopmentPlansController(TibbiNavDbContext db, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("DevelopmentPlan", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var query = db.IndividualDevelopmentPlans.AsNoTracking().ApplyScope(scope).Include(p => p.Goals).AsQueryable();
        if (employeeId is not null) query = query.Where(p => p.EmployeeId == employeeId);

        var plans = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        return Ok(plans.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("DevelopmentPlan", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var plan = await GetInScopeAsync(id, ct);
        return plan is null ? NotFound() : Ok(ToDto(plan));
    }

    [HttpPost]
    [RequirePermission("DevelopmentPlan", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateDevelopmentPlanRequest req, CancellationToken ct)
    {
        if (req.Goals is null || req.Goals.Count == 0)
            return BadRequest(new { error = "В плане развития должна быть хотя бы одна цель." });

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct);
        if (employee is null) return BadRequest(new { error = "Сотрудник не найден." });

        if (req.ReviewParticipantId is { } participantId)
        {
            var participant = await db.ReviewParticipants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == participantId, ct);
            if (participant is null) return BadRequest(new { error = "Участник цикла оценки не найден." });
            if (participant.EmployeeId != req.EmployeeId)
                return BadRequest(new { error = "ReviewParticipant относится к другому сотруднику." });
        }

        var plan = new IndividualDevelopmentPlan
        {
            OrganizationId = employee.OrganizationId,
            ClinicId = employee.ClinicId,
            EmployeeId = req.EmployeeId,
            ReviewParticipantId = req.ReviewParticipantId,
        };

        foreach (var g in req.Goals)
        {
            if (string.IsNullOrWhiteSpace(g.Description)) return BadRequest(new { error = "У цели развития должно быть описание." });
            plan.Goals.Add(new IdpGoal { IndividualDevelopmentPlan = plan, Description = g.Description, TargetDate = g.TargetDate, Comment = g.Comment });
        }

        db.IndividualDevelopmentPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = plan.Id }, ToDto(plan));
    }

    [HttpPost("{id:guid}/goals/{goalId:guid}/status")]
    [RequirePermission("DevelopmentPlan", PermissionAction.Edit)]
    public async Task<IActionResult> SetGoalStatus(Guid id, Guid goalId, [FromBody] SetGoalStatusRequest req, CancellationToken ct)
    {
        var plan = await GetInScopeAsync(id, ct, tracking: true);
        if (plan is null) return NotFound();

        var goal = plan.Goals.FirstOrDefault(g => g.Id == goalId);
        if (goal is null) return NotFound();

        goal.Status = req.Status;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(plan));
    }

    private async Task<IndividualDevelopmentPlan?> GetInScopeAsync(Guid id, CancellationToken ct, bool tracking = false)
    {
        var scope = scopeAccessor.Current!;
        var query = db.IndividualDevelopmentPlans.ApplyScope(scope).Include(p => p.Goals).AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    private static object ToDto(IndividualDevelopmentPlan p) => new
    {
        p.Id,
        p.EmployeeId,
        p.ReviewParticipantId,
        p.CreatedAtUtc,
        Goals = p.Goals.OrderBy(g => g.TargetDate).Select(g => new { g.Id, g.Description, g.TargetDate, g.Comment, g.Status }),
    };
}
