using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Attendance;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Attendance;

public sealed record TimesheetDayInput(DateOnly Date, TimesheetDayType DayType, decimal Hours, decimal OvertimeHours, string? Comment);

/// <summary>
/// Раздел 35-36 ТЗ: табель рабочего времени. Обычные правки (SetDaysAsync)
/// запрещены, если период уже закрыт (Locked) — единственный путь изменить
/// закрытый табель — CorrectDayAsync, отдельная процедура, которая работает
/// даже при Locked, но требует причину и пишет TimesheetEditLog.
/// </summary>
public sealed class TimesheetService(TibbiNavDbContext db)
{
    public async Task<Timesheet> CreateAsync(Guid employeeId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        if (periodEnd < periodStart)
            throw new InvalidOperationException("Дата окончания периода раньше даты начала.");

        var exists = await db.Timesheets.AnyAsync(t =>
            t.EmployeeId == employeeId && t.PeriodStart == periodStart && t.PeriodEnd == periodEnd, ct);
        if (exists)
            throw new InvalidOperationException("Табель за этот период для сотрудника уже существует.");

        var employment = await db.EmploymentRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.IsCurrent, ct)
            ?? throw new InvalidOperationException("У сотрудника нет текущей записи трудоустройства (EmploymentRecord).");

        var timesheet = new Timesheet
        {
            OrganizationId = employment.OrganizationId,
            ClinicId = employment.ClinicId ?? throw new InvalidOperationException("У записи трудоустройства не указана клиника."),
            DepartmentId = employment.DepartmentId,
            EmployeeId = employeeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
        };
        db.Timesheets.Add(timesheet);
        await db.SaveChangesAsync(ct);
        return timesheet;
    }

    public async Task<bool> IsLockedAsync(Guid timesheetId, CancellationToken ct)
    {
        var timesheet = await db.Timesheets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == timesheetId, ct)
            ?? throw new KeyNotFoundException("Табель не найден.");
        return await IsPeriodLockedAsync(timesheet.DepartmentId, timesheet.PeriodStart, timesheet.PeriodEnd, ct);
    }

    /// <summary>Раздел 36: единый источник истины о блокировке — наличие
    /// TimesheetClosure со Status=Locked для этого Department+периода, а не
    /// собственное поле Timesheet (см. Domain-комментарий).</summary>
    private async Task<bool> IsPeriodLockedAsync(Guid departmentId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct) =>
        await db.TimesheetClosures.AsNoTracking().AnyAsync(c =>
            c.DepartmentId == departmentId && c.PeriodStart == periodStart && c.PeriodEnd == periodEnd
            && c.Status == TimesheetClosureStatus.Locked, ct);

    /// <summary>Раздел 35: массовое проставление дней табеля (upsert по дате).
    /// Запрещено, если период уже Locked.</summary>
    public async Task SetDaysAsync(Guid timesheetId, IReadOnlyList<TimesheetDayInput> days, CancellationToken ct)
    {
        var timesheet = await db.Timesheets.Include(t => t.Days).FirstOrDefaultAsync(t => t.Id == timesheetId, ct)
            ?? throw new KeyNotFoundException("Табель не найден.");

        if (await IsPeriodLockedAsync(timesheet.DepartmentId, timesheet.PeriodStart, timesheet.PeriodEnd, ct))
            throw new InvalidOperationException(
                "Период закрыт (Locked) — обычные правки запрещены. Используйте процедуру исправления (POST .../days/{date}/correct).");

        foreach (var input in days)
        {
            if (input.Date < timesheet.PeriodStart || input.Date > timesheet.PeriodEnd)
                throw new InvalidOperationException($"Дата {input.Date:yyyy-MM-dd} вне периода табеля.");

            var day = timesheet.Days.FirstOrDefault(d => d.Date == input.Date);
            if (day is null)
            {
                // db.TimesheetDays.Add(...), а не только timesheet.Days.Add(...):
                // добавление нового child-а с уже присвоенным (Guid.NewGuid()) ключом
                // в коллекцию УЖЕ отслеживаемого (загруженного запросом, не только
                // что созданного) родителя — EF по умолчанию решает по виду ключа,
                // что запись уже существует (Unchanged), и после присвоения полей
                // ниже помечает её Modified → шлёт UPDATE несуществующей строки
                // (DbUpdateConcurrencyException). Явный Add на DbSet снимает эту
                // неоднозначность.
                day = new TimesheetDay { Timesheet = timesheet, Date = input.Date };
                db.TimesheetDays.Add(day);
                timesheet.Days.Add(day);
            }

            day.DayType = input.DayType;
            day.Hours = input.Hours;
            day.OvertimeHours = input.OvertimeHours;
            day.Comment = input.Comment;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Раздел 36: "отдельная процедура" правки после Locked — работает
    /// независимо от статуса закрытия, но обязательно требует причину и
    /// пишет TimesheetEditLog (старое/новое значение, кто, когда, почему).</summary>
    public async Task<TimesheetDay> CorrectDayAsync(
        Guid timesheetId, DateOnly date, TimesheetDayInput input, Guid editedByUserId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Для исправления необходимо указать причину — она попадёт в журнал правок.");

        var timesheet = await db.Timesheets.Include(t => t.Days).FirstOrDefaultAsync(t => t.Id == timesheetId, ct)
            ?? throw new KeyNotFoundException("Табель не найден.");

        if (date < timesheet.PeriodStart || date > timesheet.PeriodEnd)
            throw new InvalidOperationException($"Дата {date:yyyy-MM-dd} вне периода табеля.");

        var day = timesheet.Days.FirstOrDefault(d => d.Date == date);
        var oldValueJson = day is null ? null : JsonSerializer.Serialize(new { day.DayType, day.Hours, day.OvertimeHours, day.Comment });

        if (day is null)
        {
            day = new TimesheetDay { Timesheet = timesheet, Date = date }; // см. комментарий в SetDaysAsync про явный db.TimesheetDays.Add
            db.TimesheetDays.Add(day);
            timesheet.Days.Add(day);
        }

        day.DayType = input.DayType;
        day.Hours = input.Hours;
        day.OvertimeHours = input.OvertimeHours;
        day.Comment = input.Comment;

        var newValueJson = JsonSerializer.Serialize(new { day.DayType, day.Hours, day.OvertimeHours, day.Comment });

        db.TimesheetEditLogs.Add(new TimesheetEditLog
        {
            TimesheetId = timesheet.Id,
            TimesheetDayId = day.Id,
            EditedByUserId = editedByUserId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
        });

        await db.SaveChangesAsync(ct);
        return day;
    }
}
