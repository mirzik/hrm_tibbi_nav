using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Documents;

/// <summary>
/// Раздел 29: собирает плейсхолдеры для генерации документа из Employee +
/// EmploymentRecord (+ Position/Department/Clinic/Organization) и подставляет
/// их в текст шаблона (`{{Token}}`). Список поддерживаемых токенов — ниже,
/// в ResolveAsync; ровно эти же имена используются в DocumentTemplateBlock.Text
/// при заполнении шаблонов (см. DevSeedData).
/// </summary>
public sealed class DocumentPlaceholderResolver(TibbiNavDbContext db)
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        Employee employee, EmploymentRecord employment, Position position, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().FirstAsync(d => d.Id == employment.DepartmentId, ct);
        var clinic = employment.ClinicId is { } clinicId
            ? await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, ct)
            : null;
        var organization = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == employment.OrganizationId, ct);

        return new Dictionary<string, string>
        {
            ["EmployeeCode"] = employee.EmployeeCode,
            ["FullName"] = employee.FullName,
            ["DateOfBirth"] = employee.DateOfBirth.ToString("dd.MM.yyyy"),
            ["Citizenship"] = employee.Citizenship,
            ["Phone"] = employee.Phone ?? "—",
            ["Address"] = employee.Address ?? "—",
            ["PositionTitle"] = position.Title,
            ["DepartmentName"] = department.Name,
            ["ClinicName"] = clinic?.Name ?? "—",
            ["ClinicAddress"] = clinic?.Address ?? "—",
            ["OrganizationName"] = organization.Name,
            ["OrganizationLegalName"] = organization.LegalName,
            ["HireDate"] = employment.HireDate.ToString("dd.MM.yyyy"),
            ["ProbationEndDate"] = employment.ProbationEndDate?.ToString("dd.MM.yyyy") ?? "не устанавливается",
            ["BaseSalary"] = employment.BaseSalary?.ToString("N2", Culture) ?? "не указан",
            ["Fte"] = employment.Fte.ToString("0.##", Culture),
            ["EmploymentType"] = DescribeEmploymentType(employment.EmploymentType),
            ["TodayDate"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("dd.MM.yyyy"),
        };
    }

    public static string Substitute(string text, IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            text = text.Replace("{{" + key + "}}", value);
        return text;
    }

    private static string DescribeEmploymentType(EmploymentType type) => type switch
    {
        EmploymentType.FullTime => "полная занятость",
        EmploymentType.PartTime => "частичная занятость",
        EmploymentType.InternalCombination => "внутреннее совмещение",
        EmploymentType.ExternalCombination => "внешнее совмещение",
        EmploymentType.Temporary => "временная занятость",
        EmploymentType.Internship => "стажировка",
        _ => type.ToString(),
    };
}
