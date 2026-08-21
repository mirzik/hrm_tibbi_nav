using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Attendance;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Workflow;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateLeaveRequestRequest(Guid EmployeeId, string LeaveType, DateOnly StartDate, DateOnly EndDate, int Days, string? Comment);

/// <summary>
/// Раздел 37 ТЗ: заявки на отпуск. Создание запускает маршрут согласования
/// (раздел 68) — согласование руководителем подразделения (DirectManager,
/// см. LeaveRequestWorkflowAdapter + DevSeedData), в той же транзакции, и
/// прогоняет раздел 39: Leave conflict engine — не блокирует создание,
/// только возвращает предупреждение в ответе (см. LeaveConflictChecker).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/leave-requests")]
public class LeaveRequestsController(
    TibbiNavDbContext db, WorkflowEngine workflowEngine, LeaveConflictChecker conflictChecker, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("LeaveRequest", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] LeaveStatus? status, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var query = db.LeaveRequests.AsNoTracking().Where(l => scopedEmployeeIds.Contains(l.EmployeeId));
        if (status is not null) query = query.Where(l => l.Status == status);

        var requests = await query.OrderByDescending(l => l.CreatedAtUtc).Take(200).ToListAsync(ct);
        return Ok(requests);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("LeaveRequest", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var request = await db.LeaveRequests.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && scopedEmployeeIds.Contains(l.EmployeeId), ct);

        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost]
    [RequirePermission("LeaveRequest", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequestRequest req, CancellationToken ct)
    {
        if (req.EndDate < req.StartDate)
            return BadRequest(new { error = "Дата окончания не может быть раньше даты начала." });

        // Раздел 39: считается до сохранения заявки — не влияет на её создание,
        // только предупреждает вызывающую сторону.
        var conflictWarning = await conflictChecker.CheckAsync(req.EmployeeId, req.StartDate, req.EndDate, ct);

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = req.EmployeeId,
            LeaveType = req.LeaveType,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Days = req.Days,
            Comment = req.Comment,
            Status = LeaveStatus.Requested,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.LeaveRequests.Add(leaveRequest);
        await db.SaveChangesAsync(ct);

        await workflowEngine.StartAsync("LeaveRequest", leaveRequest.Id, ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = leaveRequest.Id },
            new { LeaveRequest = leaveRequest, ConflictWarning = conflictWarning });
    }
}
