using System.Reflection;

namespace TibbiNav.Application.Authorization;

/// <summary>
/// Раздел 65-66: применяет RestrictedFields из RolePermission — исключает
/// чувствительные поля из ответа API, даже если само действие (напр. View)
/// разрешено. Пример из домена: роль DepartmentManager может иметь
/// RestrictedFields="Salary,BankAccount,NationalId" — сопоставление идёт по
/// подстроке имени свойства (без учёта регистра), поэтому одна запись в
/// RolePermission перекрывает и Employee.BankAccountEncrypted,
/// и EmploymentRecord.BaseSalary, не привязываясь к точным именам C#-свойств.
/// </summary>
public static class RestrictedFieldsExtensions
{
    /// <summary>Сериализует сущность в словарь скалярных полей, вырезая
    /// ограниченные и пропуская навигационные свойства (single-ссылки и
    /// коллекции) — их нужно обрабатывать явно на вызывающей стороне, чтобы не
    /// утекли данные через случайный Include() и не словить цикл сериализации.</summary>
    public static IDictionary<string, object?> ToRestrictedDictionary(this object entity, IReadOnlySet<string> restrictedFields)
    {
        var result = new Dictionary<string, object?>();

        foreach (var prop in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            if (!IsScalar(prop.PropertyType)) continue;
            if (IsRestricted(prop.Name, restrictedFields)) continue;

            result[prop.Name] = prop.GetValue(entity);
        }

        return result;
    }

    private static bool IsRestricted(string propertyName, IReadOnlySet<string> restrictedFields)
        => restrictedFields.Any(f => propertyName.Contains(f, StringComparison.OrdinalIgnoreCase));

    private static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive
            || t.IsEnum
            || t == typeof(string)
            || t == typeof(Guid)
            || t == typeof(decimal)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(DateOnly)
            || t == typeof(TimeOnly);
    }
}
