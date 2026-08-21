using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.BulkImport;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Organization;
using TibbiNav.Domain.Recruitment;

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
