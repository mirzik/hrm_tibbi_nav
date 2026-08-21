using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Attendance;
using TibbiNav.Application.Authorization;
using TibbiNav.Domain.Attendance;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateTimesheetClosureRequest(Guid DepartmentId, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>
/// Раздел 36 ТЗ: закрытие табеля подразделения за период — Department Manager
/// → HR → Accounting → Locked. Approve/Reject каждого шага — через уже
/// существующий WorkflowInstancesController (POST /workflow/instances/{id}/approve|reject),
/// здесь только создание и просмотр (переиспользуем Workflow Engine целиком).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/timesheet-closures")]
public class TimesheetClosuresController(TibbiNavDbContext db, TimesheetClosureService closureService, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("TimesheetClosure", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] Guid? departmentId, [FromQuery] TimesheetClosureStatus? status, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var query = db.TimesheetClosures.AsNoTracking().ApplyScope(scope);
        if (departmentId is not null) query = query.Where(c => c.DepartmentId == departmentId);
        if (status is not null) query = query.Where(c => c.Status == status);

        var closures = await query.OrderByDescending(c => c.PeriodStart).Take(200).ToListAsync(ct);
        return Ok(closures);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("TimesheetClosure", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var closure = await db.TimesheetClosures.AsNoTracking().ApplyScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        return closure is null ? NotFound() : Ok(closure);
    }

    [HttpPost]
    [RequirePermission("TimesheetClosure", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTimesheetClosureRequest req, CancellationToken ct)
    {
        try
        {
            var closure = await closureService.CreateAndStartAsync(req.DepartmentId, req.PeriodStart, req.PeriodEnd, ct);
            return CreatedAtAction(nameof(GetById), new { id = closure.Id }, closure);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
