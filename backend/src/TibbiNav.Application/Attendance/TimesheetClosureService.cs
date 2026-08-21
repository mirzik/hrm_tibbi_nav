using Microsoft.EntityFrameworkCore;
using TibbiNav.Application.Workflow;
using TibbiNav.Domain.Attendance;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Attendance;

/// <summary>Раздел 36 ТЗ: запускает закрытие табеля подразделения за период —
/// создаёт TimesheetClosure и сразу же запускает для него маршрут
/// согласования (Workflow Engine, раздел 68, EntityType="TimesheetClosure").
/// Approve/Reject самого маршрута — через существующий WorkflowInstancesController
/// (переиспользуем Workflow Engine целиком, отдельных endpoint-ов не заводим).</summary>
public sealed class TimesheetClosureService(TibbiNavDbContext db, WorkflowEngine workflowEngine)
{
    public async Task<TimesheetClosure> CreateAndStartAsync(Guid departmentId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        if (periodEnd < periodStart)
            throw new InvalidOperationException("Дата окончания периода раньше даты начала.");

        var alreadyRunning = await db.TimesheetClosures.AnyAsync(c =>
            c.DepartmentId == departmentId && c.PeriodStart == periodStart && c.PeriodEnd == periodEnd
            && c.Status != TimesheetClosureStatus.Rejected, ct);
        if (alreadyRunning)
            throw new InvalidOperationException("Закрытие табеля за этот период для подразделения уже запущено или выполнено.");

        var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId, ct)
            ?? throw new KeyNotFoundException("Подразделение не найдено.");
        var clinicId = department.ClinicId
            ?? throw new InvalidOperationException("У подразделения не указана клиника — закрытие табеля по org-level подразделениям не поддерживается в этой версии.");

        var closure = new TimesheetClosure
        {
            OrganizationId = department.OrganizationId,
            ClinicId = clinicId,
            DepartmentId = departmentId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = TimesheetClosureStatus.InProgress,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.TimesheetClosures.Add(closure);
        await db.SaveChangesAsync(ct); // нужен сохранённым, чтобы WorkflowEngine мог его найти

        await workflowEngine.StartAsync("TimesheetClosure", closure.Id, ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return closure;
    }
}
