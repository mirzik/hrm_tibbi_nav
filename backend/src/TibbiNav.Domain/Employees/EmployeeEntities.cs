using TibbiNav.Domain.Common;
using TibbiNav.Domain.Organization;

namespace TibbiNav.Domain.Employees;

/// <summary>
/// Employee Master Profile (раздел 7-8). ONE EMPLOYEE — ONE PROFILE — ONE SOURCE OF TRUTH.
/// Все прочие модули (Attendance, Leave, KPI, Documents...) ссылаются сюда по EmployeeId.
/// </summary>
public class Employee : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    /// <summary>Человекочитаемый ID вида TN-DUS-000152 (раздел 7). Генерируется
    /// в Application-слое: TN-{ClinicCode}-{последовательность по клинике}.</summary>
    public string EmployeeCode { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Citizenship { get; set; } = default!;

    public string? Phone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? CorporateEmail { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }

    // Чувствительные поля — защищаются RestrictedFields в RBAC (раздел 8, 66)
    public string? NationalIdEncrypted { get; set; }
    public string? BankAccountEncrypted { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public ICollection<EmploymentRecord> EmploymentRecords { get; set; } = new List<EmploymentRecord>();
    public ICollection<MedicalCredential> MedicalCredentials { get; set; } = new List<MedicalCredential>();
}

public enum Gender { Male, Female }

public enum EmployeeStatus
{
    Candidate,      // ещё не нанят, профиль создаётся заранее? нет — кандидаты живут в Candidate,
                    // этот статус зарезервирован для preboarding-перехода
    Active,
    OnLeave,
    Probation,
    Suspended,
    Former,
    KtoReserv       // Кадровый резерв (раздел 6)
}

/// <summary>
/// Employment Record с Effective Dating (разделы 9-10): любое изменение должности,
/// подразделения, клиники, ставки или руководителя создаёт НОВУЮ запись,
/// старая закрывается EffectiveTo. История никогда не перезаписывается.
/// </summary>
public class EmploymentRecord : EffectiveDatedEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public Guid DepartmentId { get; set; }
    public Guid PositionId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public Guid? FunctionalManagerEmployeeId { get; set; }

    public decimal Fte { get; set; } = 1.0m;
    public EmploymentType EmploymentType { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public string? WorkSchedule { get; set; } // "5/2", "6/1", "shift", ...
    public string? CostCenterCode { get; set; }

    /// <summary>Compensation — намеренно чувствительная, field-level secured отдельно (раздел 48).</summary>
    public decimal? BaseSalary { get; set; }

    public string? ChangeReason { get; set; } // напр. "Перевод", "Повышение ставки", "Первичный найм"
}

public enum EmploymentType
{
    FullTime, PartTime, InternalCombination, ExternalCombination, Temporary, Internship
}

/// <summary>
/// Medical Credentials (раздел 26-27): дипломы, сертификаты, санкнижка —
/// с контролем expiration и уведомлениями за 90/60/30/14/7 дней.
/// </summary>
public class MedicalCredential : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public CredentialType Type { get; set; }
    public string Title { get; set; } = default!;
    public string? IssuingAuthority { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; } // null = бессрочный (напр. диплом)
    public string? FileUrl { get; set; }

    public CredentialStatus Status { get; set; } = CredentialStatus.PendingVerification;
}

public enum CredentialType
{
    Diploma, Internship, Residency, Specialty, Certificate, MinistryCertificate,
    QualificationCategory, ContinuingEducation, SanitaryBook, MedicalExam, Other
}

public enum CredentialStatus
{
    Valid, Expiring, Expired, Missing, PendingVerification
}
