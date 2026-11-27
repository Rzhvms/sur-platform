using Application.Settings;
using Application.UseCases.Risks.Interfaces;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto.Request;
using Domain.DalModels;
using Domain.DalModels.Reviews;

namespace Application.UseCases.Risks;

/// <summary>Прикладные операции источников пересмотра</summary>
/// <param name="calendar">Настройки календарной зоны новых расписаний</param>
/// <param name="repository">Хранилище выбранного клиента</param>
internal sealed class ReviewUseCaseManager(IReviewRepository repository, ReviewCalendarSettings calendar) : IReviewUseCaseManager
{
    /// <inheritdoc/>
    public Task<ReviewScheduleDal[]> ListReviewSchedulesAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListReviewSchedulesAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<ReviewScheduleDal> SaveReviewScheduleAsync(RiskActor actor, Guid? id, ReviewScheduleRequest request, string? timeZone = null)
    {
        return repository.SaveReviewScheduleAsync(actor, id, request, timeZone ?? calendar.TimeZone);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReviewScheduleAsync(RiskActor actor, Guid id, Guid version)
    {
        return repository.DeleteReviewScheduleAsync(actor, id, version);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookDal[]> ListReviewWebhooksAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListReviewWebhooksAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookView> GetReviewWebhookAsync(RiskActor actor, Guid id)
    {
        return repository.GetReviewWebhookAsync(actor, id);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookReceipt> CreateReviewWebhookAsync(RiskActor actor, ReviewWebhookRequest request)
    {
        return repository.CreateReviewWebhookAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookView> UpdateReviewWebhookAsync(RiskActor actor, Guid id, ReviewWebhookRequest request)
    {
        return repository.UpdateReviewWebhookAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReviewWebhookAsync(RiskActor actor, Guid id, Guid version)
    {
        return repository.DeleteReviewWebhookAsync(actor, id, version);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookReceipt> RotateReviewWebhookAsync(RiskActor actor, Guid id, RotateReviewTokenRequest request)
    {
        return repository.RotateReviewWebhookAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task AuthorizeReviewWebhookAsync(Guid id, string token)
    {
        return repository.AuthorizeReviewWebhookAsync(id, token);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookAccepted> AcceptReviewWebhookAsync(Guid id, string token, byte[] payload)
    {
        return repository.AcceptReviewWebhookAsync(id, token, payload);
    }

    /// <inheritdoc/>
    public Task<ReviewRunDal[]> ListReviewRunsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListReviewRunsAsync(actor, offset, limit);
    }
}
