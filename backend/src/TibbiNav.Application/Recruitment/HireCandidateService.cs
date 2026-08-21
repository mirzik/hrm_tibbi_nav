using Microsoft.EntityFrameworkCore;
using TibbiNav.Application.Employees;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Recruitment;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Recruitment;

/// <summary>
/// Реализует раздел 28 ТЗ: кнопка "Hire Candidate" одним действием
/// 1) создаёт Employee Profile, 2) присваивает Employee ID, 3) создаёт Employment,
/// 4) закрывает вакансию, 5) переводит application в Hired.
/// (перенос документов и запуск onboarding — отдельные обработчики domain event,
/// здесь оставлен TODO-хук, чтобы не связывать модули напрямую)
/// </summary>
public class HireCandidateService(TibbiNavDbContext db, EmployeeCodeGenerator codeGenerator)
{
    public async Task<Employee> HireAsync(Guid candidateApplicationId, DateOnly hireDate, CancellationToken ct = default)
    {
        var application = await db.CandidateApplications
            .Include(a => a.Candidate)
            .Include(a => a.Vacancy)
            .Include(a => a.Offer)
            .FirstOrDefaultAsync(a => a.Id == candidateApplicationId, ct)
            ?? throw new InvalidOperationException("Заявка кандидата не найдена");

        if (application.Offer is null || application.Offer.Status != OfferStatus.Accepted)
            throw new InvalidOperationException("Нельзя нанять кандидата без принятого оффера");

        var vacancy = application.Vacancy;
        var clinicId = vacancy.ClinicId
            ?? throw new InvalidOperationException("У вакансии не указана клиника");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var employee = new Employee
        {
            OrganizationId = vacancy.OrganizationId,
            ClinicId = clinicId,
            EmployeeCode = await codeGenerator.GenerateAsync(clinicId, ct),
            FullName = application.Candidate.FullName,
            Citizenship = "TJ", // уточняется в preboarding checklist
            Status = EmployeeStatus.Active,
            Gender = Gender.Male, // placeholder — заполняется в preboarding
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
        };
        db.Employees.Add(employee);

        var employment = new EmploymentRecord
        {
            OrganizationId = vacancy.OrganizationId,
            ClinicId = clinicId,
            EmployeeId = employee.Id,
            DepartmentId = vacancy.DepartmentId,
            PositionId = vacancy.PositionId,
            HireDate = hireDate,
            EffectiveFrom = hireDate,
            IsCurrent = true,
            Fte = application.Offer.Fte,
            BaseSalary = application.Offer.Salary,
            EmploymentType = EmploymentType.FullTime,
            ProbationEndDate = hireDate.AddDays(application.Offer.ProbationDays),
            ChangeReason = "Первичный найм",
        };
        db.EmploymentRecords.Add(employment);

        application.Stage = PipelineStage.Hired;
        vacancy.Status = VacancyStatus.Closed;

        // TODO: publish EmployeeHiredEvent -> Onboarding module запускает checklist (раздел 31-32),
        // Access Management module создаёт access requests (раздел 52), Documents переносит пакет (раздел 30).

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return employee;
    }
}
