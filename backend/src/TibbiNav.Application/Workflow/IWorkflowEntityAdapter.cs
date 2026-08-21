namespace TibbiNav.Application.Workflow;

/// <summary>Снимок сущности-триггера для WorkflowEngine: скоуп (для
/// WorkflowInstance.OrganizationId/ClinicId/DepartmentId и для
/// WorkflowApproverStrategy.RoleInClinic/RoleInDepartment — раздел 36),
/// SubjectEmployeeId (для WorkflowApproverStrategy.DirectManager — раздел 37;
/// null, если у сущности нет "владельца"-сотрудника, напр. у Vacancy) и Fields
/// (для сопоставления WorkflowCondition — раздел 68).</summary>
public sealed record WorkflowEntityContext(
    Guid OrganizationId,
    Guid? ClinicId,
    Guid? DepartmentId,
    Guid? SubjectEmployeeId,
    IReadOnlyDictionary<string, string?> Fields);

public enum WorkflowOutcome
{
    Approved,
    Rejected,
}

/// <summary>
/// Раздел 68: связывает WorkflowEngine с конкретным типом сущности-триггера
/// (Vacancy, LeaveRequest, ...). Сам движок (WorkflowEngine,
/// WorkflowDefinitionSelector, WorkflowApproverResolver) ничего не знает про
/// конкретные сущности — только про EntityType (строка) и этот интерфейс.
/// Подключение новой сущности к Workflow Engine = новая реализация этого
/// интерфейса + вызов WorkflowEngine.StartAsync в её контроллере/сервисе
/// создания; сами маршруты (шаги/условия/SLA) остаются данными.
/// </summary>
public interface IWorkflowEntityAdapter
{
    string EntityType { get; }

    Task<WorkflowEntityContext> LoadContextAsync(Guid entityId, CancellationToken ct);

    /// <summary>Применяет финальный исход workflow к самой сущности (напр.
    /// Vacancy.Status = Approved/Rejected). Вызывается WorkflowEngine после
    /// последнего шага (Approved) или сразу при отказе на любом шаге (Rejected).</summary>
    Task ApplyOutcomeAsync(Guid entityId, WorkflowOutcome outcome, CancellationToken ct);
}
