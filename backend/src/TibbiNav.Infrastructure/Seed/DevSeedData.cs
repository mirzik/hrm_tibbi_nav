using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;

namespace TibbiNav.Infrastructure.Seed;

/// <summary>
/// Только для локальной разработки. Пока не поднят Keycloak (roadmap-пункт 10
/// в README) и нет UI для управления Role/RolePermission/UserRole, этот сидер
/// создаёт минимальный демонстрационный набор данных, чтобы вручную (curl/
/// Swagger + заголовок DevHeaderAuthenticationHandler.HeaderName) проверить
/// ScopeFilterMiddleware — без ролей и назначений в БД любой запрос,
/// размеченный [RequirePermission], всегда получает 403.
///
/// Демонстрирует все 5 уровней Scope (раздел 65) на трёх пользователях:
///   admin@tibbinav.local     — SuperAdmin, Scope=Organization      — видит всех.
///   hr.dus@tibbinav.local    — HRManager,   Scope=Clinic(DUS)      — видит DUS, не KHJ.
///   manager.dus@tibbinav.local — DepartmentManager, Scope=OwnEmployees — видит своих подчинённых.
///
/// Также создаёт 3 шаблона чеклиста адаптации (раздел 25, 31-32) — общий и по
/// категории персонала (Reception/Doctor) — чтобы проверить, что
/// OnboardingChecklistTemplateSelector выбирает более специфичный.
///
/// Идемпотентно: ничего не делает, если в Users уже есть записи.
/// </summary>
public static class DevSeedData
{
    public static async Task SeedAsync(TibbiNavDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var org = new OrganizationUnit { Name = "Тибби Нав", LegalName = "ООО «Тибби Нав»" };

        var clinicDus = new Clinic { Organization = org, OrganizationId = org.Id, Code = "DUS", Name = "Клиника Душанбе", Address = "г. Душанбе" };
        var clinicKhj = new Clinic { Organization = org, OrganizationId = org.Id, Code = "KHJ", Name = "Клиника Худжанд", Address = "г. Худжанд" };

        var deptReception = new Department { OrganizationId = org.Id, Clinic = clinicDus, ClinicId = clinicDus.Id, Name = "Ресепшн" };

        var positionAdmin = new Position
        {
            OrganizationId = org.Id, ClinicId = clinicDus.Id,
            Department = deptReception, DepartmentId = deptReception.Id,
            Title = "Администратор", Category = PersonnelCategory.Reception,
        };

        var managerEmployee = new Employee
        {
            OrganizationId = org.Id, ClinicId = clinicDus.Id, EmployeeCode = "TN-DUS-000001",
            FullName = "Фарзона Рахимова", Gender = Gender.Female, DateOfBirth = new DateOnly(1985, 3, 12),
            Citizenship = "TJ", CorporateEmail = "manager.dus@tibbinav.local",
        };
        var staffEmployeeDus = new Employee
        {
            OrganizationId = org.Id, ClinicId = clinicDus.Id, EmployeeCode = "TN-DUS-000002",
            FullName = "Шариф Ниёзов", Gender = Gender.Male, DateOfBirth = new DateOnly(1990, 7, 1),
            Citizenship = "TJ", CorporateEmail = "staff.dus@tibbinav.local",
            BankAccountEncrypted = "enc:acct-dus-002", NationalIdEncrypted = "enc:nid-dus-002",
        };
        var staffEmployeeKhj = new Employee
        {
            OrganizationId = org.Id, ClinicId = clinicKhj.Id, EmployeeCode = "TN-KHJ-000001",
            FullName = "Мадина Каримова", Gender = Gender.Female, DateOfBirth = new DateOnly(1992, 11, 20),
            Citizenship = "TJ", CorporateEmail = "staff.khj@tibbinav.local",
            BankAccountEncrypted = "enc:acct-khj-001", NationalIdEncrypted = "enc:nid-khj-001",
        };

        var employmentDus = new EmploymentRecord
        {
            OrganizationId = org.Id, ClinicId = clinicDus.Id,
            Employee = staffEmployeeDus, EmployeeId = staffEmployeeDus.Id,
            DepartmentId = deptReception.Id, PositionId = positionAdmin.Id,
            ManagerEmployeeId = managerEmployee.Id,
            HireDate = new DateOnly(2022, 1, 10), EffectiveFrom = new DateOnly(2022, 1, 10), IsCurrent = true,
            BaseSalary = 4500m, EmploymentType = EmploymentType.FullTime,
        };
        var employmentKhj = new EmploymentRecord
        {
            OrganizationId = org.Id, ClinicId = clinicKhj.Id,
            Employee = staffEmployeeKhj, EmployeeId = staffEmployeeKhj.Id,
            DepartmentId = deptReception.Id, PositionId = positionAdmin.Id,
            HireDate = new DateOnly(2023, 5, 2), EffectiveFrom = new DateOnly(2023, 5, 2), IsCurrent = true,
            BaseSalary = 4200m, EmploymentType = EmploymentType.FullTime,
        };

        // --- Шаблоны адаптации (раздел 25, 31-32) ---
        // Общий — подходит всем (все условия null). Reception/Doctor — более
        // специфичные, побеждают общий при найме на соответствующую категорию
        // (см. OnboardingChecklistTemplateSelector).
        var generalOnboardingTemplate = new OnboardingChecklistTemplate { Name = "Общий чеклист адаптации" };
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Preboarding, "Отправить оффер и пакет документов на подпись", OnboardingResponsible.Hr, 0);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Preboarding, "Подготовить рабочее место и оборудование", OnboardingResponsible.ItAdmin, 1);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Day1, "Провести вводный инструктаж и знакомство с командой", OnboardingResponsible.DirectManager, 0);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Day1, "Выдать пропуск, создать корпоративную почту и учётные записи", OnboardingResponsible.ItAdmin, 1);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Week1, "Пройти вводный инструктаж по охране труда", OnboardingResponsible.Hr, 0);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Day30, "Промежуточная встреча по итогам первого месяца", OnboardingResponsible.DirectManager, 0);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Day60, "Оценка прогресса на 60-й день", OnboardingResponsible.DirectManager, 0);
        AddOnboardingTask(generalOnboardingTemplate, OnboardingStage.Day90, "Итоговая встреча по завершении испытательного срока", OnboardingResponsible.DirectManager, 0);

        var receptionOnboardingTemplate = new OnboardingChecklistTemplate { Name = "Чеклист адаптации: Ресепшн", PersonnelCategory = PersonnelCategory.Reception };
        AddOnboardingTask(receptionOnboardingTemplate, OnboardingStage.Preboarding, "Отправить оффер и пакет документов на подпись", OnboardingResponsible.Hr, 0);
        AddOnboardingTask(receptionOnboardingTemplate, OnboardingStage.Day1, "Обучение работе с CRM и телефонией ресепшн", OnboardingResponsible.DirectManager, 0);
        AddOnboardingTask(receptionOnboardingTemplate, OnboardingStage.Week1, "Самостоятельная смена под наблюдением наставника", OnboardingResponsible.DirectManager, 0);
        AddOnboardingTask(receptionOnboardingTemplate, OnboardingStage.Day30, "Оценка по стандартам обслуживания пациентов", OnboardingResponsible.DirectManager, 0);

        var doctorOnboardingTemplate = new OnboardingChecklistTemplate { Name = "Чеклист адаптации: врач", PersonnelCategory = PersonnelCategory.Doctor };
        AddOnboardingTask(doctorOnboardingTemplate, OnboardingStage.Preboarding, "Проверить действительность медицинской лицензии и сертификатов", OnboardingResponsible.Hr, 0);
        AddOnboardingTask(doctorOnboardingTemplate, OnboardingStage.Day1, "Оформить допуск в медицинскую информационную систему (МИС)", OnboardingResponsible.ItAdmin, 0);
        AddOnboardingTask(doctorOnboardingTemplate, OnboardingStage.Week1, "Ознакомить с клиническими протоколами клиники", OnboardingResponsible.DirectManager, 0);
        AddOnboardingTask(doctorOnboardingTemplate, OnboardingStage.Day90, "Аттестация по итогам испытательного срока главным врачом", OnboardingResponsible.DirectManager, 0);

        // --- Роли (раздел 64-65) ---
        var superAdminRole = new Role { Code = "SuperAdmin", Name = "Суперадминистратор", IsSystemRole = true };
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Employee", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Vacancy", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Vacancy", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Vacancy", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "BulkImport", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "BulkImport", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "BulkImport", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "OnboardingTemplate", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "OnboardingTemplate", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "OnboardingChecklist", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "OnboardingChecklist", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "OnboardingChecklist", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });

        var hrManagerRole = new Role { Code = "HRManager", Name = "HR-менеджер клиники", IsSystemRole = true };
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "Employee", Action = PermissionAction.View, Scope = PermissionScope.Clinic, RestrictedFields = "BankAccount,NationalId" });

        var deptManagerRole = new Role { Code = "DepartmentManager", Name = "Руководитель отдела", IsSystemRole = true };
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "Employee", Action = PermissionAction.View, Scope = PermissionScope.OwnEmployees, RestrictedFields = "Salary,BankAccount,NationalId" });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "OnboardingChecklist", Action = PermissionAction.View, Scope = PermissionScope.OwnEmployees });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "OnboardingChecklist", Action = PermissionAction.Edit, Scope = PermissionScope.OwnEmployees });

        // --- Пользователи + назначения ролей ---
        var adminUser = new AppUser { Email = "admin@tibbinav.local", DisplayName = "Суперадминистратор", IsActive = true };
        adminUser.UserRoles.Add(new UserRole { User = adminUser, Role = superAdminRole });

        var hrUser = new AppUser { Email = "hr.dus@tibbinav.local", DisplayName = "HR клиники DUS", IsActive = true };
        hrUser.UserRoles.Add(new UserRole { User = hrUser, Role = hrManagerRole, ScopeClinicId = clinicDus.Id });

        var managerUser = new AppUser { Email = "manager.dus@tibbinav.local", DisplayName = "Фарзона Рахимова", EmployeeId = managerEmployee.Id, IsActive = true };
        managerUser.UserRoles.Add(new UserRole { User = managerUser, Role = deptManagerRole });

        db.Organizations.Add(org);
        db.Clinics.AddRange(clinicDus, clinicKhj);
        db.Departments.Add(deptReception);
        db.Positions.Add(positionAdmin);
        db.Employees.AddRange(managerEmployee, staffEmployeeDus, staffEmployeeKhj);
        db.EmploymentRecords.AddRange(employmentDus, employmentKhj);
        db.Roles.AddRange(superAdminRole, hrManagerRole, deptManagerRole);
        db.Users.AddRange(adminUser, hrUser, managerUser);
        db.OnboardingChecklistTemplates.AddRange(generalOnboardingTemplate, receptionOnboardingTemplate, doctorOnboardingTemplate);

        await db.SaveChangesAsync(ct);
    }

    private static void AddOnboardingTask(
        OnboardingChecklistTemplate template, OnboardingStage stage, string title, OnboardingResponsible responsible, int orderIndex)
    {
        template.Tasks.Add(new OnboardingChecklistTemplateTask
        {
            Template = template,
            Stage = stage,
            Title = title,
            Responsible = responsible,
            OrderIndex = orderIndex,
        });
    }
}
