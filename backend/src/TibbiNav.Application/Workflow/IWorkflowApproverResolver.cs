using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Workflow;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Workflow;

/// <summary>Раздел 68: резолвит множество пользователей, допущенных
/// согласовывать конкретный шаг маршрута — по роли+scope или по прямому
/// руководителю (раздел 37). Пересчитывается заново на каждое решение
/// (WorkflowEngine.DecideAsync), а не берётся из снимка на момент старта шага —
/// назначения ролей могли измениться за время, пока шаг ждал согласования.</summary>
public interface IWorkflowApproverResolver
{
    Task<IReadOnlyList<Guid>> ResolveAsync(
        WorkflowApproverStrategy strategy, string? roleCode, WorkflowEntityContext context, CancellationToken ct);
}

public sealed class WorkflowApproverResolver(TibbiNavDbContext db) : IWorkflowApproverResolver
{
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        WorkflowApproverStrategy strategy, string? roleCode, WorkflowEntityContext context, CancellationToken ct)
    {
        if (strategy == WorkflowApproverStrategy.DirectManager)
        {
            if (context.SubjectEmployeeId is null) return [];

            var managerEmployeeId = await db.EmploymentRecords.AsNoTracking()
                .Where(r => r.EmployeeId == context.SubjectEmployeeId && r.IsCurrent)
                .Select(r => r.ManagerEmployeeId)
                .FirstOrDefaultAsync(ct);
            if (managerEmployeeId is null) return [];

            return await db.Users.AsNoTracking()
                .Where(u => u.EmployeeId == managerEmployeeId && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);
        }

        if (roleCode is null) return [];

        var query = db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Code == roleCode && ur.User.IsActive);

        // RoleInClinic: подходит и организационное назначение роли (ScopeClinicId
        // == null — напр. SuperAdmin), и назначение именно на клинику сущности.
        // RoleInOrganization: клиника назначения роли не важна вовсе (напр.
        // Finance/GeneralDirector согласуют по всей организации).
        if (strategy == WorkflowApproverStrategy.RoleInClinic)
            query = query.Where(ur => ur.ScopeClinicId == null || ur.ScopeClinicId == context.ClinicId);

        return await query.Select(ur => ur.UserId).Distinct().ToListAsync(ct);
    }
}
