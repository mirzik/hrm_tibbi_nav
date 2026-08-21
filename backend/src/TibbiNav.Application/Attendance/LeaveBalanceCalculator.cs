using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Core;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Attendance;

public sealed record LeaveBalance(int Year, string LeaveType, int EntitlementDays, int UsedDays, int PendingDays, int RemainingDays);

/// <summary>
/// Раздел 53 (Self Service: "баланс... отпусков"). Домен-комментарий на
/// LeaveRequest уже описывает целевую модель: "LeaveBalance считается в
/// Application-слое на основе LeaveType.AccrualRule + LeaveRequest history,
/// не хранится как счётчик" — полноценного accrual-движка (raздел 38, вне
/// текущего среза) ещё нет, поэтому AnnualEntitlementDays — фиксированная
/// MVP-константа (минимум по ТК РТ), а не хардкод бизнес-правила навсегда:
/// когда появится accrual-движок, этот калькулятор — единственное место,
/// которое нужно будет переключить на реальные начисления.
/// </summary>
public sealed class LeaveBalanceCalculator(TibbiNavDbContext db)
{
    public const int AnnualEntitlementDays = 24;
    private const string AnnualLeaveType = "Annual";

    public async Task<LeaveBalance> CalculateAsync(Guid employeeId, int year, CancellationToken ct)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.LeaveType == AnnualLeaveType)
            .Where(l => l.StartDate <= yearEnd && l.EndDate >= yearStart)
            .Where(l => l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Requested)
            .Select(l => new { l.Status, l.Days })
            .ToListAsync(ct);

        var used = requests.Where(r => r.Status == LeaveStatus.Approved).Sum(r => r.Days);
        var pending = requests.Where(r => r.Status == LeaveStatus.Requested).Sum(r => r.Days);

        return new LeaveBalance(year, AnnualLeaveType, AnnualEntitlementDays, used, pending, AnnualEntitlementDays - used);
    }
}
