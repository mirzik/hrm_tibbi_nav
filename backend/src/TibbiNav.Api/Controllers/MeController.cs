using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Attendance;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Documents;
using TibbiNav.Application.PerformanceReviews;
using TibbiNav.Application.ServiceDesk;
using TibbiNav.Application.Workflow;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.PerformanceReviews;
using TibbiNav.Domain.ServiceDesk;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateMyLeaveRequestRequest(string LeaveType, DateOnly StartDate, DateOnly EndDate, int Days, string? Comment);
public record CreateMyTicketRequest(TicketCategory Category, string Subject, string Description);
public record AddMyTicketCommentRequest(string Text);
public record SubmitMyReviewRequest(decimal Score, string? Strengths, string? AreasForImprovement, string? Comments, List<Guid>? KpiAssignmentIds);

/// <summary>
/// Раздел 53 ТЗ: Employee Self Service — сотрудник видит и создаёт только
/// СВОИ данные. В отличие от остальных контроллеров (которые полагаются на
/// generic ApplyEmployeeScope и могут в принципе получить более широкий
/// Scope в зависимости от роли), здесь EmployeeId всегда жёстко берётся из
/// scope.EmployeeId (свой профиль вызывающего пользователя, резолвится
/// ScopeContextResolver-ом из AppUser.EmployeeId) — /me/* физически не может
/// отдать чужие данные, даже если у роли когда-нибудь ошибочно расширят Scope.
/// Единый Permission "SelfService" на весь модуль — это про "у вас вообще
/// есть свой профиль", а не про то, что именно вы вправе увидеть (это уже
/// гарантировано жёстким EmployeeId-фильтром).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeController(
    TibbiNavDbContext db,
    IScopeContextAccessor scopeAccessor,
    WorkflowEngine workflowEngine,
    LeaveConflictChecker conflictChecker,
    LeaveBalanceCalculator balanceCalculator,
    DocumentGeneratorService documentGeneratorService,
    TicketService ticketService,
    PerformanceReviewService performanceReviewService) : ControllerBase
{
    [HttpGet("profile")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return NotFound(new { error = "Профиль сотрудника не найден." });

        return Ok(new
        {
            employee.Id,
            employee.EmployeeCode,
            employee.FullName,
            employee.PhotoUrl,
            employee.Gender,
            employee.DateOfBirth,
            employee.Citizenship,
            employee.Phone,
            employee.PersonalEmail,
            employee.CorporateEmail,
            employee.Address,
            employee.EmergencyContact,
            employee.Status,
            employee.ClinicId,
        });
    }

    [HttpGet("employment")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetEmployment(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var employment = await db.EmploymentRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.IsCurrent, ct);
        if (employment is null) return NotFound(new { error = "Текущая запись трудоустройства не найдена." });

        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == employment.DepartmentId, ct);
        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == employment.PositionId, ct);
        var manager = employment.ManagerEmployeeId is null
            ? null
            : await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employment.ManagerEmployeeId, ct);

        return Ok(new
        {
            employment.Id,
            DepartmentName = department?.Name,
            PositionTitle = position?.Title,
            ManagerFullName = manager?.FullName,
            employment.Fte,
            employment.EmploymentType,
            employment.HireDate,
            employment.ProbationEndDate,
            employment.WorkSchedule,
            employment.BaseSalary,
        });
    }

    [HttpGet("timesheets")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetTimesheets(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var timesheets = await db.Timesheets.AsNoTracking()
            .Where(t => t.EmployeeId == employeeId)
            .OrderByDescending(t => t.PeriodStart)
            .Select(t => new { t.Id, t.PeriodStart, t.PeriodEnd })
            .ToListAsync(ct);

        return Ok(timesheets);
    }

    [HttpGet("timesheets/{id:guid}")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetTimesheet(Guid id, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var timesheet = await db.Timesheets.AsNoTracking().Include(t => t.Days)
            .FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId, ct);
        if (timesheet is null) return NotFound();

        return Ok(new
        {
            timesheet.Id,
            timesheet.PeriodStart,
            timesheet.PeriodEnd,
            Days = timesheet.Days.OrderBy(d => d.Date).Select(d => new { d.Date, d.DayType, d.Hours, d.OvertimeHours, d.Comment }),
            Summary = new
            {
                TotalWorkingHours = timesheet.Days.Sum(d => d.Hours),
                TotalOvertimeHours = timesheet.Days.Sum(d => d.OvertimeHours),
            },
        });
    }

    [HttpGet("leave-requests")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetLeaveRequests(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(requests);
    }

    [HttpGet("leave-balance")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetLeaveBalance([FromQuery] int? year, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var balance = await balanceCalculator.CalculateAsync(employeeId, year ?? DateTime.UtcNow.Year, ct);
        return Ok(balance);
    }

    /// <summary>Раздел 53: заявка на отпуск "от своего имени" — EmployeeId
    /// всегда берётся из своего профиля, не из тела запроса. Прогоняет тот же
    /// Leave conflict engine (раздел 39) и запускает тот же Workflow Engine
    /// (раздел 68, раздел 37), что и HR-эндпоинт LeaveRequestsController.Create.</summary>
    [HttpPost("leave-requests")]
    [RequirePermission("SelfService", PermissionAction.Create)]
    public async Task<IActionResult> CreateLeaveRequest([FromBody] CreateMyLeaveRequestRequest req, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;
        if (req.EndDate < req.StartDate)
            return BadRequest(new { error = "Дата окончания не может быть раньше даты начала." });

        var conflictWarning = await conflictChecker.CheckAsync(employeeId, req.StartDate, req.EndDate, ct);

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
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

        return Ok(new { LeaveRequest = leaveRequest, ConflictWarning = conflictWarning });
    }

    [HttpGet("credentials")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetCredentials(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var credentials = await db.MedicalCredentials.AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderBy(c => c.ExpiryDate)
            .Select(c => new { c.Id, c.Type, c.Title, c.IssuingAuthority, c.IssueDate, c.ExpiryDate, c.Status })
            .ToListAsync(ct);

        return Ok(credentials);
    }

    [HttpGet("documents")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var documents = await db.EmployeeDocuments.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.GeneratedAtUtc)
            .Select(d => new { d.Id, d.DocumentType, d.Title, d.Status, d.GeneratedAtUtc })
            .ToListAsync(ct);

        return Ok(documents);
    }

    [HttpGet("documents/{id:guid}/file")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> DownloadDocument(Guid id, [FromQuery] string format = "docx", CancellationToken ct = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var owned = await db.EmployeeDocuments.AsNoTracking().AnyAsync(d => d.Id == id && d.EmployeeId == employeeId, ct);
        if (!owned) return NotFound();

        try
        {
            var (content, fileName, contentType) = await documentGeneratorService.GetFileAsync(id, format, ct);
            return File(content, contentType, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("tickets")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetTickets(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.EmployeeId == employeeId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new { t.Id, t.Category, t.Subject, t.Status, t.CreatedAtUtc })
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpGet("tickets/{id:guid}")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetTicket(Guid id, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var ticket = await db.Tickets.AsNoTracking().Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId, ct);
        if (ticket is null) return NotFound();

        return Ok(ToTicketDetailDto(ticket));
    }

    [HttpPost("tickets")]
    [RequirePermission("SelfService", PermissionAction.Create)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateMyTicketRequest req, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        try
        {
            var ticket = await ticketService.CreateAsync(employeeId, req.Category, req.Subject, req.Description, ct);
            return Ok(new { ticket.Id, ticket.Category, ticket.Subject, ticket.Status, ticket.CreatedAtUtc });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Раздел 53-54: сотрудник комментирует только свой тикет,
    /// IsInternal всегда false — внутренние заметки HR ему не видны и он не
    /// может их создавать.</summary>
    [HttpPost("tickets/{id:guid}/comments")]
    [RequirePermission("SelfService", PermissionAction.Create)]
    public async Task<IActionResult> AddTicketComment(Guid id, [FromBody] AddMyTicketCommentRequest req, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var owned = await db.Tickets.AsNoTracking().AnyAsync(t => t.Id == id && t.EmployeeId == employeeId, ct);
        if (!owned) return NotFound();

        try
        {
            var scope = scopeAccessor.Current!;
            var comment = await ticketService.AddCommentAsync(id, scope.UserId, req.Text, isInternal: false, ct);
            return Ok(new { comment.Id, comment.Text, comment.CreatedAtUtc });
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

    /// <summary>Раздел 47: оценки, которые Я должен подать как оценщик (Self —
    /// про себя же, Manager/Peer/Subordinate — про других) — не путать со
    /// списком того, как оценивают МЕНЯ (для этого — GetMyReviewResults).</summary>
    [HttpGet("reviews")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetReviewsToSubmit(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var assignments = await db.ReviewAssignments.AsNoTracking()
            .Include(a => a.ReviewParticipant).ThenInclude(p => p.ReviewCycle)
            .Where(a => a.ReviewerId == employeeId)
            .OrderByDescending(a => a.ReviewParticipant.ReviewCycle.PeriodStart)
            .Select(a => new
            {
                a.Id,
                a.ReviewerRole,
                a.Status,
                a.Score,
                SubjectEmployeeId = a.ReviewParticipant.EmployeeId,
                ReviewCycleName = a.ReviewParticipant.ReviewCycle.Name,
                ReviewCycleId = a.ReviewParticipant.ReviewCycleId,
            })
            .ToListAsync(ct);

        return Ok(assignments);
    }

    [HttpPost("reviews/{id:guid}/submit")]
    [RequirePermission("SelfService", PermissionAction.Create)]
    public async Task<IActionResult> SubmitReview(Guid id, [FromBody] SubmitMyReviewRequest req, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        try
        {
            var assignment = await performanceReviewService.SubmitReviewAsync(
                id, employeeId, req.Score, req.Strengths, req.AreasForImprovement, req.Comments, req.KpiAssignmentIds, ct);
            return Ok(new { assignment.Id, assignment.Status, assignment.Score, assignment.SubmittedAtUtc });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // "Эта оценка назначена не вам" — тоже 404, а не 403, не подтверждаем чужой assignmentId (см. Tickets).
            return ex.Message.Contains("назначена не вам") ? NotFound() : BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Мои планы развития (IDP) — обычно рождаются из результатов
    /// цикла оценки, см. DevelopmentPlansController.</summary>
    [HttpGet("development-plans")]
    [RequirePermission("SelfService", PermissionAction.View)]
    public async Task<IActionResult> GetMyDevelopmentPlans(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error)) return error;

        var plans = await db.IndividualDevelopmentPlans.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId)
            .Include(p => p.Goals)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(plans.Select(p => new
        {
            p.Id,
            p.CreatedAtUtc,
            Goals = p.Goals.OrderBy(g => g.TargetDate).Select(g => new { g.Id, g.Description, g.TargetDate, g.Comment, g.Status }),
        }));
    }

    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult error)
    {
        var scope = scopeAccessor.Current!;
        if (scope.EmployeeId is { } id)
        {
            employeeId = id;
            error = null!;
            return true;
        }

        employeeId = default;
        error = BadRequest(new { error = "Ваша учётная запись не привязана к профилю сотрудника — self service недоступен." });
        return false;
    }

    private static object ToTicketDetailDto(Ticket t) => new
    {
        t.Id,
        t.Category,
        t.Subject,
        t.Description,
        t.Status,
        t.CreatedAtUtc,
        t.ResolvedAtUtc,
        t.ClosedAtUtc,
        // Внутренние HR-заметки (IsInternal) сотруднику не показываем.
        Comments = t.Comments.Where(c => !c.IsInternal).OrderBy(c => c.CreatedAtUtc)
            .Select(c => new { c.Id, c.AuthorUserId, c.Text, c.CreatedAtUtc }),
    };
}
