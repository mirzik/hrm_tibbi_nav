using TibbiNav.Domain.Common;
using TibbiNav.Domain.Kpi;

namespace TibbiNav.Domain.PerformanceReviews;

/// <summary>Раздел 47 ТЗ: какой процесс запускает цикл оценки — определяет,
/// какие ReviewerRole автоматически создаются при добавлении участника (см.
/// PerformanceReviewService.AddParticipantAsync). Self — только самооценка;
/// Manager — только оценка руководителем; Review180 — оба (самооценка +
/// руководитель, "два взгляда"); Review360 — оба плюс явно добавляемые
/// Peer/Subordinate (см. AddReviewerAsync — кого именно назначить в оценщики
/// 360° не резолвится автоматически, это осознанный выбор HR/руководителя).</summary>
public enum ReviewCycleType { Self, Manager, Review180, Review360 }

public enum ReviewCycleStatus { Draft, Open, Closed }

/// <summary>С чьей точки зрения дана оценка внутри ReviewAssignment.</summary>
public enum ReviewerRole { Self, Manager, Peer, Subordinate }

public enum ReviewAssignmentStatus { NotStarted, Submitted }

public enum ParticipantStatus { InProgress, Completed }

public enum IdpGoalStatus { NotStarted, InProgress, Completed }

/// <summary>
/// Раздел 47 ТЗ: цикл оценки эффективности за период (напр. "Оценка Q3 2026") —
/// задаёт тип процесса (Self/Manager/180/360) и период, который оценивается
/// (не путать с датами самого цикла — сотрудников оценивают ЗА PeriodStart..
/// PeriodEnd, а заполняют формы уже после его окончания).
/// </summary>
public class ReviewCycle : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public string Name { get; set; } = default!;
    public ReviewCycleType Type { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public ReviewCycleStatus Status { get; set; } = ReviewCycleStatus.Draft;

    public ICollection<ReviewParticipant> Participants { get; set; } = new List<ReviewParticipant>();
}

/// <summary>
/// Один сотрудник внутри одного цикла — "зонтик" над всеми его
/// ReviewAssignment (Self/Manager/Peer/Subordinate), несущий итоговую оценку.
/// </summary>
public class ReviewParticipant : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid ReviewCycleId { get; set; }
    public ReviewCycle ReviewCycle { get; set; } = default!;

    public Guid EmployeeId { get; set; } // кого оценивают

    public ParticipantStatus Status { get; set; } = ParticipantStatus.InProgress;

    /// <summary>Считается в PerformanceReviewService.CalculateOverallScoreAsync —
    /// среднее по ReviewerRole (сначала усредняем внутри роли, потом между
    /// ролями), чтобы 360° с 5 Peer-оценками не "забивало" 1 Manager-оценку.</summary>
    public decimal? OverallScore { get; set; }
    public DateTime? OverallScoreCalculatedAtUtc { get; set; }

    public ICollection<ReviewAssignment> Assignments { get; set; } = new List<ReviewAssignment>();
    public ICollection<IndividualDevelopmentPlan> DevelopmentPlans { get; set; } = new List<IndividualDevelopmentPlan>();
}

/// <summary>
/// Одна конкретная оценка от одного оценщика (Self/Manager/Peer/Subordinate)
/// одного участника. Раздел 47: "может ссылаться на KpiAssignment сотрудника
/// за период как часть оценки" — см. ReviewKpiReference.
/// </summary>
public class ReviewAssignment : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid ReviewParticipantId { get; set; }
    public ReviewParticipant ReviewParticipant { get; set; } = default!;

    public ReviewerRole ReviewerRole { get; set; }
    public Guid ReviewerId { get; set; } // EmployeeId оценщика (сам себе — для Self)

    public ReviewAssignmentStatus Status { get; set; } = ReviewAssignmentStatus.NotStarted;

    public decimal? Score { get; set; } // шкала не хардкодится (обычно 1-5), см. README
    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? Comments { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    public ICollection<ReviewKpiReference> KpiReferences { get; set; } = new List<ReviewKpiReference>();
}

/// <summary>Связка оценки с KPI-результатами сотрудника за тот же период —
/// оценщик явно выбирает, какие KpiAssignment учёл при выставлении Score.</summary>
public class ReviewKpiReference : BaseEntity
{
    public Guid ReviewAssignmentId { get; set; }
    public ReviewAssignment ReviewAssignment { get; set; } = default!;

    public Guid KpiAssignmentId { get; set; }
    public KpiAssignment KpiAssignment { get; set; } = default!;
}

/// <summary>
/// Раздел 47 ТЗ: Individual Development Plan — простая структура (цели
/// развития + сроки + комментарии), формируемая по итогам цикла оценки.
/// ReviewParticipantId опционален — план обычно рождается из результатов
/// оценки, но не обязан существовать только при её наличии.
/// </summary>
public class IndividualDevelopmentPlan : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid? ReviewParticipantId { get; set; }
    public ReviewParticipant? ReviewParticipant { get; set; }

    public ICollection<IdpGoal> Goals { get; set; } = new List<IdpGoal>();
}

public class IdpGoal : BaseEntity
{
    public Guid IndividualDevelopmentPlanId { get; set; }
    public IndividualDevelopmentPlan IndividualDevelopmentPlan { get; set; } = default!;

    public string Description { get; set; } = default!; // цель развития
    public DateOnly TargetDate { get; set; } // срок
    public string? Comment { get; set; }
    public IdpGoalStatus Status { get; set; } = IdpGoalStatus.NotStarted;
}
