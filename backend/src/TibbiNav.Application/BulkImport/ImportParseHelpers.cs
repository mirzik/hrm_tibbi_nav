using System.Globalization;

namespace TibbiNav.Application.BulkImport;

/// <summary>Общие парсеры значений ячеек для всех IBulkImportDefinition —
/// принимают и invariant-формат (как отдаёт XlsxParser для настоящих
/// date/number ячеек), и типичный ручной ввод (запятая как разделитель,
/// дата dd.MM.yyyy) на случай, если столбец в файле текстовый.</summary>
public static class ImportParseHelpers
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "yyyy/MM/dd", "dd/MM/yyyy"];

    public static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var normalized = raw.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseDateOnly(string? raw, out DateOnly value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var text = raw.Trim();

        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return true;

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    public static string? GetOrNull(IReadOnlyDictionary<string, string> rawRow, IReadOnlyDictionary<string, string?> mapping, string fieldKey)
    {
        if (!mapping.TryGetValue(fieldKey, out var sourceColumn) || sourceColumn is null) return null;
        return rawRow.TryGetValue(sourceColumn, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    }
}
