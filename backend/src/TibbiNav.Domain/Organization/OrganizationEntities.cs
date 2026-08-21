using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Organization;

/// <summary>Юридическое лицо верхнего уровня. ООО «Тибби Нав».</summary>
public class OrganizationUnit : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string LegalName { get; set; } = default!;
    public string? TaxId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Clinic> Clinics { get; set; } = new List<Clinic>();
}

/// <summary>
/// Клиника сети (раздел 2, 3 ТЗ). Не хардкодится: новая клиника
/// создаётся через UI администратором, ядро кода не меняется.
/// </summary>
public class Clinic : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public OrganizationUnit Organization { get; set; } = default!;

    /// <summary>Для IOrganizationScoped: клиника сама себе scope.</summary>
    Guid? IOrganizationScoped.ClinicId { get => Id; set { } }

    public string Code { get; set; } = default!; // напр. "DUS" -> используется в Employee ID
    public string Name { get; set; } = default!;
    public string Address { get; set; } = default!;

    public ClinicStatus Status { get; set; } = ClinicStatus.Active;
    public DateOnly? PlannedOpeningDate { get; set; } // Workforce Planning, раздел 12

    public ICollection<Department> Departments { get; set; } = new List<Department>();
}

public enum ClinicStatus
{
    Planning = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3
}

public class Department : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }
    public Clinic Clinic { get; set; } = default!;

    public Guid? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }

    public string Name { get; set; } = default!;
    public string? CostCenterCode { get; set; }

    public ICollection<Position> Positions { get; set; } = new List<Position>();
}

/// <summary>Штатная единица (раздел 11). Position — это "слот", Employee его занимает.</summary>
public class Position : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = default!;

    public string Title { get; set; } = default!;
    public PersonnelCategory Category { get; set; }

    public decimal ApprovedFte { get; set; } = 1.0m;
    public decimal SalaryBudgetMonthly { get; set; }

    public Guid? ReportsToPositionId { get; set; }

    /// <summary>Filled FTE / vacancy считаются в Application-слое агрегатным запросом,
    /// не хранятся денормализовано, чтобы не рассинхронизировались.</summary>
}

/// <summary>Категории персонала — раздел 6 ТЗ.</summary>
public enum PersonnelCategory
{
    CorporateAup,
    MedicalAup,
    Doctor,
    NursingStaff,
    JuniorMedicalStaff,
    Reception,
    TechnicalStaff,
    PartTime,
    ExternalPartTime,
    Temporary,
    Intern,
    ForeignSpecialist,
    RussianDoctor
}
