using Microsoft.EntityFrameworkCore;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Employees;

/// <summary>
/// Генерирует Employee ID вида TN-DUS-000152 (раздел 7 ТЗ):
/// TN = префикс компании, DUS = Clinic.Code, 000152 = порядковый номер по клинике.
/// </summary>
public class EmployeeCodeGenerator(TibbiNavDbContext db)
{
    public async Task<string> GenerateAsync(Guid clinicId, CancellationToken ct = default)
    {
        var clinic = await db.Clinics.FirstAsync(c => c.Id == clinicId, ct);

        var prefix = $"TN-{clinic.Code}-";
        var lastCode = await db.Employees
            .Where(e => e.ClinicId == clinicId && e.EmployeeCode.StartsWith(prefix))
            .OrderByDescending(e => e.EmployeeCode)
            .Select(e => e.EmployeeCode)
            .FirstOrDefaultAsync(ct);

        var nextSeq = 1;
        if (lastCode is not null && int.TryParse(lastCode.Split('-').Last(), out var lastSeq))
            nextSeq = lastSeq + 1;

        return $"{prefix}{nextSeq:D6}";
    }
}
