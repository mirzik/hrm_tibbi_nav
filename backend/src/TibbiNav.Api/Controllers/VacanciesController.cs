using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Recruitment;
using TibbiNav.Application.Workflow;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Recruitment;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateVacancyRequest(
    Guid OrganizationId, Guid ClinicId, Guid DepartmentId, Guid PositionId,
    int HeadcountRequested, VacancyReason Reason, Guid? ReplacingEmployeeId,
    DateOnly? DesiredStartDate, decimal? BudgetSalary, string? Requirements, VacancyPriority Priority);

[ApiController]
[Authorize]
[Route("api/v1/vacancies")]
public class VacanciesController(
    TibbiNavDbContext db, HireCandidateService hireCandidateService, WorkflowEngine workflowEngine, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Vacancy", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] VacancyStatus? status, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var q = db.Vacancies.AsNoTracking().ApplyScope(scope);
        if (status is not null) q = q.Where(v => v.Status == status);
        return Ok(await q.OrderByDescending(v => v.CreatedAtUtc).Take(200).ToListAsync(ct));
    }

    /// <summary>Раздел 14-16: заявка + проверка штатного расписания (FTE/бюджет,
    /// сама проверка вынесена в отдельный WorkforceValidationService, не включён
    /// в MVP-срез) + запуск маршрута согласования (раздел 68, Manager → HR →
    /// Finance → ChiefDoctor → GeneralDirector — см. DevSeedData) в той же
    /// транзакции: вакансия не может быть создана без назначенного первого
    /// approver-а, если для неё настроен маршрут.</summary>
    [HttpPost]
    [RequirePermission("Vacancy", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateVacancyRequest req, CancellationToken ct)
    {
        var vacancy = new Vacancy
        {
            OrganizationId = req.OrganizationId,
            ClinicId = req.ClinicId,
            DepartmentId = req.DepartmentId,
            PositionId = req.PositionId,
            HeadcountRequested = req.HeadcountRequested,
            Reason = req.Reason,
            ReplacingEmployeeId = req.ReplacingEmployeeId,
            DesiredStartDate = req.DesiredStartDate,
            BudgetSalary = req.BudgetSalary,
            Requirements = req.Requirements,
            Priority = req.Priority,
            Status = VacancyStatus.PendingApproval,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(ct); // нужен сохранённым, чтобы WorkflowEngine мог его найти

        await workflowEngine.StartAsync("Vacancy", vacancy.Id, ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return CreatedAtAction(nameof(List), new { }, vacancy);
    }

    /// <summary>Раздел 28: кнопка "Hire Candidate". Требует Edit на Vacancy —
    /// действие необратимо меняет её статус и создаёт Employee/EmploymentRecord.</summary>
    [HttpPost("applications/{candidateApplicationId:guid}/hire")]
    [RequirePermission("Vacancy", PermissionAction.Edit)]
    public async Task<IActionResult> Hire(Guid candidateApplicationId, [FromQuery] DateOnly hireDate, CancellationToken ct)
    {
        var employee = await hireCandidateService.HireAsync(candidateApplicationId, hireDate, ct);
        return Ok(new { employee.Id, employee.EmployeeCode });
    }
}
