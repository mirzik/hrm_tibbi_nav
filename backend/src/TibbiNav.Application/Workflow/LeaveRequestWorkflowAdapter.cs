using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Core;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Workflow;

/// <summary>Раздел 37, 68: согласование заявки на отпуск руководителем
/// подразделения — SubjectEmployeeId = заявитель, шаг маршрута использует
/// WorkflowApproverStrategy.DirectManager (резолвится через текущую
/// EmploymentRecord.ManagerEmployeeId заявителя, см. WorkflowApproverResolver),
/// а не роль — у заявки нет собственной клиники, только через сотрудника.</summary>
public sealed class LeaveRequestWorkflowAdapter(TibbiNavDbContext db) : IWorkflowEntityAdapter
{
    public string EntityType => "LeaveRequest";

    public async Task<WorkflowEntityContext> LoadContextAsync(Guid entityId, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(l => l.Id == entityId, ct)
            ?? throw new KeyNotFoundException("Заявка на отпуск не найдена.");
        var employee = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == leave.EmployeeId, ct);
        var employment = await db.EmploymentRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.IsCurrent, ct);

        var fields = new Dictionary<string, string?>
        {
            ["LeaveType"] = leave.LeaveType,
            ["Days"] = leave.Days.ToString(),
            ["ClinicId"] = employee.ClinicId?.ToString(),
        };

        return new WorkflowEntityContext(employee.OrganizationId, employee.ClinicId, employment?.DepartmentId, employee.Id, fields);
    }

    public async Task ApplyOutcomeAsync(Guid entityId, WorkflowOutcome outcome, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == entityId, ct)
            ?? throw new KeyNotFoundException("Заявка на отпуск не найдена.");

        leave.Status = outcome == WorkflowOutcome.Approved ? LeaveStatus.Approved : LeaveStatus.Rejected;
    }
}
