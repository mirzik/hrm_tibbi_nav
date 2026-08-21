using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.BulkImport;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.BulkImport;

/// <summary>
/// Раздел 63 + 92 (Phase 1): импорт штатного расписания — создаёт/обновляет
/// Department и Position. Департамент, которого ещё нет в этой клинике,
/// создаётся автоматически (это и есть основной сценарий bulk-переноса
/// текущего штатного расписания компании из Excel в систему, а не точечное
/// редактирование уже настроенной оргструктуры).
/// </summary>
public sealed class StaffingScheduleImportDefinition : IBulkImportDefinition
{
    public ImportKind Kind => ImportKind.StaffingSchedule;
    public string TemplateSheetName => "Штатное расписание";

    public IReadOnlyList<ImportFieldDef> Fields { get; } =
    [
        new("ClinicCode", "Код клиники", true, "Должен совпадать с Clinic.Code, напр. DUS"),
        new("DepartmentName", "Подразделение", true, "Будет создано, если ещё не существует в этой клинике"),
        new("PositionTitle", "Должность", true),
        new("Category", "Категория персонала", true, ImportLabelMaps.Describe(ImportLabelMaps.Category)),
        new("ApprovedFte", "Утверждённая ставка (FTE)", true, "Число, напр. 1 или 0.5"),
        new("SalaryBudgetMonthly", "Бюджет ЗП/мес", true, "Число, в местной валюте"),
    ];

    public async Task<ImportRowResult> ValidateRowAsync(
        int rowNumber, IReadOnlyDictionary<string, string> rawRow, IReadOnlyDictionary<string, string?> mapping,
        TibbiNavDbContext db, Guid organizationId, CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var mapped = new Dictionary<string, object?>();

        var clinicCode = ImportParseHelpers.GetOrNull(rawRow, mapping, "ClinicCode");
        Clinic? clinic = null;
        if (clinicCode is null) errors.Add("Не указан код клиники.");
        else
        {
            clinic = await db.Clinics.AsNoTracking()
                .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Code == clinicCode, ct);
            if (clinic is null) errors.Add($"Клиника с кодом '{clinicCode}' не найдена.");
            else mapped["ClinicId"] = clinic.Id;
        }

        var departmentName = ImportParseHelpers.GetOrNull(rawRow, mapping, "DepartmentName");
        if (departmentName is null) errors.Add("Не указано подразделение.");
        else
        {
            mapped["DepartmentName"] = departmentName;
            if (clinic is not null)
            {
                var exists = await db.Departments.AsNoTracking()
                    .AnyAsync(d => d.ClinicId == clinic.Id && d.Name == departmentName, ct);
                if (!exists) warnings.Add($"Подразделение '{departmentName}' будет создано.");
            }
        }

        var positionTitle = ImportParseHelpers.GetOrNull(rawRow, mapping, "PositionTitle");
        if (positionTitle is null) errors.Add("Не указана должность.");
        else mapped["PositionTitle"] = positionTitle;

        var categoryRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "Category");
        if (categoryRaw is null) errors.Add("Не указана категория персонала.");
        else if (!ImportLabelMaps.Category.TryGetValue(categoryRaw, out var category))
            errors.Add($"Неизвестная категория персонала '{categoryRaw}'. Допустимые: {ImportLabelMaps.Describe(ImportLabelMaps.Category)}.");
        else mapped["Category"] = category;

        var fteRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "ApprovedFte");
        if (fteRaw is null) errors.Add("Не указана ставка (FTE).");
        else if (!ImportParseHelpers.TryParseDecimal(fteRaw, out var fte) || fte <= 0)
            errors.Add($"Некорректная ставка (FTE): '{fteRaw}'.");
        else mapped["ApprovedFte"] = fte;

        var budgetRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "SalaryBudgetMonthly");
        if (budgetRaw is null) errors.Add("Не указан бюджет ЗП/мес.");
        else if (!ImportParseHelpers.TryParseDecimal(budgetRaw, out var budget) || budget < 0)
            errors.Add($"Некорректный бюджет ЗП/мес: '{budgetRaw}'.");
        else mapped["SalaryBudgetMonthly"] = budget;

        return new ImportRowResult { RowNumber = rowNumber, IsValid = errors.Count == 0, Errors = errors, Warnings = warnings, MappedValues = mapped };
    }

    public async Task<ImportRowOutcome> ImportRowAsync(
        ImportRowResult row, TibbiNavDbContext db, Guid organizationId, IDictionary<string, Guid> importedInBatch, CancellationToken ct)
    {
        var clinicId = (Guid)row.MappedValues["ClinicId"]!;
        var departmentName = (string)row.MappedValues["DepartmentName"]!;
        var positionTitle = (string)row.MappedValues["PositionTitle"]!;
        var category = (PersonnelCategory)row.MappedValues["Category"]!;
        var fte = (decimal)row.MappedValues["ApprovedFte"]!;
        var budget = (decimal)row.MappedValues["SalaryBudgetMonthly"]!;

        var deptCacheKey = $"dept:{clinicId}:{departmentName.ToLowerInvariant()}";
        Guid departmentId;
        if (importedInBatch.TryGetValue(deptCacheKey, out var cachedDeptId))
        {
            departmentId = cachedDeptId;
        }
        else
        {
            var department = await db.Departments.FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.Name == departmentName, ct);
            if (department is null)
            {
                department = new Department { OrganizationId = organizationId, ClinicId = clinicId, Name = departmentName };
                db.Departments.Add(department);
                await db.SaveChangesAsync(ct); // нужен Id для Position ниже
            }
            departmentId = department.Id;
            importedInBatch[deptCacheKey] = departmentId;
        }

        var position = await db.Positions.FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.Title == positionTitle, ct);
        bool created;
        if (position is null)
        {
            position = new Position
            {
                OrganizationId = organizationId,
                ClinicId = clinicId,
                DepartmentId = departmentId,
                Title = positionTitle,
                Category = category,
                ApprovedFte = fte,
                SalaryBudgetMonthly = budget,
            };
            db.Positions.Add(position);
            created = true;
        }
        else
        {
            position.Category = category;
            position.ApprovedFte = fte;
            position.SalaryBudgetMonthly = budget;
            created = false;
        }

        await db.SaveChangesAsync(ct);
        return new ImportRowOutcome(row.RowNumber, created, !created, false, null, position.Id);
    }
}
