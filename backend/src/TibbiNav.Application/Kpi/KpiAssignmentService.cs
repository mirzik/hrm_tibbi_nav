using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Kpi;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Kpi;

/// <summary>
/// Раздел 45-46 ТЗ: назначение шаблона KPI на цель (Individual → Employee,
/// Department → Department, Clinic → сама клиника, Corporate → организация
/// целиком) на конкретный период, и последующее "проставление факта".
/// Период (PeriodEnd) всегда считается от KpiTemplate.PeriodType — назначение
/// не может завести период другой периодичности, чем задумано в шаблоне.
/// </summary>
public sealed class KpiAssignmentService(TibbiNavDbContext db)
{
    public async Task<KpiAssignment> AssignAsync(
        Guid kpiTemplateId, Guid? employeeId, Guid? departmentId, Guid? clinicId,
        DateOnly periodStart, decimal target, decimal? weight, CancellationToken ct)
    {
        var template = await db.KpiTemplates.FirstOrDefaultAsync(t => t.Id == kpiTemplateId, ct)
            ?? throw new KeyNotFoundException("Шаблон KPI не найден.");
        if (!template.IsActive)
            throw new InvalidOperationException("Шаблон KPI деактивирован — назначение недоступно.");

        Guid organizationId;
        Guid? resolvedClinicId;

        switch (template.Level)
        {
            case KpiLevel.Individual:
                if (employeeId is null) throw new InvalidOperationException("Для индивидуального KPI нужен employeeId.");
                if (departmentId is not null || clinicId is not null)
                    throw new InvalidOperationException("Для индивидуального KPI указывается только employeeId.");
                var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
                    ?? throw new InvalidOperationException("Сотрудник не найден.");
                organizationId = employee.OrganizationId;
                resolvedClinicId = employee.ClinicId;
                break;

            case KpiLevel.Department:
                if (departmentId is null) throw new InvalidOperationException("Для KPI подразделения нужен departmentId.");
                if (employeeId is not null || clinicId is not null)
                    throw new InvalidOperationException("Для KPI подразделения указывается только departmentId.");
                var department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId, ct)
                    ?? throw new InvalidOperationException("Подразделение не найдено.");
                organizationId = department.OrganizationId;
                resolvedClinicId = department.ClinicId;
                break;

            case KpiLevel.Clinic:
                if (clinicId is null) throw new InvalidOperationException("Для KPI клиники нужен clinicId.");
                if (employeeId is not null || departmentId is not null)
                    throw new InvalidOperationException("Для KPI клиники указывается только clinicId.");
                var clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, ct)
                    ?? throw new InvalidOperationException("Клиника не найдена.");
                organizationId = clinic.OrganizationId;
                resolvedClinicId = clinic.Id;
                break;

            case KpiLevel.Corporate:
                if (employeeId is not null || departmentId is not null || clinicId is not null)
                    throw new InvalidOperationException("Для корпоративного KPI цель (employee/department/clinic) не указывается.");
                organizationId = template.OrganizationId;
                resolvedClinicId = null;
                break;

            default:
                throw new InvalidOperationException($"Неизвестный уровень KPI: {template.Level}.");
        }

        if (target < 0) throw new InvalidOperationException("Target не может быть отрицательным.");
        var effectiveWeight = weight ?? template.DefaultWeight;
        if (effectiveWeight < 0 || effectiveWeight > 100) throw new InvalidOperationException("Вес KPI должен быть в диапазоне 0-100.");

        var assignment = new KpiAssignment
        {
            OrganizationId = organizationId,
            ClinicId = resolvedClinicId,
            KpiTemplateId = template.Id,
            EmployeeId = employeeId,
            DepartmentId = departmentId,
            PeriodType = template.PeriodType,
            PeriodStart = periodStart,
            PeriodEnd = KpiPeriodCalculator.GetPeriodEnd(periodStart, template.PeriodType),
            Weight = effectiveWeight,
            Target = target,
        };

        db.KpiAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment;
    }

    /// <summary>"Проставление факта" — задаёт Actual (перезаписываемо, напр. при
    /// уточнении данных источника) и опциональный комментарий-пояснение.</summary>
    public async Task<KpiAssignment> SetActualAsync(Guid assignmentId, decimal actual, string? comment, Guid actualSetByUserId, CancellationToken ct)
    {
        var assignment = await db.KpiAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new KeyNotFoundException("Назначение KPI не найдено.");

        assignment.Actual = actual;
        assignment.Comment = comment;
        assignment.ActualSetAtUtc = DateTime.UtcNow;
        assignment.ActualSetByUserId = actualSetByUserId;

        await db.SaveChangesAsync(ct);
        return assignment;
    }
}
