using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Identity;

public class AppUser : AuditableEntity
{
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string DisplayName { get; set; } = default!;
    public Guid? EmployeeId { get; set; } // связь с Employee, если пользователь — сотрудник
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>Роли — раздел 64. Список минимальный, расширяется без изменения кода
/// (роль — данные, а не enum, чтобы SuperAdmin мог добавлять новые).</summary>
public class Role : AuditableEntity
{
    public string Code { get; set; } = default!; // SuperAdmin, HRAdmin, ChiefDoctor, ...
    public string Name { get; set; } = default!;
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Permission = Role + Action + Scope + Field Restrictions (раздел 65).
/// Пример: HRManager может Edit Employee в пределах Clinic="DUS", кроме поля Salary.
/// </summary>
public class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public string Resource { get; set; } = default!; // "Employee", "Vacancy", "Compensation"
    public PermissionAction Action { get; set; }
    public PermissionScope Scope { get; set; }

    /// <summary>CSV полей, к которым нет доступа несмотря на разрешённое действие.
    /// Напр. "Salary,BankAccount,NationalId" для роли DepartmentManager.</summary>
    public string? RestrictedFields { get; set; }
}

public enum PermissionAction
{
    View, Create, Edit, Delete, Approve, Export
}

/// <summary>Раздел 65: Organization / Clinic / Department / Own Employees / Self.</summary>
public enum PermissionScope
{
    Self,
    OwnEmployees,
    Department,
    Clinic,
    Organization
}

public class UserRole : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    /// <summary>Опциональное сужение роли до конкретной клиники/подразделения
    /// (напр. ChiefDoctor только для Clinic="DUS").</summary>
    public Guid? ScopeClinicId { get; set; }
    public Guid? ScopeDepartmentId { get; set; }
}

/// <summary>
/// Раздел 21: временный доступ внешнего эксперта (MEDSI Russia) только
/// к конкретному кандидату, с автоматическим истечением после оценки.
/// </summary>
public class ExternalExpertAccess : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid CandidateId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
