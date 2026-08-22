using TibbiNav.Domain.Kpi;

namespace TibbiNav.Application.Kpi;

/// <summary>Раздел 46 ТЗ: месяц/квартал/полугодие/год — вычисляет конец периода
/// от даты начала, чтобы клиент не мог завести рассинхронизированный период
/// (например, "квартал" длиной в 2 месяца).</summary>
public static class KpiPeriodCalculator
{
    public static DateOnly GetPeriodEnd(DateOnly periodStart, KpiPeriodType periodType)
    {
        var monthsToAdd = periodType switch
        {
            KpiPeriodType.Month => 1,
            KpiPeriodType.Quarter => 3,
            KpiPeriodType.HalfYear => 6,
            KpiPeriodType.Year => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(periodType)),
        };

        return periodStart.AddMonths(monthsToAdd).AddDays(-1);
    }
}
