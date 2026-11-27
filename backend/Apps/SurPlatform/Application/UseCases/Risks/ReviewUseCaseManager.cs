using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto.Request;
using Domain.DalModels;

namespace Application.UseCases.Risks;

/// <summary>Прикладные операции источников пересмотра</summary>
/// <param name="repository">Хранилище выбранного клиента</param>
public sealed class ReviewUseCaseManager(IReviewRepository repository)
{
    /// <summary>Список расписаний</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewScheduleDal[]> ListReviewSchedulesAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListReviewSchedulesAsync(actor, offset, limit);
    }

    /// <summary>Создание или изменение расписания</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <param name="timeZone">Календарная зона по умолчанию</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewScheduleDal> SaveReviewScheduleAsync(RiskActor actor, Guid? id, ReviewScheduleRequest request, string timeZone)
    {
        return repository.SaveReviewScheduleAsync(actor, id, request, timeZone);
    }

    /// <summary>Удаление расписания</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Результат операции</returns>
    public Task<bool> DeleteReviewScheduleAsync(RiskActor actor, Guid id, Guid version)
    {
        return repository.DeleteReviewScheduleAsync(actor, id, version);
    }

    /// <summary>Список внешних источников</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewWebhookDal[]> ListReviewWebhooksAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListReviewWebhooksAsync(actor, offset, limit);
    }

    /// <summary>Действующая область внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewWebhookView> GetReviewWebhookAsync(RiskActor actor, Guid id)
    {
        return repository.GetReviewWebhookAsync(actor, id);
    }

    /// <summary>Создание внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewWebhookReceipt> CreateReviewWebhookAsync(RiskActor actor, ReviewWebhookRequest request)
    {
        return repository.CreateReviewWebhookAsync(actor, request);
    }

    /// <summary>Изменение внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewWebhookView> UpdateReviewWebhookAsync(RiskActor actor, Guid id, ReviewWebhookRequest request)
    {
        return repository.UpdateReviewWebhookAsync(actor, id, request);
    }

    /// <summary>Удаление внешнего источника</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Результат операции</returns>
    public Task<bool> DeleteReviewWebhookAsync(RiskActor actor, Guid id, Guid version)
    {
        return repository.DeleteReviewWebhookAsync(actor, id, version);
    }

    /// <summary>Подтверждённая замена секрета</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewWebhookReceipt> RotateReviewWebhookAsync(RiskActor actor, Guid id, RotateReviewTokenRequest request)
    {
        return repository.RotateReviewWebhookAsync(actor, id, request);
    }

    /// <summary>Проверка секрета до чтения тела</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="token">Секрет внешнего источника</param>
    public Task AuthorizeReviewWebhookAsync(Guid id, string token)
    {
        return repository.AuthorizeReviewWebhookAsync(id, token);
    }

    /// <summary>Сохранение входящего сигнала</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="token">Секрет внешнего источника</param>
    /// <param name="payload">Исходное авторизованное тело</param>
    /// <returns>Результат операции</returns>
    public Task<ReviewWebhookAccepted> AcceptReviewWebhookAsync(Guid id, string token, byte[] payload)
    {
        return repository.AcceptReviewWebhookAsync(id, token, payload);
    }

    /// <summary>Читает историю первичной обработки</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Запуски без исходных тел</returns>
    public Task<ReviewRunDal[]> ListReviewRunsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListReviewRunsAsync(actor, offset, limit);
    }
}
