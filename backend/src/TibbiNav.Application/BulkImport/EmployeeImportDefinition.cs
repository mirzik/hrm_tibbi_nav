using Microsoft.EntityFrameworkCore;
using TibbiNav.Application.Employees;
using TibbiNav.Domain.BulkImport;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.BulkImport;

/// <summary>
/// Раздел 63 + 92 (Phase 1): импорт текущей базы сотрудников — создаёт
/// Employee + первичный EmploymentRecord (Effective Dating, раздел 10;
/// ChangeReason = "Bulk Import"). Department/Position должны уже
/// существовать (обычно — из предварительно прогнанного импорта штатного
/// расписания, см. StaffingScheduleImportDefinition), самих сотрудников
/// импорт не выдумывает организационную структуру.
///
/// Руководитель указывается по ФИО (не по EmployeeCode — на момент
/// первичной загрузки у сотрудников ещё нет кода в системе, только в файле
/// компании). Резолвится сначала среди уже существующих в БД сотрудников
/// той же клиники, затем среди уже импортированных строк этого же файла —
/// поэтому если менеджер идёт в файле позже своих подчинённых, связь не
/// восстановится (ограничение MVP, репортится как warning).
/// </summary>
public sealed class EmployeeImportDefinition(EmployeeCodeGenerator codeGenerator) : IBulkImportDefinition
{
    public ImportKind Kind => ImportKind.Employees;
    public string TemplateSheetName => "Сотрудники";

    public IReadOnlyList<ImportFieldDef> Fields { get; } =
    [
        new("ClinicCode", "Код клиники", true),
        new("DepartmentName", "Подразделение", true, "Должно уже существовать в этой клинике"),
        new("PositionTitle", "Должность", true, "Должна уже существовать в этом подразделении"),
        new("FullName", "ФИО", true),
        new("Gender", "Пол", true, ImportLabelMaps.Describe(ImportLabelMaps.Gender)),
        new("DateOfBirth", "Дата рождения", true, "дд.мм.гггг"),
        new("Citizenship", "Гражданство", true, "напр. TJ"),
        new("Phone", "Телефон", false),
        new("PersonalEmail", "Личная почта", false),
        new("CorporateEmail", "Корпоративная почта", false),
        new("HireDate", "Дата приёма", true, "дд.мм.гггг"),
        new("EmploymentType", "Тип занятости", true, ImportLabelMaps.Describe(ImportLabelMaps.EmploymentType)),
        new("Fte", "Ставка (FTE)", true),
        new("BaseSalary", "Оклад", false),
        new("ManagerFullName", "ФИО руководителя", false, "Должен уже существовать в системе или встречаться раньше в этом файле"),
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
            clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Code == clinicCode, ct);
            if (clinic is null) errors.Add($"Клиника с кодом '{clinicCode}' не найдена.");
            else mapped["ClinicId"] = clinic.Id;
        }

        Department? department = null;
        var departmentName = ImportParseHelpers.GetOrNull(rawRow, mapping, "DepartmentName");
        if (departmentName is null) errors.Add("Не указано подразделение.");
        else if (clinic is not null)
        {
            department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.ClinicId == clinic.Id && d.Name == departmentName, ct);
            if (department is null) errors.Add($"Подразделение '{departmentName}' не найдено в клинике '{clinicCode}'. Сначала импортируйте штатное расписание.");
            else mapped["DepartmentId"] = department.Id;
        }

        var positionTitle = ImportParseHelpers.GetOrNull(rawRow, mapping, "PositionTitle");
        if (positionTitle is null) errors.Add("Не указана должность.");
        else if (department is not null)
        {
            var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.DepartmentId == department.Id && p.Title == positionTitle, ct);
            if (position is null) errors.Add($"Должность '{positionTitle}' не найдена в подразделении '{departmentName}'. Сначала импортируйте штатное расписание.");
            else mapped["PositionId"] = position.Id;
        }

        var fullName = ImportParseHelpers.GetOrNull(rawRow, mapping, "FullName");
        if (fullName is null) errors.Add("Не указано ФИО.");
        else mapped["FullName"] = fullName;

        var genderRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "Gender");
        if (genderRaw is null) errors.Add("Не указан пол.");
        else if (!ImportLabelMaps.Gender.TryGetValue(genderRaw, out var gender))
            errors.Add($"Некорректное значение пола '{genderRaw}'. Допустимые: {ImportLabelMaps.Describe(ImportLabelMaps.Gender)}.");
        else mapped["Gender"] = gender;

        var dobRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "DateOfBirth");
        if (dobRaw is null) errors.Add("Не указана дата рождения.");
        else if (!ImportParseHelpers.TryParseDateOnly(dobRaw, out var dob) || dob >= DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add($"Некорректная дата рождения '{dobRaw}'.");
        else mapped["DateOfBirth"] = dob;

        var citizenship = ImportParseHelpers.GetOrNull(rawRow, mapping, "Citizenship");
        if (citizenship is null) errors.Add("Не указано гражданство.");
        else mapped["Citizenship"] = citizenship;

        mapped["Phone"] = ImportParseHelpers.GetOrNull(rawRow, mapping, "Phone");
        mapped["PersonalEmail"] = ImportParseHelpers.GetOrNull(rawRow, mapping, "PersonalEmail");
        mapped["CorporateEmail"] = ImportParseHelpers.GetOrNull(rawRow, mapping, "CorporateEmail");

        var hireDateRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "HireDate");
        if (hireDateRaw is null) errors.Add("Не указана дата приёма.");
        else if (!ImportParseHelpers.TryParseDateOnly(hireDateRaw, out var hireDate))
            errors.Add($"Некорректная дата приёма '{hireDateRaw}'.");
        else mapped["HireDate"] = hireDate;

        var employmentTypeRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "EmploymentType");
        if (employmentTypeRaw is null) errors.Add("Не указан тип занятости.");
        else if (!ImportLabelMaps.EmploymentType.TryGetValue(employmentTypeRaw, out var employmentType))
            errors.Add($"Неизвестный тип занятости '{employmentTypeRaw}'. Допустимые: {ImportLabelMaps.Describe(ImportLabelMaps.EmploymentType)}.");
        else mapped["EmploymentType"] = employmentType;

        var fteRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "Fte");
        if (fteRaw is null) errors.Add("Не указана ставка (FTE).");
        else if (!ImportParseHelpers.TryParseDecimal(fteRaw, out var fte) || fte <= 0)
            errors.Add($"Некорректная ставка (FTE) '{fteRaw}'.");
        else mapped["Fte"] = fte;

        var salaryRaw = ImportParseHelpers.GetOrNull(rawRow, mapping, "BaseSalary");
        if (salaryRaw is not null)
        {
            if (!ImportParseHelpers.TryParseDecimal(salaryRaw, out var salary) || salary < 0)
                errors.Add($"Некорректный оклад '{salaryRaw}'.");
            else mapped["BaseSalary"] = salary;
        }

        var managerFullName = ImportParseHelpers.GetOrNull(rawRow, mapping, "ManagerFullName");
        if (managerFullName is not null)
        {
            mapped["ManagerFullName"] = managerFullName;
            if (clinic is not null)
            {
                var managerCandidates = await db.Employees.AsNoTracking()
                    .Where(e => e.ClinicId == clinic.Id && e.FullName == managerFullName)
                    .Select(e => e.Id)
                    .ToListAsync(ct);
                if (managerCandidates.Count == 1) mapped["ManagerEmployeeId"] = managerCandidates[0];
                else if (managerCandidates.Count > 1) warnings.Add($"Руководитель '{managerFullName}' встречается несколько раз — связь не установлена, назначьте вручную.");
                else warnings.Add($"Руководитель '{managerFullName}' не найден в системе (возможно, будет импортирован позже в этом же файле) — связь не установлена автоматически.");
            }
        }

        if (fullName is not null && mapped.TryGetValue("DateOfBirth", out var dobVal) && clinic is not null)
        {
            var duplicate = await db.Employees.AsNoTracking()
                .AnyAsync(e => e.ClinicId == clinic.Id && e.FullName == fullName && e.DateOfBirth == (DateOnly)dobVal!, ct);
            if (duplicate) warnings.Add("Похоже, сотрудник с таким ФИО и датой рождения уже есть в системе — проверьте на дубликат перед импортом.");
        }

        return new ImportRowResult { RowNumber = rowNumber, IsValid = errors.Count == 0, Errors = errors, Warnings = warnings, MappedValues = mapped };
    }

    public async Task<ImportRowOutcome> ImportRowAsync(
        ImportRowResult row, TibbiNavDbContext db, Guid organizationId, IDictionary<string, Guid> importedInBatch, CancellationToken ct)
    {
        var clinicId = (Guid)row.MappedValues["ClinicId"]!;
        var departmentId = (Guid)row.MappedValues["DepartmentId"]!;
        var positionId = (Guid)row.MappedValues["PositionId"]!;
        var fullName = (string)row.MappedValues["FullName"]!;

        Guid? managerId = row.MappedValues.TryGetValue("ManagerEmployeeId", out var mgr) ? (Guid)mgr! : null;
        if (managerId is null && row.MappedValues.TryGetValue("ManagerFullName", out var mgrNameObj))
        {
            var cacheKey = $"emp-name:{clinicId}:{((string)mgrNameObj!).ToLowerInvariant()}";
            if (importedInBatch.TryGetValue(cacheKey, out var cachedManagerId))
                managerId = cachedManagerId;
        }

        var employee = new Employee
        {
            OrganizationId = organizationId,
            ClinicId = clinicId,
            EmployeeCode = await codeGenerator.GenerateAsync(clinicId, ct),
            FullName = fullName,
            Gender = (Gender)row.MappedValues["Gender"]!,
            DateOfBirth = (DateOnly)row.MappedValues["DateOfBirth"]!,
            Citizenship = (string)row.MappedValues["Citizenship"]!,
            Phone = (string?)row.MappedValues.GetValueOrDefault("Phone"),
            PersonalEmail = (string?)row.MappedValues.GetValueOrDefault("PersonalEmail"),
            CorporateEmail = (string?)row.MappedValues.GetValueOrDefault("CorporateEmail"),
            Status = EmployeeStatus.Active,
        };
        db.Employees.Add(employee);

        var hireDate = (DateOnly)row.MappedValues["HireDate"]!;
        var employment = new EmploymentRecord
        {
            OrganizationId = organizationId,
            ClinicId = clinicId,
            Employee = employee,
            DepartmentId = departmentId,
            PositionId = positionId,
            ManagerEmployeeId = managerId,
            Fte = (decimal)row.MappedValues["Fte"]!,
            EmploymentType = (Domain.Employees.EmploymentType)row.MappedValues["EmploymentType"]!,
            HireDate = hireDate,
            EffectiveFrom = hireDate,
            IsCurrent = true,
            BaseSalary = row.MappedValues.TryGetValue("BaseSalary", out var salary) ? (decimal)salary! : null,
            ChangeReason = "Bulk Import",
        };
        db.EmploymentRecords.Add(employment);

        await db.SaveChangesAsync(ct);

        importedInBatch[$"emp-name:{clinicId}:{fullName.ToLowerInvariant()}"] = employee.Id;

        var note = managerId is null && row.MappedValues.ContainsKey("ManagerFullName")
            ? "Руководитель не был найден — связь не установлена."
            : null;

        return new ImportRowOutcome(row.RowNumber, true, false, false, note, employee.Id);
    }
}
