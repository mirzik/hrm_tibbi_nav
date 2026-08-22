using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Attendance;
using TibbiNav.Domain.BulkImport;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Documents;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Kpi;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;
using TibbiNav.Domain.Recruitment;
using TibbiNav.Domain.ServiceDesk;
using TibbiNav.Domain.Workflow;

namespace TibbiNav.Infrastructure;

public class TibbiNavDbContext(DbContextOptions<TibbiNavDbContext> options) : DbContext(options)
{
    // Organization
    public DbSet<OrganizationUnit> Organizations => Set<OrganizationUnit>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();

    // Identity / RBAC
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ExternalExpertAccess> ExternalExpertAccesses => Set<ExternalExpertAccess>();

    // Employees
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmploymentRecord> EmploymentRecords => Set<EmploymentRecord>();
    public DbSet<MedicalCredential> MedicalCredentials => Set<MedicalCredential>();

    // Recruitment
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateApplication> CandidateApplications => Set<CandidateApplication>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<Offer> Offers => Set<Offer>();

    // Core
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    // Bulk Import (раздел 63)
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    // Onboarding (раздел 25, 31-32)
    public DbSet<OnboardingChecklistTemplate> OnboardingChecklistTemplates => Set<OnboardingChecklistTemplate>();
    public DbSet<OnboardingChecklistTemplateTask> OnboardingChecklistTemplateTasks => Set<OnboardingChecklistTemplateTask>();
    public DbSet<OnboardingChecklist> OnboardingChecklists => Set<OnboardingChecklist>();
    public DbSet<OnboardingChecklistTask> OnboardingChecklistTasks => Set<OnboardingChecklistTask>();

    // Document Generator (раздел 29-30)
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<DocumentTemplateBlock> DocumentTemplateBlocks => Set<DocumentTemplateBlock>();

    // Workflow Engine (раздел 68)
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowCondition> WorkflowConditions => Set<WorkflowCondition>();
    public DbSet<WorkflowStepDefinition> WorkflowStepDefinitions => Set<WorkflowStepDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStepInstance> WorkflowStepInstances => Set<WorkflowStepInstance>();

    // Attendance / Timesheet (раздел 35-39)
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<TimesheetDay> TimesheetDays => Set<TimesheetDay>();
    public DbSet<TimesheetClosure> TimesheetClosures => Set<TimesheetClosure>();
    public DbSet<TimesheetEditLog> TimesheetEditLogs => Set<TimesheetEditLog>();
    public DbSet<DepartmentLeaveThreshold> DepartmentLeaveThresholds => Set<DepartmentLeaveThreshold>();

    // HR Service Desk (раздел 54)
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    // KPI (раздел 45-46)
    public DbSet<KpiTemplate> KpiTemplates => Set<KpiTemplate>();
    public DbSet<KpiAssignment> KpiAssignments => Set<KpiAssignment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // --- Organization ---
        b.Entity<Clinic>().HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        b.Entity<Clinic>().HasOne(x => x.Organization).WithMany(x => x.Clinics).HasForeignKey(x => x.OrganizationId);
        b.Entity<Department>().HasOne(x => x.Clinic).WithMany(x => x.Departments).HasForeignKey(x => x.ClinicId);
        b.Entity<Department>().HasOne(x => x.ParentDepartment).WithMany().HasForeignKey(x => x.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Position>().HasOne(x => x.Department).WithMany(x => x.Positions).HasForeignKey(x => x.DepartmentId);

        // --- Employees ---
        b.Entity<Employee>().HasIndex(x => x.EmployeeCode).IsUnique();
        b.Entity<Employee>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<EmploymentRecord>()
            .HasOne(x => x.Employee).WithMany(x => x.EmploymentRecords).HasForeignKey(x => x.EmployeeId);
        // Effective dating: только одна "текущая" запись employment на сотрудника
        b.Entity<EmploymentRecord>().HasIndex(x => new { x.EmployeeId, x.IsCurrent });
        b.Entity<MedicalCredential>().HasOne(x => x.Employee).WithMany(x => x.MedicalCredentials).HasForeignKey(x => x.EmployeeId);

        // --- Recruitment ---
        b.Entity<CandidateApplication>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => x.CandidateId);
        b.Entity<CandidateApplication>().HasOne(x => x.Vacancy).WithMany().HasForeignKey(x => x.VacancyId);
        b.Entity<CandidateApplication>().HasIndex(x => new { x.CandidateId, x.VacancyId }).IsUnique();
        b.Entity<Offer>().HasOne<CandidateApplication>().WithOne(x => x.Offer).HasForeignKey<Offer>(x => x.CandidateApplicationId);

        // --- Onboarding ---
        b.Entity<OnboardingChecklistTemplateTask>().HasOne(x => x.Template).WithMany(x => x.Tasks).HasForeignKey(x => x.TemplateId);
        b.Entity<OnboardingChecklistTask>().HasOne(x => x.Checklist).WithMany(x => x.Tasks).HasForeignKey(x => x.ChecklistId);
        b.Entity<OnboardingChecklist>().HasIndex(x => x.EmployeeId);

        // --- Document Generator ---
        b.Entity<DocumentTemplateBlock>().HasOne(x => x.Template).WithMany(x => x.Blocks).HasForeignKey(x => x.TemplateId);
        b.Entity<EmployeeDocument>().HasIndex(x => x.EmployeeId);

        // --- Workflow Engine ---
        b.Entity<WorkflowCondition>().HasOne(x => x.WorkflowDefinition).WithMany(x => x.Conditions).HasForeignKey(x => x.WorkflowDefinitionId);
        b.Entity<WorkflowStepDefinition>().HasOne(x => x.WorkflowDefinition).WithMany(x => x.Steps).HasForeignKey(x => x.WorkflowDefinitionId);
        b.Entity<WorkflowStepInstance>().HasOne(x => x.WorkflowInstance).WithMany(x => x.Steps).HasForeignKey(x => x.WorkflowInstanceId);
        b.Entity<WorkflowInstance>().HasIndex(x => new { x.EntityType, x.EntityId });

        // --- Attendance / Timesheet ---
        b.Entity<TimesheetDay>().HasOne(x => x.Timesheet).WithMany(x => x.Days).HasForeignKey(x => x.TimesheetId);
        b.Entity<Timesheet>().HasIndex(x => new { x.EmployeeId, x.PeriodStart, x.PeriodEnd }).IsUnique();
        b.Entity<TimesheetClosure>().HasIndex(x => new { x.DepartmentId, x.PeriodStart, x.PeriodEnd });
        b.Entity<DepartmentLeaveThreshold>().HasIndex(x => x.DepartmentId).IsUnique();

        // --- HR Service Desk ---
        b.Entity<TicketComment>().HasOne(x => x.Ticket).WithMany(x => x.Comments).HasForeignKey(x => x.TicketId);
        b.Entity<Ticket>().HasIndex(x => x.EmployeeId);

        // --- KPI ---
        b.Entity<KpiAssignment>().HasOne(x => x.KpiTemplate).WithMany(x => x.Assignments).HasForeignKey(x => x.KpiTemplateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KpiAssignment>().HasIndex(x => x.EmployeeId);
        b.Entity<KpiAssignment>().HasIndex(x => x.DepartmentId);

        // --- Identity ---
        b.Entity<Role>().HasIndex(x => x.Code).IsUnique();
        b.Entity<UserRole>().HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
        b.Entity<UserRole>().HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        b.Entity<RolePermission>().HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId);

        // Все timestamp-поля храним как timestamptz / UTC (раздел 83)
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            foreach (var prop in entityType.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime) || prop.ClrType == typeof(DateTime?))
                    prop.SetColumnType("timestamptz");
            }
        }
    }
}
