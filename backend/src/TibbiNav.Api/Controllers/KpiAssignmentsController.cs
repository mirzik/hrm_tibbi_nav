using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Kpi;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Kpi;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record AssignKpiRequest(Guid KpiTemplateId, Guid? EmployeeId, Guid? DepartmentId, Guid? ClinicId, DateOnly PeriodStart, decimal Target, decimal? Weight);
public record SetKpiActualRequest(decimal Actual, string? Comment);

/// <summary>
/// Раздел 45-46 ТЗ: назначение KpiTemplate на исполнителя (сотрудник/
/// подразделение/клиника/организация — см. KpiAssignmentService) и
/// проставление факта. Выполнение (%) считается на лету в ToDto.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/kpi-assignments")]
public class KpiAssignmentsController(TibbiNavDbContext db, KpiAssignmentService kpiAssignmentService, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("KpiAssignment", PermissionAction.View)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? employeeId, [FromQuery] Guid? departmentId, [FromQuery] Guid? kpiTemplateId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var query = db.KpiAssignments.AsNoTracking().Include(a => a.KpiTemplate).ApplyScope(scope);

        if (employeeId is not null) query = query.Where(a => a.EmployeeId == employeeId);
        if (departmentId is not null) query = query.Where(a => a.DepartmentId == departmentId);
        if (kpiTemplateId is not null) query = query.Where(a => a.KpiTemplateId == kpiTemplateId);

        var assignments = await query.OrderByDescending(a => a.PeriodStart).ToListAsync(ct);
        return Ok(assignments.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("KpiAssignment", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var assignment = await GetInScopeAsync(id, ct);
        return assignment is null ? NotFound() : Ok(ToDto(assignment));
    }

    [HttpPost]
    [RequirePermission("KpiAssignment", PermissionAction.Create)]
    public async Task<IActionResult> Assign([FromBody] AssignKpiRequest req, CancellationToken ct)
    {
        try
        {
            var assignment = await kpiAssignmentService.AssignAsync(
                req.KpiTemplateId, req.EmployeeId, req.DepartmentId, req.ClinicId, req.PeriodStart, req.Target, req.Weight, ct);
            await db.Entry(assignment).Reference(a => a.KpiTemplate).LoadAsync(ct);
            return CreatedAtAction(nameof(GetById), new { id = assignment.Id }, ToDto(assignment));
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

    /// <summary>"Проставление факта" — Actual можно уточнять повторно (напр.
    /// при коррекции данных источника), каждый раз обновляя ActualSetAtUtc.</summary>
    [HttpPost("{id:guid}/actual")]
    [RequirePermission("KpiAssignment", PermissionAction.Edit)]
    public async Task<IActionResult> SetActual(Guid id, [FromBody] SetKpiActualRequest req, CancellationToken ct)
    {
        if (await GetInScopeAsync(id, ct) is null) return NotFound();

        var scope = scopeAccessor.Current!;
        try
        {
            var assignment = await kpiAssignmentService.SetActualAsync(id, req.Actual, req.Comment, scope.UserId, ct);
            await db.Entry(assignment).Reference(a => a.KpiTemplate).LoadAsync(ct);
            return Ok(ToDto(assignment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private async Task<KpiAssignment?> GetInScopeAsync(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        return await db.KpiAssignments.AsNoTracking().Include(a => a.KpiTemplate).ApplyScope(scope)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    private static object ToDto(KpiAssignment a) => new
    {
        a.Id,
        a.KpiTemplateId,
        TemplateName = a.KpiTemplate.Name,
        Unit = a.KpiTemplate.Unit,
        a.EmployeeId,
        a.DepartmentId,
        a.ClinicId,
        a.PeriodType,
        a.PeriodStart,
        a.PeriodEnd,
        a.Weight,
        a.Target,
        a.Actual,
        AchievementPercent = a.Actual is null || a.Target == 0
            ? (decimal?)null
            : Math.Round(a.Actual.Value / a.Target * 100m, 1),
        a.Comment,
        a.ActualSetAtUtc,
        a.ActualSetByUserId,
    };
}
