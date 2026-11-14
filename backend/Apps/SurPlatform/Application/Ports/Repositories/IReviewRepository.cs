using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>Администрирование и доверенный вход автоматического пересмотра</summary>
public interface IReviewRepository
{
    /// <summary>Список расписаний</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<ReviewScheduleDal[]> ListReviewSchedulesAsync(RiskActor actor, int offset, int limit);

    /// <summary>Создание или изменение расписания</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <param name="timeZone">Календарная зона по умолчанию</param>
    /// <returns>Результат операции</returns>
    Task<ReviewScheduleDal> SaveReviewScheduleAsync(RiskActor actor, Guid? id, ReviewScheduleRequest request, string timeZone);

    /// <summary>Удаление расписания</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Результат операции</returns>
    Task<bool> DeleteReviewScheduleAsync(RiskActor actor, Guid id, Guid version);

    /// <summary>Список внешних источников</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<ReviewWebhookDal[]> ListReviewWebhooksAsync(RiskActor actor, int offset, int limit);

    /// <summary>Действующая область внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <returns>Результат операции</returns>
    Task<ReviewWebhookView> GetReviewWebhookAsync(RiskActor actor, Guid id);

    /// <summary>Создание внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции</returns>
    Task<ReviewWebhookReceipt> CreateReviewWebhookAsync(RiskActor actor, ReviewWebhookRequest request);

    /// <summary>Изменение внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции</returns>
    Task<ReviewWebhookView> UpdateReviewWebhookAsync(RiskActor actor, Guid id, ReviewWebhookRequest request);

    /// <summary>Удаление внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Результат операции</returns>
    Task<bool> DeleteReviewWebhookAsync(RiskActor actor, Guid id, Guid version);

    /// <summary>Подтверждённая замена секрета</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции</returns>
    Task<ReviewWebhookReceipt> RotateReviewWebhookAsync(RiskActor actor, Guid id, RotateReviewTokenRequest request);

    /// <summary>Проверка секрета до чтения тела</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="token">Секрет внешнего источника</param>
    Task AuthorizeReviewWebhookAsync(Guid id, string token);

    /// <summary>Сохранение входящего сигнала</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="token">Секрет внешнего источника</param>
    /// <param name="payload">Исходное авторизованное тело</param>
    /// <returns>Результат операции</returns>
    Task<ReviewWebhookAccepted> AcceptReviewWebhookAsync(Guid id, string token, byte[] payload);

    /// <summary>Читает историю первичной обработки</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Запуски без исходных тел</returns>
    Task<ReviewRunDal[]> ListReviewRunsAsync(RiskActor actor, int offset, int limit);
}
