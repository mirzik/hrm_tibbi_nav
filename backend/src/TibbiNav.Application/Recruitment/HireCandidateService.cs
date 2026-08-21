using Microsoft.EntityFrameworkCore;
using TibbiNav.Application.Documents;
using TibbiNav.Application.Employees;
using TibbiNav.Application.Onboarding;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Recruitment;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Recruitment;

/// <summary>
/// Реализует раздел 28 ТЗ: кнопка "Hire Candidate" одним действием
/// 1) создаёт Employee Profile, 2) присваивает Employee ID, 3) создаёт Employment,
/// 4) запускает чеклист адаптации (раздел 31-32, см. OnboardingChecklistService),
/// 5) генерирует стандартный пакет документов (раздел 29, см. DocumentGeneratorService),
/// 6) закрывает вакансию, 7) переводит application в Hired — одной транзакцией.
/// (access requests — раздел 52 — пока не реализованы, см. TODO ниже; когда
/// появятся, это тоже прямой вызов, а не шина событий — раздел 76: модульный
/// монолит.)
/// </summary>
public class HireCandidateService(
    TibbiNavDbContext db, EmployeeCodeGenerator codeGenerator,
    OnboardingChecklistService onboardingService, DocumentGeneratorService documentGeneratorService)
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
        var position = await db.Positions.AsNoTracking().FirstAsync(p => p.Id == vacancy.PositionId, ct);

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

        // Раздел 31-32: чеклист адаптации запускается автоматически, атомарно с
        // наймом — если для этой категории/должности/клиники ещё нет активного
        // шаблона, сам найм всё равно проходит (см. OnboardingChecklistService).
        await onboardingService.CreateChecklistAsync(
            vacancy.OrganizationId, clinicId, employee.Id, position.Category, position.Id, hireDate, ct);

        // Раздел 29: стандартный пакет документов (трудовой договор, приказ о
        // приёме, NDA, согласие на обработку ПДн) — атомарно с наймом. Тип
        // документа без настроенного шаблона молча пропускается, найм не блокируется.
        await documentGeneratorService.GenerateStandardHirePacketAsync(employee, employment, position, ct);

        application.Stage = PipelineStage.Hired;
        vacancy.Status = VacancyStatus.Closed;

        // TODO: Access Management module создаёт access requests (раздел 52).

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return employee;
    }
}
