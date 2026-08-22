using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Attendance;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Documents;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;
using TibbiNav.Domain.Workflow;

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
/// OnboardingChecklistTemplateSelector выбирает более специфичный, 4 шаблона
/// документов (раздел 29) — по одному на EmployeeDocumentType, 3 маршрута
/// согласования (раздел 68: Vacancy/LeaveRequest/TimesheetClosure) и отдел
/// "Хирургия" с 4 врачами + порог 50% (раздел 39: Leave conflict engine).
///
/// Раздел 53-54: у каждого из 4 врачей — свой self-service AppUser (роль
/// Employee, по CorporateEmail surgeon{N}.dus@tibbinav.local) для проверки
/// Employee Self Service и HR Service Desk, в т.ч. что один сотрудник не
/// видит данные/тикеты другого.
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
        // Отдельный Department для клиники KHJ — не переиспользуем deptReception
        // (принадлежащий clinicDus) для сотрудника из другой клиники: иначе
        // Leave conflict engine (раздел 39) считал бы сотрудников разных клиник
        // "коллегами" по одному и тому же DepartmentId.
        var deptReceptionKhj = new Department { OrganizationId = org.Id, Clinic = clinicKhj, ClinicId = clinicKhj.Id, Name = "Ресепшн" };

        var positionAdmin = new Position
        {
            OrganizationId = org.Id, ClinicId = clinicDus.Id,
            Department = deptReception, DepartmentId = deptReception.Id,
            Title = "Администратор", Category = PersonnelCategory.Reception,
        };
        var positionAdminKhj = new Position
        {
            OrganizationId = org.Id, ClinicId = clinicKhj.Id,
            Department = deptReceptionKhj, DepartmentId = deptReceptionKhj.Id,
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
            DepartmentId = deptReceptionKhj.Id, PositionId = positionAdminKhj.Id,
            HireDate = new DateOnly(2023, 5, 2), EffectiveFrom = new DateOnly(2023, 5, 2), IsCurrent = true,
            BaseSalary = 4200m, EmploymentType = EmploymentType.FullTime,
        };

        // --- Отдел "Хирургия" с 4 врачами (раздел 39: демо Leave conflict engine —
        // "в период отсутствуют 2 из 4 врачей отделения" ровно этой группой) ---
        var deptSurgery = new Department { OrganizationId = org.Id, Clinic = clinicDus, ClinicId = clinicDus.Id, Name = "Хирургия" };
        var positionSurgeon = new Position
        {
            OrganizationId = org.Id, ClinicId = clinicDus.Id,
            Department = deptSurgery, DepartmentId = deptSurgery.Id,
            Title = "Врач-хирург", Category = PersonnelCategory.Doctor,
        };

        var surgeons = new List<Employee>();
        var surgeonEmployments = new List<EmploymentRecord>();
        var surgeonNames = new[] { "Далер Юсупов", "Нигора Сафарова", "Комрон Исмоилов", "Зарина Холова" };
        for (var i = 0; i < surgeonNames.Length; i++)
        {
            var surgeon = new Employee
            {
                OrganizationId = org.Id, ClinicId = clinicDus.Id, EmployeeCode = $"TN-DUS-{(i + 3):D6}",
                FullName = surgeonNames[i], Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                DateOfBirth = new DateOnly(1980 + i, 4, 10), Citizenship = "TJ",
                CorporateEmail = $"surgeon{i + 1}.dus@tibbinav.local",
            };
            surgeons.Add(surgeon);
            surgeonEmployments.Add(new EmploymentRecord
            {
                OrganizationId = org.Id, ClinicId = clinicDus.Id,
                Employee = surgeon, EmployeeId = surgeon.Id,
                DepartmentId = deptSurgery.Id, PositionId = positionSurgeon.Id,
                ManagerEmployeeId = managerEmployee.Id,
                HireDate = new DateOnly(2021, 1, 1), EffectiveFrom = new DateOnly(2021, 1, 1), IsCurrent = true,
                BaseSalary = 9000m, EmploymentType = EmploymentType.FullTime,
            });
        }

        // Порог для "Хирургии" — 50% (раздел 39: конфигурируемо по подразделению,
        // не хардкод; для остальных подразделений применяется дефолт из
        // LeaveConflictChecker.DefaultThresholdPercent).
        var surgeryLeaveThreshold = new DepartmentLeaveThreshold { DepartmentId = deptSurgery.Id, MaxConcurrentAbsencePercent = 50 };

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

        // --- Шаблоны документов (раздел 29) ---
        // Плейсхолдеры — см. DocumentPlaceholderResolver. По одному активному
        // шаблону на EmployeeDocumentType — этого достаточно для стандартного
        // пакета, генерируемого автоматически при найме.
        var contractTemplate = new DocumentTemplate { Name = "Трудовой договор", DocumentType = EmployeeDocumentType.EmploymentContract };
        AddDocumentBlock(contractTemplate, DocumentBlockKind.Title, "ТРУДОВОЙ ДОГОВОР", bold: true, 0);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.Paragraph,
            "{{OrganizationLegalName}}, именуемое в дальнейшем «Работодатель», и {{FullName}}, именуемый(ая) в дальнейшем «Работник», заключили настоящий договор о нижеследующем:", false, 1);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.FieldLine, "Должность: {{PositionTitle}}", false, 2);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.FieldLine, "Подразделение: {{DepartmentName}}, {{ClinicName}}", false, 3);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.FieldLine, "Дата начала работы: {{HireDate}}", false, 4);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.FieldLine, "Вид занятости: {{EmploymentType}}, ставка {{Fte}}", false, 5);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.FieldLine, "Оклад: {{BaseSalary}}", false, 6);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.FieldLine, "Испытательный срок до: {{ProbationEndDate}}", false, 7);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.Paragraph,
            "Работник обязуется добросовестно исполнять трудовые обязанности, соблюдать правила внутреннего трудового распорядка и требования охраны труда.", false, 8);
        AddDocumentBlock(contractTemplate, DocumentBlockKind.SignatureLine, "Работодатель: _____________________     Работник: {{FullName}} _____________________", false, 9);

        var hireOrderTemplate = new DocumentTemplate { Name = "Приказ о приёме на работу", DocumentType = EmployeeDocumentType.HireOrder };
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.Title, "ПРИКАЗ О ПРИЁМЕ НА РАБОТУ от {{TodayDate}}", true, 0);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.Paragraph, "ПРИНЯТЬ:", true, 1);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.FieldLine, "{{FullName}}, табельный номер {{EmployeeCode}}", false, 2);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.FieldLine, "на должность: {{PositionTitle}}", false, 3);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.FieldLine, "в подразделение: {{DepartmentName}} ({{ClinicName}})", false, 4);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.FieldLine, "с {{HireDate}}, ставка {{Fte}}, оклад {{BaseSalary}}", false, 5);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.Paragraph, "Основание: трудовой договор от {{HireDate}}.", false, 6);
        AddDocumentBlock(hireOrderTemplate, DocumentBlockKind.SignatureLine, "Руководитель: _____________________", false, 7);

        var ndaTemplate = new DocumentTemplate { Name = "Соглашение о неразглашении", DocumentType = EmployeeDocumentType.Nda };
        AddDocumentBlock(ndaTemplate, DocumentBlockKind.Title, "СОГЛАШЕНИЕ О НЕРАЗГЛАШЕНИИ КОНФИДЕНЦИАЛЬНОЙ ИНФОРМАЦИИ", true, 0);
        AddDocumentBlock(ndaTemplate, DocumentBlockKind.Paragraph,
            "{{FullName}}, занимающий(ая) должность {{PositionTitle}} в {{ClinicName}}, обязуется не разглашать третьим лицам конфиденциальную информацию Работодателя, ставшую известной в связи с исполнением трудовых обязанностей, включая медицинские данные пациентов, коммерческую и служебную информацию.", false, 1);
        AddDocumentBlock(ndaTemplate, DocumentBlockKind.Paragraph,
            "Настоящее обязательство действует в течение всего периода работы и 3 (трёх) лет после увольнения.", false, 2);
        AddDocumentBlock(ndaTemplate, DocumentBlockKind.SignatureLine, "Работник: {{FullName}} _____________________     Дата: {{TodayDate}}", false, 3);

        var consentTemplate = new DocumentTemplate { Name = "Согласие на обработку персональных данных", DocumentType = EmployeeDocumentType.PersonalDataConsent };
        AddDocumentBlock(consentTemplate, DocumentBlockKind.Title, "СОГЛАСИЕ НА ОБРАБОТКУ ПЕРСОНАЛЬНЫХ ДАННЫХ", true, 0);
        AddDocumentBlock(consentTemplate, DocumentBlockKind.Paragraph,
            "Я, {{FullName}}, гражданство {{Citizenship}}, дата рождения {{DateOfBirth}}, даю согласие {{OrganizationLegalName}} на обработку моих персональных данных в целях кадрового учёта и исполнения трудового договора.", false, 1);
        AddDocumentBlock(consentTemplate, DocumentBlockKind.Paragraph,
            "Согласие действует на весь период трудовых отношений; {{OrganizationLegalName}} обязуется обеспечить конфиденциальность и защиту предоставленных данных.", false, 2);
        AddDocumentBlock(consentTemplate, DocumentBlockKind.SignatureLine, "Работник: {{FullName}} _____________________     Дата: {{TodayDate}}", false, 3);

        // --- Маршруты согласования (раздел 68) ---
        // Vacancy (раздел 16): Manager → HR → Finance → ChiefDoctor → GeneralDirector.
        // ChiefDoctor эскалируется на GeneralDirector по истечении SLA — демонстрация
        // ReassignToRole (не обязательно сработает в обычном E2E-прогоне, SLA
        // достаточно большой, чтобы не мешать ручному тестированию).
        var vacancyWorkflow = new WorkflowDefinition { Name = "Согласование вакансии", EntityType = "Vacancy", Priority = 0 };
        AddWorkflowStep(vacancyWorkflow, 0, "Согласование руководителем", WorkflowApproverStrategy.RoleInClinic, "DepartmentManager", 48);
        AddWorkflowStep(vacancyWorkflow, 1, "Согласование HR", WorkflowApproverStrategy.RoleInClinic, "HRManager", 48);
        AddWorkflowStep(vacancyWorkflow, 2, "Согласование финансового отдела", WorkflowApproverStrategy.RoleInOrganization, "Finance", 72);
        AddWorkflowStep(vacancyWorkflow, 3, "Согласование главного врача", WorkflowApproverStrategy.RoleInClinic, "ChiefDoctor", 48,
            WorkflowEscalationAction.ReassignToRole, "GeneralDirector");
        AddWorkflowStep(vacancyWorkflow, 4, "Согласование генерального директора", WorkflowApproverStrategy.RoleInOrganization, "GeneralDirector", 72);

        // LeaveRequest (раздел 37): один шаг — руководитель подразделения
        // сотрудника-заявителя (DirectManager, резолвится через EmploymentRecord).
        var leaveWorkflow = new WorkflowDefinition { Name = "Согласование отпуска", EntityType = "LeaveRequest", Priority = 0 };
        AddWorkflowStep(leaveWorkflow, 0, "Согласование руководителем подразделения", WorkflowApproverStrategy.DirectManager, null, 24);

        // TimesheetClosure (раздел 36): Department Manager → HR → Accounting → Locked.
        // RoleInDepartment — впервые используемая здесь стратегия (раньше и
        // Vacancy, и LeaveRequest обходились Clinic-уровнем/DirectManager).
        var timesheetClosureWorkflow = new WorkflowDefinition { Name = "Закрытие табеля", EntityType = "TimesheetClosure", Priority = 0 };
        AddWorkflowStep(timesheetClosureWorkflow, 0, "Согласование руководителем подразделения", WorkflowApproverStrategy.RoleInDepartment, "DepartmentManager", 48);
        AddWorkflowStep(timesheetClosureWorkflow, 1, "Согласование HR", WorkflowApproverStrategy.RoleInClinic, "HRManager", 48);
        AddWorkflowStep(timesheetClosureWorkflow, 2, "Согласование бухгалтерией", WorkflowApproverStrategy.RoleInOrganization, "Accounting", 72);

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
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "DocumentTemplate", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "DocumentTemplate", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "EmployeeDocument", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "EmployeeDocument", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "EmployeeDocument", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "EmployeeDocument", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "WorkflowDefinition", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "WorkflowDefinition", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "LeaveRequest", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "LeaveRequest", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Timesheet", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Timesheet", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Timesheet", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Timesheet", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "TimesheetClosure", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "TimesheetClosure", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "DepartmentLeaveThreshold", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "DepartmentLeaveThreshold", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Ticket", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "Ticket", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "KpiTemplate", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "KpiTemplate", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "KpiTemplate", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "KpiAssignment", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "KpiAssignment", Action = PermissionAction.Create, Scope = PermissionScope.Organization });
        superAdminRole.Permissions.Add(new RolePermission { Role = superAdminRole, Resource = "KpiAssignment", Action = PermissionAction.Edit, Scope = PermissionScope.Organization });

        var hrManagerRole = new Role { Code = "HRManager", Name = "HR-менеджер клиники", IsSystemRole = true };
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "Employee", Action = PermissionAction.View, Scope = PermissionScope.Clinic, RestrictedFields = "BankAccount,NationalId" });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "Timesheet", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "Timesheet", Action = PermissionAction.Edit, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "TimesheetClosure", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "LeaveRequest", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "Ticket", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "Ticket", Action = PermissionAction.Edit, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "KpiTemplate", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "KpiTemplate", Action = PermissionAction.Create, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "KpiTemplate", Action = PermissionAction.Edit, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "KpiAssignment", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "KpiAssignment", Action = PermissionAction.Create, Scope = PermissionScope.Clinic });
        hrManagerRole.Permissions.Add(new RolePermission { Role = hrManagerRole, Resource = "KpiAssignment", Action = PermissionAction.Edit, Scope = PermissionScope.Clinic });

        var deptManagerRole = new Role { Code = "DepartmentManager", Name = "Руководитель отдела", IsSystemRole = true };
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "Employee", Action = PermissionAction.View, Scope = PermissionScope.OwnEmployees, RestrictedFields = "Salary,BankAccount,NationalId" });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "OnboardingChecklist", Action = PermissionAction.View, Scope = PermissionScope.OwnEmployees });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "OnboardingChecklist", Action = PermissionAction.Edit, Scope = PermissionScope.OwnEmployees });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "EmployeeDocument", Action = PermissionAction.View, Scope = PermissionScope.OwnEmployees });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "Timesheet", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "Timesheet", Action = PermissionAction.Edit, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "TimesheetClosure", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "TimesheetClosure", Action = PermissionAction.Create, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "LeaveRequest", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "LeaveRequest", Action = PermissionAction.Create, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "KpiTemplate", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "KpiAssignment", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        deptManagerRole.Permissions.Add(new RolePermission { Role = deptManagerRole, Resource = "KpiAssignment", Action = PermissionAction.Edit, Scope = PermissionScope.Clinic });

        // Раздел 16: последние 3 шага маршрута вакансии — организационные роли,
        // ещё не имевшие поводов появиться в сидере до Workflow Engine.
        var financeRole = new Role { Code = "Finance", Name = "Финансовый отдел", IsSystemRole = true };
        financeRole.Permissions.Add(new RolePermission { Role = financeRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        financeRole.Permissions.Add(new RolePermission { Role = financeRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });

        var chiefDoctorRole = new Role { Code = "ChiefDoctor", Name = "Главный врач", IsSystemRole = true };
        chiefDoctorRole.Permissions.Add(new RolePermission { Role = chiefDoctorRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Clinic });
        chiefDoctorRole.Permissions.Add(new RolePermission { Role = chiefDoctorRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Clinic });

        var generalDirectorRole = new Role { Code = "GeneralDirector", Name = "Генеральный директор", IsSystemRole = true };
        generalDirectorRole.Permissions.Add(new RolePermission { Role = generalDirectorRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        generalDirectorRole.Permissions.Add(new RolePermission { Role = generalDirectorRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });

        // Раздел 36: последний шаг маршрута закрытия табеля.
        var accountingRole = new Role { Code = "Accounting", Name = "Бухгалтерия", IsSystemRole = true };
        accountingRole.Permissions.Add(new RolePermission { Role = accountingRole, Resource = "WorkflowInstance", Action = PermissionAction.View, Scope = PermissionScope.Organization });
        accountingRole.Permissions.Add(new RolePermission { Role = accountingRole, Resource = "WorkflowInstance", Action = PermissionAction.Approve, Scope = PermissionScope.Organization });
        accountingRole.Permissions.Add(new RolePermission { Role = accountingRole, Resource = "Timesheet", Action = PermissionAction.View, Scope = PermissionScope.Organization });

        // Раздел 53-54: базовая роль рядового сотрудника — доступ только к
        // /api/v1/me/* (SelfService), сама фильтрация "только свои данные"
        // жёстко зашита в MeController по scope.EmployeeId, а не зависит от
        // Scope этого Permission (см. комментарий в MeController).
        var employeeRole = new Role { Code = "Employee", Name = "Сотрудник (self-service)", IsSystemRole = true };
        employeeRole.Permissions.Add(new RolePermission { Role = employeeRole, Resource = "SelfService", Action = PermissionAction.View, Scope = PermissionScope.Self });
        employeeRole.Permissions.Add(new RolePermission { Role = employeeRole, Resource = "SelfService", Action = PermissionAction.Create, Scope = PermissionScope.Self });

        // --- Пользователи + назначения ролей ---
        var adminUser = new AppUser { Email = "admin@tibbinav.local", DisplayName = "Суперадминистратор", IsActive = true };
        adminUser.UserRoles.Add(new UserRole { User = adminUser, Role = superAdminRole });

        var hrUser = new AppUser { Email = "hr.dus@tibbinav.local", DisplayName = "HR клиники DUS", IsActive = true };
        hrUser.UserRoles.Add(new UserRole { User = hrUser, Role = hrManagerRole, ScopeClinicId = clinicDus.Id });

        var managerUser = new AppUser { Email = "manager.dus@tibbinav.local", DisplayName = "Фарзона Рахимова", EmployeeId = managerEmployee.Id, IsActive = true };
        managerUser.UserRoles.Add(new UserRole { User = managerUser, Role = deptManagerRole, ScopeClinicId = clinicDus.Id });

        var financeUser = new AppUser { Email = "finance@tibbinav.local", DisplayName = "Финансовый отдел", IsActive = true };
        financeUser.UserRoles.Add(new UserRole { User = financeUser, Role = financeRole }); // организационная роль — без ScopeClinicId

        var chiefDoctorUser = new AppUser { Email = "chiefdoctor.dus@tibbinav.local", DisplayName = "Главный врач DUS", IsActive = true };
        chiefDoctorUser.UserRoles.Add(new UserRole { User = chiefDoctorUser, Role = chiefDoctorRole, ScopeClinicId = clinicDus.Id });

        var generalDirectorUser = new AppUser { Email = "director@tibbinav.local", DisplayName = "Генеральный директор", IsActive = true };
        generalDirectorUser.UserRoles.Add(new UserRole { User = generalDirectorUser, Role = generalDirectorRole });

        var accountingUser = new AppUser { Email = "accounting@tibbinav.local", DisplayName = "Бухгалтерия", IsActive = true };
        accountingUser.UserRoles.Add(new UserRole { User = accountingUser, Role = accountingRole });

        // Раздел 53: self-service доступ для каждого из 4 врачей — по их же
        // CorporateEmail (surgeon{N}.dus@tibbinav.local). Позволяет проверить
        // и Self Service (каждый видит только себя), и Service Desk
        // (сотрудник создаёт тикет о себе), и что один сотрудник не видит
        // данные/тикеты другого (раздел 65: Scope=Self).
        var selfServiceUsers = surgeons.Select(surgeon => new AppUser
        {
            Email = surgeon.CorporateEmail!, DisplayName = surgeon.FullName, EmployeeId = surgeon.Id, IsActive = true,
        }).ToList();
        foreach (var user in selfServiceUsers)
            user.UserRoles.Add(new UserRole { User = user, Role = employeeRole });

        db.Organizations.Add(org);
        db.Clinics.AddRange(clinicDus, clinicKhj);
        db.Departments.AddRange(deptReception, deptReceptionKhj, deptSurgery);
        db.Positions.AddRange(positionAdmin, positionAdminKhj, positionSurgeon);
        db.Employees.AddRange(managerEmployee, staffEmployeeDus, staffEmployeeKhj);
        db.Employees.AddRange(surgeons);
        db.EmploymentRecords.AddRange(employmentDus, employmentKhj);
        db.EmploymentRecords.AddRange(surgeonEmployments);
        db.DepartmentLeaveThresholds.Add(surgeryLeaveThreshold);
        db.Roles.AddRange(superAdminRole, hrManagerRole, deptManagerRole, financeRole, chiefDoctorRole, generalDirectorRole, accountingRole, employeeRole);
        db.Users.AddRange(adminUser, hrUser, managerUser, financeUser, chiefDoctorUser, generalDirectorUser, accountingUser);
        db.Users.AddRange(selfServiceUsers);
        db.OnboardingChecklistTemplates.AddRange(generalOnboardingTemplate, receptionOnboardingTemplate, doctorOnboardingTemplate);
        db.DocumentTemplates.AddRange(contractTemplate, hireOrderTemplate, ndaTemplate, consentTemplate);
        db.WorkflowDefinitions.AddRange(vacancyWorkflow, leaveWorkflow, timesheetClosureWorkflow);

        await db.SaveChangesAsync(ct);
    }

    private static void AddWorkflowStep(
        WorkflowDefinition definition, int orderIndex, string name, WorkflowApproverStrategy strategy, string? roleCode,
        int slaHours, WorkflowEscalationAction escalationAction = WorkflowEscalationAction.None, string? escalationRoleCode = null)
    {
        definition.Steps.Add(new WorkflowStepDefinition
        {
            WorkflowDefinition = definition,
            OrderIndex = orderIndex,
            Name = name,
            ApproverStrategy = strategy,
            ApproverRoleCode = roleCode,
            SlaHours = slaHours,
            EscalationAction = escalationAction,
            EscalationRoleCode = escalationRoleCode,
        });
    }

    private static void AddDocumentBlock(
        DocumentTemplate template, DocumentBlockKind kind, string text, bool bold, int orderIndex)
    {
        template.Blocks.Add(new DocumentTemplateBlock
        {
            Template = template,
            Kind = kind,
            Text = text,
            Bold = bold,
            OrderIndex = orderIndex,
        });
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
