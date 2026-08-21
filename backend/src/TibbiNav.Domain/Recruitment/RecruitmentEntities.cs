using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Recruitment;

/// <summary>Vacancy Request (раздел 14-16): заявка руководителя, проходит workflow согласования.</summary>
public class Vacancy : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid PositionId { get; set; }

    public int HeadcountRequested { get; set; } = 1;
    public VacancyReason Reason { get; set; }
    public Guid? ReplacingEmployeeId { get; set; }
    public DateOnly? DesiredStartDate { get; set; }
    public decimal? BudgetSalary { get; set; }
    public string? Requirements { get; set; }
    public VacancyPriority Priority { get; set; } = VacancyPriority.Normal;

    public VacancyStatus Status { get; set; } = VacancyStatus.PendingApproval;
    public Guid CreatedByEmployeeId { get; set; }
}

public enum VacancyReason { NewPosition, Replacement }
public enum VacancyPriority { Low, Normal, High, Critical }

public enum VacancyStatus
{
    PendingApproval, Approved, Rejected, Open, OnHold, Closed, Cancelled
}

/// <summary>Candidate Profile (раздел 17). Duplicate detection — раздел 19 — реализуется
/// в Application-слое по Phone/Email/FullName/DocumentId перед созданием.</summary>
public class Candidate : AuditableEntity
{
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? CvUrl { get; set; }
    public string? Source { get; set; }
    public string? Specialization { get; set; }
    public int? ExperienceYears { get; set; }
    public decimal? SalaryExpectation { get; set; }

    public TalentPoolStatus? TalentPoolStatus { get; set; } // раздел 58
}

public enum TalentPoolStatus { TalentPool, FutureCandidate, EligibleForRehire, DoNotRehire }

/// <summary>Kanban-стадия конкретного кандидата на конкретную вакансию (раздел 18).</summary>
public class CandidateApplication : AuditableEntity
{
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = default!;
    public Guid VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = default!;

    public PipelineStage Stage { get; set; } = PipelineStage.New;
    public string? RejectionReason { get; set; }

    public ICollection<Interview> Interviews { get; set; } = new List<Interview>();
    public Offer? Offer { get; set; }
}

public enum PipelineStage
{
    New, Screening, HrInterview, ProfessionalInterview, MedsiRussiaReview,
    FinalInterview, Approved, OfferPreparation, OfferSent, OfferAccepted,
    Documents, ReadyToHire, Hired, Rejected, TalentPool
}

/// <summary>Interview + scorecard (разделы 20-22).</summary>
public class Interview : AuditableEntity
{
    public Guid CandidateApplicationId { get; set; }
    public InterviewType Type { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public Guid? InterviewerUserId { get; set; }
    public InterviewResult? Result { get; set; }

    /// <summary>JSON scorecard — гибкая структура на каждый InterviewType
    /// (клинические знания, опыт, коммуникация... для врачей — раздел 22).</summary>
    public string? ScorecardJson { get; set; }
    public string? Notes { get; set; }
}

public enum InterviewType { Hr, Manager, Medical, Panel, MedsiRussia, Final }
public enum InterviewResult { RecommendedNoConditions, RecommendedWithConditions, NotRecommended }

/// <summary>Offer Management (раздел 23).</summary>
public class Offer : AuditableEntity
{
    public Guid CandidateApplicationId { get; set; }
    public decimal Salary { get; set; }
    public decimal? Bonus { get; set; }
    public decimal Fte { get; set; } = 1.0m;
    public DateOnly ProposedStartDate { get; set; }
    public int ProbationDays { get; set; } = 90;
    public string? AdditionalTerms { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Draft;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}

public enum OfferStatus { Draft, Sent, Accepted, Declined, Expired }
