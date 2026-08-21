using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Attendance;

/// <summary>
/// Раздел 35-36 ТЗ: табель рабочего времени одного сотрудника за период
/// (обычно месяц). Блокировка правок управляется не собственным полем, а
/// наличием TimesheetClosure со Status=Locked, покрывающего то же
/// Department+период — так закрытие остаётся единым источником истины и не
/// может рассинхронизироваться с отдельными табелями сотрудников этого
/// подразделения (см. TimesheetService.IsPeriodLockedAsync).
/// </summary>
public class Timesheet : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ClinicId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid EmployeeId { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public ICollection<TimesheetDay> Days { get; set; } = new List<TimesheetDay>();
}

public class TimesheetDay : BaseEntity
{
    public Guid TimesheetId { get; set; }
    public Timesheet Timesheet { get; set; } = default!;

    public DateOnly Date { get; set; }
    public TimesheetDayType DayType { get; set; }

    /// <summary>Обычные часы за день (раздел 35). Переработка — отдельное
    /// поле, а не отдельный DayType: переработка обычно происходит поверх
    /// рабочего дня, а не вместо него.</summary>
    public decimal Hours { get; set; }
    public decimal OvertimeHours { get; set; }

    public string? Comment { get; set; }
}

public enum TimesheetDayType
{
    Working,
    Weekend,
    Holiday,
    Leave,
    Sick,
    BusinessTrip,
    Absence,
}

/// <summary>
/// Раздел 36 ТЗ: закрытие табеля по подразделению+периоду — проходит маршрут
/// Department Manager → HR → Accounting (Workflow Engine, раздел 68,
/// EntityType="TimesheetClosure", см. TimesheetClosureWorkflowAdapter в
/// Application-слое). Locked — единственный статус, реально блокирующий
/// правки Timesheet-ов этого Department+периода; Rejected просто возвращает
/// период в редактируемое состояние (повторное закрытие — новый TimesheetClosure).
/// </summary>
public class TimesheetClosure : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }
    public Guid DepartmentId { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public TimesheetClosureStatus Status { get; set; } = TimesheetClosureStatus.InProgress;
    public DateTime? LockedAtUtc { get; set; }
}

public enum TimesheetClosureStatus
{
    InProgress,
    Locked,
    Rejected,
}

/// <summary>Раздел 36: "После Locked изменения запрещены без отдельной
/// процедуры (все правки логируются)" — эта таблица и есть журнал той
/// процедуры (см. TimesheetService.CorrectDayAsync). Обычные, до-Locked
/// правки (SetDaysAsync) в этот журнал не пишутся — только исправления
/// после закрытия периода.</summary>
public class TimesheetEditLog : BaseEntity
{
    public Guid TimesheetId { get; set; }
    public Guid TimesheetDayId { get; set; }

    public DateTime EditedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid EditedByUserId { get; set; }
    public string Reason { get; set; } = default!;

    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
}

/// <summary>Раздел 39 ТЗ: порог конфликта одновременных отпусков по
/// подразделению — данные, не хардкод (см. Application.Attendance.LeaveConflictChecker).
/// Если для подразделения нет строки — применяется дефолтный порог
/// (LeaveConflictChecker.DefaultThresholdPercent).</summary>
public class DepartmentLeaveThreshold : AuditableEntity
{
    public Guid DepartmentId { get; set; }
    public int MaxConcurrentAbsencePercent { get; set; }
}
