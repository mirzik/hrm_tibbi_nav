using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Workflow;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Workflow;

/// <summary>
/// Раздел 68: выбирает активный WorkflowDefinition для EntityType, у которого
/// ВСЕ Conditions выполняются (пусто = подходит всем), а из подходящих — с
/// наибольшим Priority. Аналог OnboardingChecklistTemplateSelector, но вместо
/// специфичности по вложенным null-полям — явные Condition-строки (раздел 68
/// прямо требует Conditions как часть настройки маршрута, а не просто scope).
/// </summary>
public sealed class WorkflowDefinitionSelector(TibbiNavDbContext db)
{
    public async Task<WorkflowDefinition?> SelectAsync(string entityType, WorkflowEntityContext context, CancellationToken ct)
    {
        var candidates = await db.WorkflowDefinitions
            .Include(d => d.Conditions)
            .Include(d => d.Steps)
            .Where(d => d.IsActive && d.EntityType == entityType)
            .ToListAsync(ct);

        return candidates
            .Where(d => d.Conditions.All(c => Matches(c, context.Fields)))
            .OrderByDescending(d => d.Priority)
            .FirstOrDefault();
    }

    private static bool Matches(WorkflowCondition condition, IReadOnlyDictionary<string, string?> fields)
    {
        if (!fields.TryGetValue(condition.FieldName, out var rawValue) || rawValue is null) return false;

        return condition.Operator switch
        {
            WorkflowConditionOperator.Equals => string.Equals(rawValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            WorkflowConditionOperator.NotEquals => !string.Equals(rawValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            WorkflowConditionOperator.Contains => rawValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            WorkflowConditionOperator.GreaterThan => Compare(rawValue, condition.Value) > 0,
            WorkflowConditionOperator.LessThan => Compare(rawValue, condition.Value) < 0,
            WorkflowConditionOperator.GreaterThanOrEqual => Compare(rawValue, condition.Value) >= 0,
            WorkflowConditionOperator.LessThanOrEqual => Compare(rawValue, condition.Value) <= 0,
            _ => false,
        };
    }

    /// <summary>Числовое сравнение, если оба значения — числа (напр. BudgetSalary),
    /// иначе — сравнение строк (для >/&lt; по enum-именам вроде Priority
    /// это не совсем "по значению enum", а лексикографически — осознанное
    /// упрощение; для числовых сравнений в шаблонах используйте числовые поля).</summary>
    private static int Compare(string a, string b) =>
        decimal.TryParse(a, NumberStyles.Number, CultureInfo.InvariantCulture, out var da) &&
        decimal.TryParse(b, NumberStyles.Number, CultureInfo.InvariantCulture, out var db2)
            ? da.CompareTo(db2)
            : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
}
