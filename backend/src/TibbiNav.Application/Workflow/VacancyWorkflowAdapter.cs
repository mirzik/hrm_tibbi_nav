using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Recruitment;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Workflow;

/// <summary>Раздел 16, 68: маршрут согласования вакансии
/// (Manager → HR → Finance → ChiefDoctor → GeneralDirector, см. DevSeedData) —
/// у Vacancy нет "владельца"-сотрудника (SubjectEmployeeId = null), поэтому
/// её шаги резолвятся по роли/scope (RoleInClinic/RoleInOrganization), не по
/// DirectManager.</summary>
public sealed class VacancyWorkflowAdapter(TibbiNavDbContext db) : IWorkflowEntityAdapter
{
    public string EntityType => "Vacancy";

    public async Task<WorkflowEntityContext> LoadContextAsync(Guid entityId, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == entityId, ct)
            ?? throw new KeyNotFoundException("Вакансия не найдена.");

        var fields = new Dictionary<string, string?>
        {
            ["ClinicId"] = vacancy.ClinicId?.ToString(),
            ["DepartmentId"] = vacancy.DepartmentId.ToString(),
            ["HeadcountRequested"] = vacancy.HeadcountRequested.ToString(CultureInfo.InvariantCulture),
            ["BudgetSalary"] = vacancy.BudgetSalary?.ToString(CultureInfo.InvariantCulture),
            ["Priority"] = vacancy.Priority.ToString(),
            ["Reason"] = vacancy.Reason.ToString(),
        };

        return new WorkflowEntityContext(vacancy.OrganizationId, vacancy.ClinicId, vacancy.DepartmentId, null, fields);
    }

    public async Task ApplyOutcomeAsync(Guid entityId, WorkflowOutcome outcome, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == entityId, ct)
            ?? throw new KeyNotFoundException("Вакансия не найдена.");

        vacancy.Status = outcome == WorkflowOutcome.Approved ? VacancyStatus.Approved : VacancyStatus.Rejected;
    }
}
