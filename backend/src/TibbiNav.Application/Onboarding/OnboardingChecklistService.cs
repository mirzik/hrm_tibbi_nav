using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Onboarding;

/// <summary>
/// Раздел 25, 31-32 ТЗ: создаёт и ведёт чеклист адаптации сотрудника.
/// CreateChecklistAsync вызывается автоматически из HireCandidateService сразу
/// после найма (см. TibbiNav.Application.Recruitment.HireCandidateService) —
/// сознательно не через отдельный шину событий (это модульный монолит,
/// раздел 76), а прямым вызовом внутри той же транзакции: адаптация должна
/// начаться атомарно вместе с наймом, а не "рано или поздно" через очередь.
/// </summary>
public sealed class OnboardingChecklistService(TibbiNavDbContext db, OnboardingChecklistTemplateSelector templateSelector)
{
    /// <summary>Не вызывает SaveChanges — рассчитан на вызов внутри уже открытой
    /// транзакции (HireCandidateService). Отсутствие подходящего активного
    /// шаблона — не ошибка (возвращает null): найм не должен блокироваться
    /// из-за того, что HR ещё не настроил чеклисты для этой категории/клиники.</summary>
    public async Task<OnboardingChecklist?> CreateChecklistAsync(
        Guid organizationId, Guid? clinicId, Guid employeeId,
        PersonnelCategory category, Guid positionId, DateOnly hireDate, CancellationToken ct)
    {
        var template = await templateSelector.SelectAsync(category, positionId, clinicId, ct);
        if (template is null) return null;

        var checklist = new OnboardingChecklist
        {
            OrganizationId = organizationId,
            ClinicId = clinicId,
            EmployeeId = employeeId,
            TemplateId = template.Id,
            HireDate = hireDate,
            Status = OnboardingChecklistStatus.InProgress,
        };

        foreach (var taskDef in template.Tasks.OrderBy(t => t.OrderIndex))
        {
            var offset = taskDef.DueOffsetDays ?? OnboardingStageDefaults.DefaultOffsetDays[taskDef.Stage];
            checklist.Tasks.Add(new OnboardingChecklistTask
            {
                Checklist = checklist,
                Stage = taskDef.Stage,
                Title = taskDef.Title,
                Description = taskDef.Description,
                Responsible = taskDef.Responsible,
                DueDate = hireDate.AddDays(offset),
                OrderIndex = taskDef.OrderIndex,
                Status = OnboardingTaskStatus.Pending,
            });
        }

        db.OnboardingChecklists.Add(checklist);
        return checklist;
    }

    /// <summary>То же самое, но с явным SaveChanges — для ручного запуска
    /// (backfill для сотрудников, нанятых до появления этого модуля, либо
    /// повторный запуск после того, как HR добавил недостающий шаблон).</summary>
    public async Task<OnboardingChecklist?> CreateAndSaveChecklistAsync(
        Guid organizationId, Guid? clinicId, Guid employeeId,
        PersonnelCategory category, Guid positionId, DateOnly hireDate, CancellationToken ct)
    {
        var checklist = await CreateChecklistAsync(organizationId, clinicId, employeeId, category, positionId, hireDate, ct);
        if (checklist is not null) await db.SaveChangesAsync(ct);
        return checklist;
    }

    /// <summary>Раздел 32: отмечает задачу выполненной/пропущенной; когда все
    /// задачи чеклиста закрыты (Done/Skipped) — чеклист автоматически
    /// переходит в Completed. Переоткрытие задачи после завершения снимает
    /// этот статус обратно в InProgress.</summary>
    public async Task<OnboardingChecklistTask> SetTaskStatusAsync(
        Guid taskId, OnboardingTaskStatus status, Guid? completedByUserId, string? notes, CancellationToken ct)
    {
        var task = await db.OnboardingChecklistTasks
            .Include(t => t.Checklist).ThenInclude(c => c.Tasks)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new KeyNotFoundException("Задача чеклиста не найдена.");

        task.Status = status;
        if (notes is not null) task.Notes = notes;

        if (status is OnboardingTaskStatus.Done or OnboardingTaskStatus.Skipped)
        {
            task.CompletedAtUtc = DateTime.UtcNow;
            task.CompletedByUserId = completedByUserId;
        }
        else
        {
            task.CompletedAtUtc = null;
            task.CompletedByUserId = null;
        }

        var checklist = task.Checklist;
        var allClosed = checklist.Tasks.All(t => t.Status is OnboardingTaskStatus.Done or OnboardingTaskStatus.Skipped);
        if (allClosed && checklist.Status != OnboardingChecklistStatus.Completed)
        {
            checklist.Status = OnboardingChecklistStatus.Completed;
            checklist.CompletedAtUtc = DateTime.UtcNow;
        }
        else if (!allClosed && checklist.Status == OnboardingChecklistStatus.Completed)
        {
            checklist.Status = OnboardingChecklistStatus.InProgress;
            checklist.CompletedAtUtc = null;
        }

        await db.SaveChangesAsync(ct);
        return task;
    }
}
