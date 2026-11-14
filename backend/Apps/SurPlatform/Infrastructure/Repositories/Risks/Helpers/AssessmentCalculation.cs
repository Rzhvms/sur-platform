using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks.Helpers;

/// <summary>Правила денежного расчёта и маршрута встроенной матрицы</summary>
internal static class AssessmentCalculation
{
    /// <summary>Число календарных часов в неделе</summary>
    private const int HoursPerWeek = 168;

    /// <summary>Число календарных часов в сутках</summary>
    private const int HoursPerDay = 24;

    /// <summary>Количество дробных знаков денежной суммы</summary>
    private const int MoneyScale = 2;

    /// <summary>Минимальный денежный шаг</summary>
    private const decimal Cent = 0.01m;

    /// <summary>Вычисляет входы, тяжесть и балл для новой версии</summary>
    /// <param name="riskId">Оцениваемый риск</param>
    /// <param name="inputs">Ввод пользователя или исторические входы</param>
    /// <param name="rate">Ставка объекта, если оценка по простою</param>
    /// <param name="scale">Применяемая денежная шкала</param>
    /// <returns>Оценка до добавления метаданных записи</returns>
    internal static RiskAssessmentDal Calculate(Guid riskId, AssessmentRequest inputs, decimal? rate, ImpactScale scale)
    {
        var likelihood = AssessmentMethod.GetLikelihoodWeight(inputs.LikelihoodLevelId)
            ?? throw new IdentityCommandException(400, RiskRules.Invalid);
        decimal loss;
        decimal? hours = null;
        try
        {
            if (rate is decimal hourlyCost)
            {
                if (inputs.LossAmount is not null || !IsMoney(hourlyCost) ||
                    inputs.DowntimeWeeks is < 0 || inputs.DowntimeDays is < 0 || inputs.DowntimeHours is < 0 ||
                    (inputs.DowntimeWeeks is null && inputs.DowntimeDays is null && inputs.DowntimeHours is null))
                {
                    throw new IdentityCommandException(400, RiskRules.Invalid);
                }

                hours = (decimal)(inputs.DowntimeWeeks ?? 0) * HoursPerWeek +
                    (decimal)(inputs.DowntimeDays ?? 0) * HoursPerDay + (inputs.DowntimeHours ?? 0);
                loss = decimal.Round(hours.Value * hourlyCost, MoneyScale, MidpointRounding.AwayFromZero);
            }
            else
            {
                if (inputs.LossAmount is not decimal amount || !IsMoney(amount) ||
                    inputs.DowntimeHours is not null || inputs.DowntimeWeeks is not null || inputs.DowntimeDays is not null)
                {
                    throw new IdentityCommandException(400, RiskRules.Invalid);
                }

                loss = amount;
            }
        }
        catch (OverflowException)
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var impact = FindImpact(loss, scale);
        return new RiskAssessmentDal
        {
            RiskId = riskId, MethodId = AssessmentMethod.Id, ScaleVersionId = scale.Version.Id,
            LikelihoodLevelId = inputs.LikelihoodLevelId, ImpactLevelId = AssessmentMethod.ImpactId(impact),
            RiskScore = likelihood * impact, LossAmount = loss, DowntimeWeeks = inputs.DowntimeWeeks,
            DowntimeDays = inputs.DowntimeDays, DowntimeHours = inputs.DowntimeHours, DowntimeTotalHours = hours,
            HourlyDowntimeCostSnapshot = rate
        };
    }

    /// <summary>Находит денежный диапазон, включая его границы</summary>
    /// <param name="loss">Неотрицательная сумма после округления</param>
    /// <param name="scale">Версия шкалы</param>
    /// <returns>Вес тяжести</returns>
    internal static int FindImpact(decimal loss, ImpactScale scale)
    {
        var matches = scale.Thresholds.Where(level => loss >= level.MinLossAmount &&
            (level.MaxLossAmount is null || loss <= level.MaxLossAmount)).Take(2).ToArray();
        if (matches.Length != 1)
        {
            throw new IdentityCommandException(409, RiskRules.ScaleRequired);
        }
        return matches[0].Weight;
    }

    /// <summary>Строит все денежные диапазоны для публикации</summary>
    /// <param name="scaleId">Новая версия шкалы</param>
    /// <param name="minimums">Нижние пороги уровней 1–5</param>
    /// <returns>Шесть диапазонов, включая возможно пустой нулевой</returns>
    internal static ImpactThresholdDal[] BuildThresholds(Guid scaleId, decimal[]? minimums)
    {
        if (minimums is not { Length: AssessmentMethod.MaxWeight })
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var levels = new ImpactThresholdDal[AssessmentMethod.MaxWeight + 1];
        for (var weight = 0; weight < levels.Length; weight++)
        {
            var minimum = weight == 0 ? 0 : minimums[weight - 1];
            if (!IsMoney(minimum) || (weight > 1 && minimum <= minimums[weight - 2]))
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }
        }

        for (var weight = 0; weight < levels.Length; weight++)
        {
            levels[weight] = new ImpactThresholdDal
            {
                ScaleId = scaleId, Weight = weight, MinLossAmount = weight == 0 ? 0 : minimums[weight - 1],
                MaxLossAmount = weight == AssessmentMethod.MaxWeight ? null : minimums[weight] - Cent
            };
        }

        return levels;
    }

    /// <summary>Определяет маршрут по фактическому баллу</summary>
    /// <param name="score">Оценка встроенного метода</param>
    /// <param name="settings">Пороги маршрута клиента</param>
    /// <returns>Код уровня принятия</returns>
    internal static string Route(decimal score, RiskAcceptanceSettingsDal settings)
    {
        if (score <= settings.OwnerMaxScore)
        {
            return OrganizationRoles.Owner;
        }
        if (score <= settings.DirectorMaxScore)
        {
            return OrganizationRoles.Director;
        }
        return score <= settings.CeoMaxScore ? OrganizationRoles.Ceo : RiskRules.TreatmentRequired;
    }

    /// <summary>Проверяет формат неотрицательной суммы в рублях</summary>
    /// <param name="value">Денежное значение</param>
    /// <returns>Допустима ли точность значения</returns>
    private static bool IsMoney(decimal value)
    {
        return value >= 0 && decimal.Round(value, MoneyScale) == value;
    }
}
