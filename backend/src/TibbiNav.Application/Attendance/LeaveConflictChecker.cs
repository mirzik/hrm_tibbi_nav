using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Attendance;

public sealed record ConflictingLeave(Guid EmployeeId, string FullName, DateOnly StartDate, DateOnly EndDate);

public sealed record LeaveConflictWarning(
    bool HasConflict,
    string Message,
    string Category,
    int TotalPeers,
    int AbsentCount,
    int ThresholdPercent,
    IReadOnlyList<ConflictingLeave> ConflictingLeaves);

/// <summary>
/// Раздел 39 ТЗ: при создании LeaveRequest предупреждает, если в выбранный
/// период уже отсутствует значимая доля коллег той же категории персонала в
/// том же подразделении (напр. "в период отсутствуют 2 из 4 врачей отделения") —
/// не блокирует создание заявки, только предупреждает (см. LeaveRequestsController.Create).
/// "Коллеги" — сотрудники того же Department И той же PersonnelCategory (через
/// текущую Position), а не всё подразделение целиком: нехватка медсестёр и
/// нехватка врачей — разные, не взаимозаменяемые проблемы покрытия.
/// Порог — DepartmentLeaveThreshold (раздел 39: "конфигурируемый порог по
/// подразделению, не хардкод"); при отсутствии настройки — DefaultThresholdPercent.
/// </summary>
public sealed class LeaveConflictChecker(TibbiNavDbContext db)
{
    public const int DefaultThresholdPercent = 50;

    public async Task<LeaveConflictWarning> CheckAsync(Guid employeeId, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        var employment = await db.EmploymentRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.IsCurrent, ct);
        if (employment is null)
            return NoData("У сотрудника нет текущей записи трудоустройства — проверка конфликтов недоступна.");

        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == employment.PositionId, ct);
        if (position is null)
            return NoData("У сотрудника не определена должность — проверка конфликтов недоступна.");

        var peerIds = await db.EmploymentRecords.AsNoTracking()
            .Where(r => r.IsCurrent && r.DepartmentId == employment.DepartmentId)
            .Join(db.Positions.AsNoTracking().Where(p => p.Category == position.Category),
                r => r.PositionId, p => p.Id, (r, _) => r.EmployeeId)
            .Distinct()
            .ToListAsync(ct);

        var totalPeers = peerIds.Count;
        if (totalPeers == 0)
            return NoData($"В подразделении нет других сотрудников категории «{Describe(position.Category)}» — сравнивать не с кем.", position.Category);

        var overlapping = await db.LeaveRequests.AsNoTracking()
            .Where(l => peerIds.Contains(l.EmployeeId) && l.EmployeeId != employeeId && l.Status == LeaveStatus.Approved)
            .Where(l => l.StartDate <= endDate && l.EndDate >= startDate)
            .ToListAsync(ct);

        var overlappingEmployeeIds = overlapping.Select(l => l.EmployeeId).Distinct().ToList();
        // +1 — сам заявитель: если эту заявку тоже одобрят, он тоже будет отсутствовать.
        var absentCount = overlappingEmployeeIds.Count + 1;

        var threshold = await db.DepartmentLeaveThresholds.AsNoTracking()
            .Where(t => t.DepartmentId == employment.DepartmentId)
            .Select(t => (int?)t.MaxConcurrentAbsencePercent)
            .FirstOrDefaultAsync(ct) ?? DefaultThresholdPercent;

        var absentPercent = absentCount * 100 / totalPeers;
        var hasConflict = absentPercent >= threshold;

        var conflictingLeaves = new List<ConflictingLeave>();
        if (overlapping.Count > 0)
        {
            var names = await db.Employees.AsNoTracking()
                .Where(e => overlappingEmployeeIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
            conflictingLeaves = overlapping
                .Select(l => new ConflictingLeave(l.EmployeeId, names.GetValueOrDefault(l.EmployeeId, "?"), l.StartDate, l.EndDate))
                .ToList();
        }

        var categoryLabel = Describe(position.Category);
        var message = hasConflict
            ? $"В выбранный период отсутствуют {absentCount} из {totalPeers} сотрудников категории «{categoryLabel}» в подразделении — порог {threshold}% превышен."
            : $"Конфликта нет: {absentCount} из {totalPeers} сотрудников категории «{categoryLabel}» (порог {threshold}%).";

        return new LeaveConflictWarning(hasConflict, message, categoryLabel, totalPeers, absentCount, threshold, conflictingLeaves);
    }

    private static LeaveConflictWarning NoData(string message, PersonnelCategory? category = null) =>
        new(false, message, category?.ToString() ?? "", 0, 0, DefaultThresholdPercent, []);

    private static string Describe(PersonnelCategory category) => category switch
    {
        PersonnelCategory.Doctor => "врач",
        PersonnelCategory.NursingStaff => "средний медперсонал",
        PersonnelCategory.JuniorMedicalStaff => "младший медперсонал",
        PersonnelCategory.Reception => "ресепшн",
        PersonnelCategory.TechnicalStaff => "технический персонал",
        PersonnelCategory.CorporateAup => "корпоративный АУП",
        PersonnelCategory.MedicalAup => "медицинский АУП",
        _ => category.ToString(),
    };
}
