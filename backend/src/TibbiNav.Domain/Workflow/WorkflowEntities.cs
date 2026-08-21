using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Workflow;

/// <summary>
/// Раздел 68 ТЗ: настраиваемый маршрут согласования — Trigger (EntityType) +
/// Conditions + Steps, целиком данные, без хардкода в коде. Один активный
/// WorkflowDefinition на EntityType выбирается WorkflowDefinitionSelector-ом
/// по совпадению всех Conditions (пусто = подходит всем), среди подходящих —
/// с наибольшим Priority.
/// </summary>
public class WorkflowDefinition : AuditableEntity
{
    public string Name { get; set; } = default!;

    /// <summary>"Vacancy", "LeaveRequest", ... — сопоставляется с
    /// IWorkflowEntityAdapter.EntityType. Новый EntityType подключается
    /// реализацией адаптера (Application-слой), а не изменением этой сущности.</summary>
    public string EntityType { get; set; } = default!;

    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }

    public ICollection<WorkflowCondition> Conditions { get; set; } = new List<WorkflowCondition>();
    public ICollection<WorkflowStepDefinition> Steps { get; set; } = new List<WorkflowStepDefinition>();
}

/// <summary>Раздел 68: условие применимости маршрута — сравнение поля
/// сущности (см. IWorkflowEntityAdapter.LoadContextAsync → Fields) со
/// значением. Все условия шаблона должны выполниться (AND).</summary>
public class WorkflowCondition : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = default!;

    public string FieldName { get; set; } = default!; // напр. "ClinicId", "BudgetSalary", "Priority"
    public WorkflowConditionOperator Operator { get; set; }
    public string Value { get; set; } = default!;
}

public enum WorkflowConditionOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
}

/// <summary>Раздел 68: один шаг маршрута — кто согласует (роль + scope, либо
/// прямой руководитель сотрудника), срок (SLA) и что делать при просрочке
/// (Escalation).</summary>
public class WorkflowStepDefinition : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = default!;

    public int OrderIndex { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Как резолвится множество допустимых согласующих — см.
    /// IWorkflowApproverResolver. ApproverRoleCode не используется для
    /// DirectManager (резолвится через EmploymentRecord.ManagerEmployeeId
    /// сотрудника — субъекта сущности, раздел 37).</summary>
    public WorkflowApproverStrategy ApproverStrategy { get; set; }
    public string? ApproverRoleCode { get; set; }

    /// <summary>0 = без SLA (шаг не просрочивается).</summary>
    public int SlaHours { get; set; }
    public WorkflowEscalationAction EscalationAction { get; set; } = WorkflowEscalationAction.None;

    /// <summary>Для EscalationAction.ReassignToRole — на какую роль
    /// переназначить (резолвится RoleInOrganization, см. WorkflowEngine).</summary>
    public string? EscalationRoleCode { get; set; }
}

public enum WorkflowApproverStrategy
{
    /// <summary>Любой пользователь с ApproverRoleCode, независимо от клиники
    /// назначения роли — для организационных ролей (Finance, GeneralDirector).</summary>
    RoleInOrganization,

    /// <summary>Пользователь с ApproverRoleCode, назначенным на клинику
    /// сущности (или организационно, ScopeClinicId == null).</summary>
    RoleInClinic,

    /// <summary>Раздел 36: пользователь с ApproverRoleCode, назначенным именно
    /// на подразделение сущности (ScopeDepartmentId == сущности), либо на её
    /// клинику целиком (ScopeDepartmentId == null, ScopeClinicId совпадает),
    /// либо организационно (оба null) — в порядке убывания специфичности.</summary>
    RoleInDepartment,

    /// <summary>Прямой руководитель сотрудника-субъекта сущности
    /// (WorkflowEntityContext.SubjectEmployeeId → EmploymentRecord.ManagerEmployeeId
    /// → связанный AppUser). Раздел 37: согласование руководителем подразделения.</summary>
    DirectManager,
}

public enum WorkflowEscalationAction
{
    /// <summary>Ничего не делать — шаг просто помечается просроченным
    /// (WorkflowStepInstance.EscalatedAtUtc).</summary>
    None,

    /// <summary>Раздел 68: уведомление ответственных о просрочке. Реальная
    /// отправка — через Notification Engine (roadmap-пункт 9 в README),
    /// который ещё не реализован; пока только фиксируется факт эскалации.</summary>
    Notify,

    /// <summary>Переназначить шаг на другую роль (EscalationRoleCode).</summary>
    ReassignToRole,

    /// <summary>Автоматически одобрить шаг и продвинуть workflow дальше.</summary>
    AutoApprove,
}

/// <summary>
/// Раздел 68: запущенный экземпляр маршрута для конкретной сущности
/// (EntityType+EntityId) — создаётся автоматически при создании сущности
/// (см. WorkflowEngine.StartAsync, вызывается из VacanciesController /
/// LeaveRequestsController).
/// </summary>
public class WorkflowInstance : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public Guid WorkflowDefinitionId { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }

    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.InProgress;

    /// <summary>OrderIndex текущего (активного, Pending) шага.</summary>
    public int CurrentStepIndex { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<WorkflowStepInstance> Steps { get; set; } = new List<WorkflowStepInstance>();
}

public enum WorkflowInstanceStatus
{
    InProgress,
    Approved,
    Rejected,
    Cancelled,
}

public class WorkflowStepInstance : AuditableEntity
{
    public Guid WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = default!;

    public Guid StepDefinitionId { get; set; }
    public int OrderIndex { get; set; }
    public string Name { get; set; } = default!;

    public string? ApproverRoleCode { get; set; }

    /// <summary>Один из резолвленных на момент старта шага eligible-пользователей
    /// — для отображения "текущий approver". Авторизация на Approve/Reject
    /// пересчитывается заново (см. WorkflowEngine.DecideAsync), а не полагается
    /// на этот снимок — назначения ролей могли измениться.</summary>
    public Guid? AssignedApproverUserId { get; set; }

    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Pending;
    public DateTime? DueAtUtc { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; } // null у шага, одобренного автоматически по эскалации (AutoApprove)
    public string? Comment { get; set; }
}

public enum WorkflowStepStatus
{
    Pending,
    Approved,
    Rejected,
}
