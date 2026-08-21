using TibbiNav.Domain.Onboarding;

namespace TibbiNav.Application.Onboarding;

/// <summary>Раздел 31-32: срок задачи по умолчанию (в днях от даты выхода
/// HireDate), если автор шаблона не задал DueOffsetDays явно на конкретной
/// задаче — так шаблон можно собрать, вообще не думая о датах, а точечно
/// сдвигать только там, где это важно.</summary>
public static class OnboardingStageDefaults
{
    public static readonly IReadOnlyDictionary<OnboardingStage, int> DefaultOffsetDays = new Dictionary<OnboardingStage, int>
    {
        [OnboardingStage.Preboarding] = -7,
        [OnboardingStage.Day1] = 0,
        [OnboardingStage.Week1] = 7,
        [OnboardingStage.Day30] = 30,
        [OnboardingStage.Day60] = 60,
        [OnboardingStage.Day90] = 90,
    };
}
