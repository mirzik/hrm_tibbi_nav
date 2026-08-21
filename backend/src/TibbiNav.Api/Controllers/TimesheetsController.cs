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

public record CreateTimesheetRequest(Guid EmployeeId, DateOnly PeriodStart, DateOnly PeriodEnd);
public record TimesheetDayRequest(DateOnly Date, TimesheetDayType DayType, decimal Hours, decimal OvertimeHours, string? Comment);
public record SetTimesheetDaysRequest(List<TimesheetDayRequest> Days);
public record CorrectTimesheetDayRequest(TimesheetDayType DayType, decimal Hours, decimal OvertimeHours, string? Comment, string Reason);

/// <summary>
/// Раздел 35-36 ТЗ: табель рабочего времени. Блокировку правок определяет
/// TimesheetClosure подразделения+периода (см. TimesheetService) — не
/// собственное поле. Scope (раздел 65) — как и у Onboarding/EmployeeDocument:
/// табель привязан к конкретному сотруднику, фильтруется через тот же
/// ApplyEmployeeScope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/timesheets")]
public class TimesheetsController(TibbiNavDbContext db, TimesheetService timesheetService, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Timesheet", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, [FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var query = db.Timesheets.AsNoTracking().Where(t => scopedEmployeeIds.Contains(t.EmployeeId));
        if (employeeId is not null) query = query.Where(t => t.EmployeeId == employeeId);
        if (departmentId is not null) query = query.Where(t => t.DepartmentId == departmentId);

        var timesheets = await query.OrderByDescending(t => t.PeriodStart).Take(200).ToListAsync(ct);
        return Ok(timesheets.Select(t => new { t.Id, t.EmployeeId, t.DepartmentId, t.ClinicId, t.PeriodStart, t.PeriodEnd }));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("Timesheet", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var timesheet = await GetInScopeAsync(id, ct);
        if (timesheet is null) return NotFound();

        var isLocked = await timesheetService.IsLockedAsync(id, ct);
        return Ok(ToDetailDto(timesheet, isLocked));
    }

    [HttpPost]
    [RequirePermission("Timesheet", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTimesheetRequest req, CancellationToken ct)
    {
        try
        {
            var timesheet = await timesheetService.CreateAsync(req.EmployeeId, req.PeriodStart, req.PeriodEnd, ct);
            return CreatedAtAction(nameof(GetById), new { id = timesheet.Id },
                new { timesheet.Id, timesheet.EmployeeId, timesheet.DepartmentId, timesheet.ClinicId, timesheet.PeriodStart, timesheet.PeriodEnd });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Раздел 35: массовое проставление дней — запрещено, если период
    /// уже Locked (см. TimesheetService.SetDaysAsync).</summary>
    [HttpPut("{id:guid}/days")]
    [RequirePermission("Timesheet", PermissionAction.Edit)]
    public async Task<IActionResult> SetDays(Guid id, [FromBody] SetTimesheetDaysRequest req, CancellationToken ct)
    {
        if (!await IsInScopeAsync(id, ct)) return NotFound();

        try
        {
            var days = req.Days.Select(d => new TimesheetDayInput(d.Date, d.DayType, d.Hours, d.OvertimeHours, d.Comment)).ToList();
            await timesheetService.SetDaysAsync(id, days, ct);

            // Свежий AsNoTracking-запрос, а не повторное чтение через тот же
            // трекнутый граф, который уже использовал TimesheetService — иначе
            // Timesheet.Days оказывается затронут двумя разными Include-запросами
            // в одном DbContext, и EF на следующем SaveChanges путает Added/Modified
            // (DbUpdateConcurrencyException на новых строках).
            var isLocked = await timesheetService.IsLockedAsync(id, ct);
            var timesheet = await db.Timesheets.AsNoTracking().Include(t => t.Days).FirstAsync(t => t.Id == id, ct);
            return Ok(ToDetailDto(timesheet, isLocked));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Раздел 36: "отдельная процедура" правки после Locked — требует
    /// Permission "Approve" (более весомое действие, чем обычная правка) и
    /// обязательную причину; пишет TimesheetEditLog.</summary>
    [HttpPost("{id:guid}/days/{date}/correct")]
    [RequirePermission("Timesheet", PermissionAction.Approve)]
    public async Task<IActionResult> CorrectDay(Guid id, DateOnly date, [FromBody] CorrectTimesheetDayRequest req, CancellationToken ct)
    {
        if (!await IsInScopeAsync(id, ct)) return NotFound();

        var scope = scopeAccessor.Current!;
        try
        {
            var input = new TimesheetDayInput(date, req.DayType, req.Hours, req.OvertimeHours, req.Comment);
            var day = await timesheetService.CorrectDayAsync(id, date, input, scope.UserId, req.Reason, ct);
            return Ok(new { day.Id, day.Date, day.DayType, day.Hours, day.OvertimeHours, day.Comment });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/edit-log")]
    [RequirePermission("Timesheet", PermissionAction.View)]
    public async Task<IActionResult> GetEditLog(Guid id, CancellationToken ct)
    {
        if (!await IsInScopeAsync(id, ct)) return NotFound();

        var log = await db.TimesheetEditLogs.AsNoTracking()
            .Where(l => l.TimesheetId == id)
            .OrderByDescending(l => l.EditedAtUtc)
            .ToListAsync(ct);

        return Ok(log);
    }

    /// <summary>Только для GetById — единственное место, где нужен полный
    /// (трекнутый) граф Timesheet+Days за один запрос. Остальные экшены
    /// используют лёгкий IsInScopeAsync, чтобы не грузить Days дважды в одном
    /// DbContext вперемешку с TimesheetService (см. комментарий в SetDays).</summary>
    private async Task<Timesheet?> GetInScopeAsync(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);
        return await db.Timesheets.AsNoTracking().Include(t => t.Days)
            .FirstOrDefaultAsync(t => t.Id == id && scopedEmployeeIds.Contains(t.EmployeeId), ct);
    }

    private async Task<bool> IsInScopeAsync(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);
        return await db.Timesheets.AsNoTracking().AnyAsync(t => t.Id == id && scopedEmployeeIds.Contains(t.EmployeeId), ct);
    }

    private static object ToDetailDto(Timesheet t, bool isLocked) => new
    {
        t.Id,
        t.EmployeeId,
        t.DepartmentId,
        t.ClinicId,
        t.PeriodStart,
        t.PeriodEnd,
        IsLocked = isLocked,
        Days = t.Days.OrderBy(d => d.Date).Select(d => new { d.Id, d.Date, d.DayType, d.Hours, d.OvertimeHours, d.Comment }),
        Summary = new
        {
            TotalWorkingHours = t.Days.Sum(d => d.Hours),
            TotalOvertimeHours = t.Days.Sum(d => d.OvertimeHours),
            LeaveDays = t.Days.Count(d => d.DayType == TimesheetDayType.Leave),
            SickDays = t.Days.Count(d => d.DayType == TimesheetDayType.Sick),
            BusinessTripDays = t.Days.Count(d => d.DayType == TimesheetDayType.BusinessTrip),
        },
    };
}
