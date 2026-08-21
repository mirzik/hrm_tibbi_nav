using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Organization;

namespace TibbiNav.Application.BulkImport;

/// <summary>Русские подписи для enum-ов, которые заполняются людьми в Excel
/// (раздел 63) — используются и при валидации импорта, и при генерации
/// справочного листа в шаблоне (BulkImportTemplateGenerator).</summary>
public static class ImportLabelMaps
{
    public static readonly IReadOnlyDictionary<string, PersonnelCategory> Category =
        new Dictionary<string, PersonnelCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["Корпоративный АУП"] = PersonnelCategory.CorporateAup,
            ["Медицинский АУП"] = PersonnelCategory.MedicalAup,
            ["Врач"] = PersonnelCategory.Doctor,
            ["Средний медперсонал"] = PersonnelCategory.NursingStaff,
            ["Младший медперсонал"] = PersonnelCategory.JuniorMedicalStaff,
            ["Ресепшн"] = PersonnelCategory.Reception,
            ["Технический персонал"] = PersonnelCategory.TechnicalStaff,
            ["Совместитель (внутренний)"] = PersonnelCategory.PartTime,
            ["Совместитель (внешний)"] = PersonnelCategory.ExternalPartTime,
            ["Временный персонал"] = PersonnelCategory.Temporary,
            ["Стажёр"] = PersonnelCategory.Intern,
            ["Иностранный специалист"] = PersonnelCategory.ForeignSpecialist,
            ["Врач из России"] = PersonnelCategory.RussianDoctor,
        };

    public static readonly IReadOnlyDictionary<string, EmploymentType> EmploymentType =
        new Dictionary<string, EmploymentType>(StringComparer.OrdinalIgnoreCase)
        {
            ["Полная занятость"] = Domain.Employees.EmploymentType.FullTime,
            ["Частичная занятость"] = Domain.Employees.EmploymentType.PartTime,
            ["Внутреннее совмещение"] = Domain.Employees.EmploymentType.InternalCombination,
            ["Внешнее совмещение"] = Domain.Employees.EmploymentType.ExternalCombination,
            ["Временная"] = Domain.Employees.EmploymentType.Temporary,
            ["Стажировка"] = Domain.Employees.EmploymentType.Internship,
        };

    public static readonly IReadOnlyDictionary<string, Gender> Gender =
        new Dictionary<string, Gender>(StringComparer.OrdinalIgnoreCase)
        {
            ["М"] = Domain.Employees.Gender.Male,
            ["Мужской"] = Domain.Employees.Gender.Male,
            ["Ж"] = Domain.Employees.Gender.Female,
            ["Женский"] = Domain.Employees.Gender.Female,
        };

    public static string Describe<TEnum>(IReadOnlyDictionary<string, TEnum> map) where TEnum : struct
        => string.Join(", ", map.Keys);
}
