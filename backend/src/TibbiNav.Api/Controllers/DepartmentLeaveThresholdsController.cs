using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Attendance;
using TibbiNav.Domain.Attendance;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record SetDepartmentLeaveThresholdRequest(int MaxConcurrentAbsencePercent);

/// <summary>Раздел 39 ТЗ: порог конфликта одновременных отпусков — данные,
/// не хардкод. Подразделение без своей строки использует
/// LeaveConflictChecker.DefaultThresholdPercent.</summary>
[ApiController]
[Authorize]
[Route("api/v1/leave-thresholds")]
public class DepartmentLeaveThresholdsController(TibbiNavDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("DepartmentLeaveThreshold", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var query = db.DepartmentLeaveThresholds.AsNoTracking().AsQueryable();
        if (departmentId is not null) query = query.Where(t => t.DepartmentId == departmentId);

        var thresholds = await query.ToListAsync(ct);
        return Ok(thresholds.Select(t => new { t.DepartmentId, t.MaxConcurrentAbsencePercent }));
    }

    [HttpPut("{departmentId:guid}")]
    [RequirePermission("DepartmentLeaveThreshold", PermissionAction.Edit)]
    public async Task<IActionResult> Set(Guid departmentId, [FromBody] SetDepartmentLeaveThresholdRequest req, CancellationToken ct)
    {
        if (req.MaxConcurrentAbsencePercent is < 1 or > 100)
            return BadRequest(new { error = "Порог должен быть в диапазоне 1-100." });

        var departmentExists = await db.Departments.AsNoTracking().AnyAsync(d => d.Id == departmentId, ct);
        if (!departmentExists) return NotFound(new { error = "Подразделение не найдено." });

        var threshold = await db.DepartmentLeaveThresholds.FirstOrDefaultAsync(t => t.DepartmentId == departmentId, ct);
        if (threshold is null)
        {
            threshold = new DepartmentLeaveThreshold { DepartmentId = departmentId };
            db.DepartmentLeaveThresholds.Add(threshold);
        }
        threshold.MaxConcurrentAbsencePercent = req.MaxConcurrentAbsencePercent;

        await db.SaveChangesAsync(ct);
        return Ok(new { threshold.DepartmentId, threshold.MaxConcurrentAbsencePercent });
    }
}
