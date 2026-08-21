using TibbiNav.Domain.Identity;

namespace TibbiNav.Application.Authorization;

/// <summary>
/// Раздел 65 ТЗ: Permission = Role + Action + Scope + Restricted Fields.
/// Это разрешённый контекст доступа текущего пользователя к конкретному
/// Resource+Action — результат работы <see cref="ScopeContextResolver"/>,
/// который выставляет его <see cref="ScopeFilterMiddleware"/> (проект API)
/// в <see cref="IScopeContextAccessor"/> перед тем, как запрос дойдёт до
/// контроллера. Контроллер применяет Scope к запросу через
/// <see cref="ScopeQueryExtensions"/> / <see cref="EmployeeScopeQueryExtensions"/>
/// и RestrictedFields — через <see cref="RestrictedFieldsExtensions"/>.
/// </summary>
public sealed class ScopeContext
{
    public required Guid UserId { get; init; }

    /// <summary>Employee, привязанный к текущему пользователю (если он сотрудник).
    /// Нужен для Scope.Self и Scope.OwnEmployees.</summary>
    public Guid? EmployeeId { get; init; }

    public required string Resource { get; init; }
    public required PermissionAction Action { get; init; }

    /// <summary>Самый широкий Scope среди ролей пользователя, дающих это Permission.</summary>
    public required PermissionScope Scope { get; init; }

    /// <summary>Клиники, на которые распространяется дающая доступ роль
    /// (UserRole.ScopeClinicId). Пусто = роль не сужена ни до одной конкретной
    /// клиники (напр. Organization-scope роль).</summary>
    public IReadOnlySet<Guid> ClinicIds { get; init; } = new HashSet<Guid>();

    /// <summary>Подразделения, на которые распространяется дающая доступ роль
    /// (UserRole.ScopeDepartmentId).</summary>
    public IReadOnlySet<Guid> DepartmentIds { get; init; } = new HashSet<Guid>();

    /// <summary>Объединение RestrictedFields всех ролей, давших это Permission
    /// (раздел 66) — сопоставление по подстроке имени свойства,
    /// см. <see cref="RestrictedFieldsExtensions"/>.</summary>
    public IReadOnlySet<string> RestrictedFields { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
