using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Onboarding;

/// <summary>
/// Раздел 25: выбирает подходящий активный шаблон чеклиста для нового
/// сотрудника — по категории персонала, должности и клинике. Шаблон
/// подходит, если каждое заданное в нём условие (не-null) совпадает; из всех
/// подходящих выбирается самый специфичный (больше совпавших условий).
/// Порядок специфичности: Position+Category+Clinic > любые два условия >
/// одно условие > общий шаблон (все три условия — null, подходит всем).
/// </summary>
public sealed class OnboardingChecklistTemplateSelector(TibbiNavDbContext db)
{
    public async Task<OnboardingChecklistTemplate?> SelectAsync(PersonnelCategory category, Guid positionId, Guid? clinicId, CancellationToken ct)
    {
        var candidates = await db.OnboardingChecklistTemplates
            .Include(t => t.Tasks)
            .Where(t => t.IsActive)
            .Where(t => t.PersonnelCategory == null || t.PersonnelCategory == category)
            .Where(t => t.PositionId == null || t.PositionId == positionId)
            .Where(t => t.ClinicId == null || t.ClinicId == clinicId)
            .ToListAsync(ct);

        return candidates
            .OrderByDescending(Specificity)
            .FirstOrDefault();
    }

    private static int Specificity(OnboardingChecklistTemplate t) =>
        (t.PersonnelCategory is not null ? 1 : 0) +
        (t.PositionId is not null ? 1 : 0) +
        (t.ClinicId is not null ? 1 : 0);
}
