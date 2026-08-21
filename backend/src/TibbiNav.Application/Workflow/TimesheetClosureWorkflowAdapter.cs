using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Attendance;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Workflow;

/// <summary>Раздел 36, 68: маршрут закрытия табеля подразделения —
/// Department Manager (RoleInDepartment) → HR (RoleInClinic) → Accounting
/// (RoleInOrganization), см. DevSeedData. На Approved переводит
/// TimesheetClosure в Locked (единственный статус, реально блокирующий
/// правки Timesheet-ов этого Department+периода — см. TimesheetService);
/// на Rejected — в Rejected, период остаётся редактируемым, повторное
/// закрытие потребует нового TimesheetClosure.</summary>
public sealed class TimesheetClosureWorkflowAdapter(TibbiNavDbContext db) : IWorkflowEntityAdapter
{
    public string EntityType => "TimesheetClosure";

    public async Task<WorkflowEntityContext> LoadContextAsync(Guid entityId, CancellationToken ct)
    {
        var closure = await db.TimesheetClosures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId, ct)
            ?? throw new KeyNotFoundException("Закрытие табеля не найдено.");

        var fields = new Dictionary<string, string?>
        {
            ["DepartmentId"] = closure.DepartmentId.ToString(),
            ["PeriodStart"] = closure.PeriodStart.ToString("yyyy-MM-dd"),
            ["PeriodEnd"] = closure.PeriodEnd.ToString("yyyy-MM-dd"),
        };

        return new WorkflowEntityContext(closure.OrganizationId, closure.ClinicId, closure.DepartmentId, null, fields);
    }

    public async Task ApplyOutcomeAsync(Guid entityId, WorkflowOutcome outcome, CancellationToken ct)
    {
        var closure = await db.TimesheetClosures.FirstOrDefaultAsync(c => c.Id == entityId, ct)
            ?? throw new KeyNotFoundException("Закрытие табеля не найдено.");

        if (outcome == WorkflowOutcome.Approved)
        {
            closure.Status = TimesheetClosureStatus.Locked;
            closure.LockedAtUtc = DateTime.UtcNow;
        }
        else
        {
            closure.Status = TimesheetClosureStatus.Rejected;
        }
    }
}
