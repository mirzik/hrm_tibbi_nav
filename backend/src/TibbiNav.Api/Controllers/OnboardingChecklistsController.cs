using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Onboarding;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record StartOnboardingChecklistRequest(
    Guid OrganizationId, Guid? ClinicId, Guid EmployeeId, PersonnelCategory Category, Guid PositionId, DateOnly HireDate);

public record SetOnboardingTaskStatusRequest(OnboardingTaskStatus Status, string? Notes);

/// <summary>
/// Раздел 31-32: чеклисты адаптации нанятых сотрудников. Обычно создаются
/// автоматически из HireCandidateService (см. Application.Onboarding) —
/// эндпоинты здесь для просмотра прогресса, отметки задач и ручного запуска
/// (backfill для сотрудников, нанятых до появления модуля).
/// Scope (раздел 65) — тот же, что и для Employee: чеклист привязан к
/// конкретному сотруднику, поэтому фильтруется через тот же
/// ApplyEmployeeScope, что и EmployeesController.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/onboarding/checklists")]
public class OnboardingChecklistsController(TibbiNavDbContext db, IScopeContextAccessor scopeAccessor, OnboardingChecklistService onboardingService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("OnboardingChecklist", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] OnboardingChecklistStatus? status, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var query = db.OnboardingChecklists.AsNoTracking()
            .Include(c => c.Tasks)
            .Where(c => scopedEmployeeIds.Contains(c.EmployeeId));

        if (status is not null) query = query.Where(c => c.Status == status);

        var checklists = await query.OrderByDescending(c => c.HireDate).Take(200).ToListAsync(ct);
        return Ok(checklists.Select(ToSummaryDto));
    }

    [HttpGet("by-employee/{employeeId:guid}")]
    [RequirePermission("OnboardingChecklist", PermissionAction.View)]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var inScope = await db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).AnyAsync(e => e.Id == employeeId, ct);
        if (!inScope) return NotFound();

        var checklist = await db.OnboardingChecklists.AsNoTracking()
            .Include(c => c.Tasks)
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return checklist is null ? NotFound() : Ok(ToDetailDto(checklist));
    }

    /// <summary>Ручной запуск — backfill для сотрудников, нанятых до появления
    /// этого модуля, либо повторный запуск после того, как HR добавил
    /// недостающий шаблон.</summary>
    [HttpPost("start")]
    [RequirePermission("OnboardingChecklist", PermissionAction.Create)]
    public async Task<IActionResult> Start([FromBody] StartOnboardingChecklistRequest req, CancellationToken ct)
    {
        var checklist = await onboardingService.CreateAndSaveChecklistAsync(
            req.OrganizationId, req.ClinicId, req.EmployeeId, req.Category, req.PositionId, req.HireDate, ct);

        return checklist is null
            ? NotFound(new { error = "Не найден подходящий активный шаблон чеклиста для этой категории/должности/клиники." })
            : Ok(ToDetailDto(checklist));
    }

    [HttpPatch("tasks/{taskId:guid}")]
    [RequirePermission("OnboardingChecklist", PermissionAction.Edit)]
    public async Task<IActionResult> SetTaskStatus(Guid taskId, [FromBody] SetOnboardingTaskStatusRequest req, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        try
        {
            var task = await onboardingService.SetTaskStatusAsync(taskId, req.Status, scope.UserId, req.Notes, ct);
            return Ok(ToTaskDto(task));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static object ToSummaryDto(OnboardingChecklist c) => new
    {
        c.Id,
        c.EmployeeId,
        c.HireDate,
        c.Status,
        c.CompletedAtUtc,
        TotalTasks = c.Tasks.Count,
        DoneTasks = c.Tasks.Count(t => t.Status is OnboardingTaskStatus.Done or OnboardingTaskStatus.Skipped),
        OverdueTasks = c.Tasks.Count(IsOverdue),
    };

    private static object ToDetailDto(OnboardingChecklist c) => new
    {
        c.Id,
        c.EmployeeId,
        c.TemplateId,
        c.HireDate,
        c.Status,
        c.CompletedAtUtc,
        Tasks = c.Tasks.OrderBy(t => t.OrderIndex).Select(ToTaskDto),
    };

    private static object ToTaskDto(OnboardingChecklistTask t) => new
    {
        t.Id,
        t.Stage,
        t.Title,
        t.Description,
        t.Responsible,
        t.DueDate,
        t.OrderIndex,
        t.Status,
        t.CompletedAtUtc,
        t.CompletedByUserId,
        t.Notes,
        IsOverdue = IsOverdue(t),
    };

    private static bool IsOverdue(OnboardingChecklistTask t) =>
        t.Status is OnboardingTaskStatus.Pending or OnboardingTaskStatus.InProgress
        && t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow);
}
