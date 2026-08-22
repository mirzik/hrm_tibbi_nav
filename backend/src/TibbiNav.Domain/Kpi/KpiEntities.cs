using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Kpi;

/// <summary>Раздел 45 ТЗ: на каком уровне измеряется показатель — определяет,
/// какая именно цель (Employee/Department/Clinic/вся организация) допустима
/// у KpiAssignment, ссылающегося на этот шаблон.</summary>
public enum KpiLevel { Corporate, Clinic, Department, Individual }

/// <summary>Раздел 46 ТЗ: периодичность оценки показателя.</summary>
public enum KpiPeriodType { Month, Quarter, HalfYear, Year }

/// <summary>
/// Раздел 45-46 ТЗ: переиспользуемое определение показателя (KPI) — что
/// измеряем, в каких единицах, из какого источника, по какой методике. Сам
/// показатель НЕ содержит target/actual — это назначается на конкретного
/// исполнителя (сотрудника/подразделение/клинику) на конкретный период через
/// KpiAssignment, поэтому один шаблон можно переиспользовать многократно (та
/// же метрика на разных сотрудников, на разные периоды).
/// </summary>
public class KpiTemplate : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; } // null = шаблон общий на всю организацию

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public KpiLevel Level { get; set; }
    public KpiPeriodType PeriodType { get; set; }

    /// <summary>Вес показателя (%) в общей оценке исполнителя за период — по
    /// умолчанию для новых назначений, можно переопределить в KpiAssignment.Weight.</summary>
    public decimal DefaultWeight { get; set; }

    public string Unit { get; set; } = default!; // "%", "шт", "млн сомони" и т.п.
    public string? Source { get; set; } // источник данных факта (напр. "1С", "CRM", "ручной ввод HR")
    public string? Formula { get; set; } // текстовое описание методики расчёта

    public bool IsActive { get; set; } = true;

    public ICollection<KpiAssignment> Assignments { get; set; } = new List<KpiAssignment>();
}

/// <summary>
/// Раздел 45-46 ТЗ: назначение KpiTemplate на конкретную цель, на конкретный
/// период, с целевым (Target) и — после "проставления факта" — фактическим
/// (Actual) значением. Ровно одно из EmployeeId/DepartmentId заполнено согласно
/// KpiTemplate.Level (Individual/Department); для Clinic — ClinicId указывает
/// саму клинику-цель; для Corporate все три пусты (цель — организация целиком).
/// ClinicId также используется RBAC-скоупингом (ApplyScope&lt;T&gt;) — как и у
/// любой другой IOrganizationScoped-сущности.
/// Выполнение (%) не хранится — считается на лету как Actual/Target*100
/// (см. KpiAssignmentService.ToDto), чтобы не рассинхронизироваться при
/// повторном проставлении факта.
/// </summary>
public class KpiAssignment : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid KpiTemplateId { get; set; }
    public KpiTemplate KpiTemplate { get; set; } = default!;

    public Guid? EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }

    public KpiPeriodType PeriodType { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public decimal Weight { get; set; }
    public decimal Target { get; set; }
    public decimal? Actual { get; set; }
    public string? Comment { get; set; }

    public DateTime? ActualSetAtUtc { get; set; }
    public Guid? ActualSetByUserId { get; set; }
}
