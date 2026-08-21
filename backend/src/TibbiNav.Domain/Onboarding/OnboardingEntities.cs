using TibbiNav.Domain.Common;
using TibbiNav.Domain.Organization;

namespace TibbiNav.Domain.Onboarding;

/// <summary>
/// Раздел 25, 31-32 ТЗ: шаблон чеклиста оформления — набор задач по этапам
/// (Preboarding → Day1 → Week1 → Day30 → Day60 → Day90). Шаблон можно сузить
/// под категорию персонала / конкретную должность / конкретную клинику — null
/// в соответствующем поле значит "подходит для всех". При найме
/// OnboardingChecklistTemplateSelector выбирает самый специфичный подходящий
/// шаблон (раздел 25: "чеклист по категории персонала/должности/клинике").
/// </summary>
public class OnboardingChecklistTemplate : AuditableEntity
{
    public string Name { get; set; } = default!;
    public PersonnelCategory? PersonnelCategory { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? ClinicId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<OnboardingChecklistTemplateTask> Tasks { get; set; } = new List<OnboardingChecklistTemplateTask>();
}

public class OnboardingChecklistTemplateTask : BaseEntity
{
    public Guid TemplateId { get; set; }
    public OnboardingChecklistTemplate Template { get; set; } = default!;

    public OnboardingStage Stage { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public OnboardingResponsible Responsible { get; set; }

    /// <summary>Срок относительно даты выхода (HireDate), в днях; отрицательное —
    /// до выхода (Preboarding). Null — берётся дефолт этапа
    /// (см. OnboardingStageDefaults в Application-слое).</summary>
    public int? DueOffsetDays { get; set; }

    public int OrderIndex { get; set; }
}

/// <summary>Раздел 31-32 ТЗ: этапы адаптации.</summary>
public enum OnboardingStage
{
    Preboarding,
    Day1,
    Week1,
    Day30,
    Day60,
    Day90,
}

/// <summary>Кто отвечает за выполнение задачи — раздел 32: чеклист распределён
/// между HR, руководителем, ИТ и самим сотрудником.</summary>
public enum OnboardingResponsible
{
    Hr,
    DirectManager,
    ItAdmin,
    Employee,
    ClinicAdmin,
}

/// <summary>
/// Раздел 31-32: конкретный чеклист конкретного нанятого сотрудника — создаётся
/// автоматически HireCandidateService-ом сразу после найма
/// (см. TibbiNav.Application.Onboarding.OnboardingChecklistService), атомарно
/// в той же транзакции.
/// </summary>
public class OnboardingChecklist : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid TemplateId { get; set; }
    public DateOnly HireDate { get; set; }

    public OnboardingChecklistStatus Status { get; set; } = OnboardingChecklistStatus.InProgress;
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<OnboardingChecklistTask> Tasks { get; set; } = new List<OnboardingChecklistTask>();
}

public enum OnboardingChecklistStatus
{
    InProgress,
    Completed,
    Cancelled,
}

public class OnboardingChecklistTask : AuditableEntity
{
    public Guid ChecklistId { get; set; }
    public OnboardingChecklist Checklist { get; set; } = default!;

    public OnboardingStage Stage { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public OnboardingResponsible Responsible { get; set; }
    public DateOnly DueDate { get; set; }
    public int OrderIndex { get; set; }

    public OnboardingTaskStatus Status { get; set; } = OnboardingTaskStatus.Pending;
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? Notes { get; set; }
}

public enum OnboardingTaskStatus
{
    Pending,
    InProgress,
    Done,
    Skipped,
}
