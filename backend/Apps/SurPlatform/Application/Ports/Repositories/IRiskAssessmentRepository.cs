using Application.UseCases.Treatments.Dto.Request;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;
using Domain.DalModels.Assessments;

namespace Application.Ports.Repositories;

/// <summary>Оценки риска, публикация шкалы и пороги принятия</summary>
public interface IRiskAssessmentRepository
{
    /// <summary>Вычисляет предварительную оценку</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Входы оценки</param>
    /// <returns>Результат операции</returns>
    Task<AssessmentPreview> PreviewAssessmentAsync(RiskActor actor, Guid id, AssessmentRequest request);

    /// <summary>Сохраняет бизнес-оценку и активирует риск</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Входы расчёта и версии</param>
    /// <returns>Результат операции</returns>
    Task<AssessmentPreview> SaveAssessmentAsync(RiskActor actor, Guid id, SaveAssessmentRequest request);

    /// <summary>Читает историю оценок риска</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<RiskAssessmentDal[]> ListAssessmentsAsync(RiskActor actor, Guid id, int offset, int limit);

    /// <summary>Читает опубликованную денежную шкалу</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Версия шкалы или null для текущей</param>
    /// <param name="admin">Административный запрос</param>
    /// <returns>Результат операции</returns>
    Task<ImpactScale?> GetImpactScaleAsync(RiskActor actor, Guid? id, bool admin);

    /// <summary>Публикует денежную шкалу и выполняет пересчёт</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="request">Ожидаемая версия и нижние пороги</param>
    /// <returns>Результат операции</returns>
    Task<ImpactScale> SaveImpactScaleAsync(RiskActor actor, ImpactScaleRequest request);

    /// <summary>Читает пороги принятия</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <returns>Результат операции</returns>
    Task<RiskAcceptanceSettingsDal> GetAcceptanceSettingsAsync(RiskActor actor);

    /// <summary>Сохраняет пороги принятия</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="request">Новые пороги и область применения</param>
    /// <returns>Результат операции</returns>
    Task<RiskAcceptanceSettingsDal> SaveAcceptanceSettingsAsync(RiskActor actor, AcceptanceSettingsRequest request);
}
