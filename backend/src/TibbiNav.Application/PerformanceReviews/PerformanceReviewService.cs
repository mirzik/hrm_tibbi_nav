using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.PerformanceReviews;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.PerformanceReviews;

/// <summary>
/// Раздел 47 ТЗ: Performance Review (Self/Manager/180°/360°) и расчёт итоговой
/// оценки. Сама подача оценки (Submit) намеренно НЕ ограничена ролью
/// HR/руководителя в этом сервисе — оценщиком может быть любой сотрудник
/// (self/manager/peer/subordinate), подающий её через self-service
/// (MeController.SubmitReview), т.к. "оценить коллегу" — это действие самого
/// сотрудника о самом себе как об оценщике, а не HR-функция. HR/руководитель
/// управляют циклом (создание, добавление участников/оценщиков, финальный
/// расчёт) через PerformanceReviewCyclesController.
/// </summary>
public sealed class PerformanceReviewService(TibbiNavDbContext db)
{
    /// <summary>Добавляет сотрудника в цикл — автоматически создаёт
    /// ReviewAssignment(Self) и/или ReviewAssignment(Manager) согласно
    /// ReviewCycle.Type. Peer/Subordinate для Review360 добавляются отдельно
    /// через AddReviewerAsync — кого именно назначить оценщиком 360° не
    /// резолвится автоматически (это выбор HR/руководителя, не бизнес-правило
    /// вроде "тот же отдел = peer").</summary>
    public async Task<ReviewParticipant> AddParticipantAsync(Guid reviewCycleId, Guid employeeId, CancellationToken ct)
    {
        var cycle = await db.ReviewCycles.FirstOrDefaultAsync(c => c.Id == reviewCycleId, ct)
            ?? throw new KeyNotFoundException("Цикл оценки не найден.");
        if (cycle.Status == ReviewCycleStatus.Closed)
            throw new InvalidOperationException("Цикл оценки закрыт — добавление участников недоступно.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Сотрудник не найден.");

        var alreadyParticipant = await db.ReviewParticipants.AnyAsync(p => p.ReviewCycleId == reviewCycleId && p.EmployeeId == employeeId, ct);
        if (alreadyParticipant)
            throw new InvalidOperationException("Этот сотрудник уже участвует в данном цикле оценки.");

        var participant = new ReviewParticipant
        {
            OrganizationId = employee.OrganizationId,
            ClinicId = employee.ClinicId,
            ReviewCycleId = reviewCycleId,
            EmployeeId = employeeId,
        };

        var includesSelf = cycle.Type is ReviewCycleType.Self or ReviewCycleType.Review180 or ReviewCycleType.Review360;
        var includesManager = cycle.Type is ReviewCycleType.Manager or ReviewCycleType.Review180 or ReviewCycleType.Review360;

        if (includesSelf)
        {
            participant.Assignments.Add(new ReviewAssignment
            {
                OrganizationId = employee.OrganizationId,
                ClinicId = employee.ClinicId,
                ReviewerRole = ReviewerRole.Self,
                ReviewerId = employeeId,
            });
        }

        if (includesManager)
        {
            var managerId = await db.EmploymentRecords.AsNoTracking()
                .Where(r => r.EmployeeId == employeeId && r.IsCurrent)
                .Select(r => r.ManagerEmployeeId)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("У сотрудника не назначен руководитель — Manager Review невозможен.");

            participant.Assignments.Add(new ReviewAssignment
            {
                OrganizationId = employee.OrganizationId,
                ClinicId = employee.ClinicId,
                ReviewerRole = ReviewerRole.Manager,
                ReviewerId = managerId,
            });
        }

        db.ReviewParticipants.Add(participant);
        await db.SaveChangesAsync(ct);
        return participant;
    }

    /// <summary>Явное добавление доп. оценщика (обычно Peer/Subordinate для
    /// Review360) — только пока цикл не закрыт.</summary>
    public async Task<ReviewAssignment> AddReviewerAsync(Guid participantId, ReviewerRole role, Guid reviewerId, CancellationToken ct)
    {
        var participant = await db.ReviewParticipants.Include(p => p.ReviewCycle).FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new KeyNotFoundException("Участник цикла оценки не найден.");
        if (participant.ReviewCycle.Status == ReviewCycleStatus.Closed)
            throw new InvalidOperationException("Цикл оценки закрыт — добавление оценщиков недоступно.");

        var reviewerExists = await db.Employees.AsNoTracking().AnyAsync(e => e.Id == reviewerId, ct);
        if (!reviewerExists) throw new InvalidOperationException("Оценщик (сотрудник) не найден.");

        var assignment = new ReviewAssignment
        {
            OrganizationId = participant.OrganizationId,
            ClinicId = participant.ClinicId,
            ReviewParticipantId = participantId,
            ReviewerRole = role,
            ReviewerId = reviewerId,
        };
        db.ReviewAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment;
    }

    /// <summary>Подача оценки самим оценщиком (self/manager/peer/subordinate —
    /// все через self-service, см. MeController). Перезаписываемо (напр. если
    /// оценщик уточнил формулировку) — как и KpiAssignmentService.SetActualAsync.</summary>
    public async Task<ReviewAssignment> SubmitReviewAsync(
        Guid assignmentId, Guid submittingEmployeeId, decimal score,
        string? strengths, string? areasForImprovement, string? comments,
        IReadOnlyCollection<Guid>? kpiAssignmentIds, CancellationToken ct)
    {
        var assignment = await db.ReviewAssignments
            .Include(a => a.ReviewParticipant).ThenInclude(p => p.ReviewCycle)
            .Include(a => a.KpiReferences)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new KeyNotFoundException("Назначение на оценку не найдено.");

        if (assignment.ReviewerId != submittingEmployeeId)
            throw new InvalidOperationException("Эта оценка назначена не вам.");
        if (assignment.ReviewParticipant.ReviewCycle.Status == ReviewCycleStatus.Closed)
            throw new InvalidOperationException("Цикл оценки закрыт — подача оценок недоступна.");

        assignment.Score = score;
        assignment.Strengths = strengths;
        assignment.AreasForImprovement = areasForImprovement;
        assignment.Comments = comments;
        assignment.Status = ReviewAssignmentStatus.Submitted;
        assignment.SubmittedAtUtc = DateTime.UtcNow;

        if (kpiAssignmentIds is { Count: > 0 })
        {
            // Защита: можно ссылаться только на KPI самого оцениваемого сотрудника
            // (не оценщика и не произвольный чужой KpiAssignment).
            var subjectEmployeeId = assignment.ReviewParticipant.EmployeeId;
            var validKpiIds = await db.KpiAssignments.AsNoTracking()
                .Where(k => k.EmployeeId == subjectEmployeeId && kpiAssignmentIds.Contains(k.Id))
                .Select(k => k.Id)
                .ToListAsync(ct);

            var invalid = kpiAssignmentIds.Except(validKpiIds).ToList();
            if (invalid.Count > 0)
                throw new InvalidOperationException($"KPI-назначения не принадлежат оцениваемому сотруднику: {string.Join(", ", invalid)}.");

            db.ReviewKpiReferences.RemoveRange(assignment.KpiReferences);
            foreach (var kpiId in validKpiIds)
                db.ReviewKpiReferences.Add(new ReviewKpiReference { ReviewAssignmentId = assignmentId, KpiAssignmentId = kpiId });
        }

        await db.SaveChangesAsync(ct);
        return assignment;
    }

    /// <summary>"Расчёт итоговой оценки" — требует, чтобы ВСЕ назначенные
    /// оценки участника были поданы (иначе цикл считается неполным). Сначала
    /// усредняем Score внутри каждой ReviewerRole, затем — между ролями,
    /// присутствующими у участника: так 5 Peer-оценок при Review360 не
    /// перевешивают единственную Manager-оценку.</summary>
    public async Task<ReviewParticipant> CalculateOverallScoreAsync(Guid participantId, CancellationToken ct)
    {
        var participant = await db.ReviewParticipants.Include(p => p.Assignments).FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new KeyNotFoundException("Участник цикла оценки не найден.");

        if (participant.Assignments.Count == 0)
            throw new InvalidOperationException("У участника нет ни одной назначенной оценки.");

        var pending = participant.Assignments.Where(a => a.Status != ReviewAssignmentStatus.Submitted).ToList();
        if (pending.Count > 0)
        {
            var roles = string.Join(", ", pending.Select(a => a.ReviewerRole).Distinct());
            throw new InvalidOperationException($"Не все оценки поданы — ожидаются: {roles}.");
        }

        var perRoleAverage = participant.Assignments
            .GroupBy(a => a.ReviewerRole)
            .Select(g => g.Average(a => a.Score!.Value));

        participant.OverallScore = Math.Round(perRoleAverage.Average(), 2);
        participant.OverallScoreCalculatedAtUtc = DateTime.UtcNow;
        participant.Status = ParticipantStatus.Completed;

        await db.SaveChangesAsync(ct);
        return participant;
    }
}
