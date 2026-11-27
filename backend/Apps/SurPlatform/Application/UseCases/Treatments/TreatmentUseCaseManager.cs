using Application.UseCases.Treatments.Interfaces;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Treatments.Dto.Request;
using Domain.DalModels;
using Domain.DalModels.Risks;
using Domain.DalModels.Treatments;

namespace Application.UseCases.Treatments;

/// <summary>Прикладные операции мер, исполнителей и согласований</summary>
/// <param name="repository">Хранилище рисков текущего клиента</param>
internal sealed class TreatmentUseCaseManager(ITreatmentRepository repository) : ITreatmentUseCaseManager
{
    /// <inheritdoc/>
    public Task<TreatmentRegistry> ListTreatmentRegistryAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListTreatmentRegistryAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<TreatmentDal[]> ListTreatmentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        return repository.ListTreatmentsAsync(actor, riskId, offset, limit);
    }

    /// <inheritdoc/>
    public Task<TreatmentCard> GetTreatmentAsync(RiskActor actor, Guid id)
    {
        return repository.GetTreatmentAsync(actor, id);
    }

    /// <inheritdoc/>
    public Task<TreatmentDal> SaveTreatmentDraftAsync(RiskActor actor, Guid id, bool create, TreatmentDraftRequest request)
    {
        return repository.SaveTreatmentDraftAsync(actor, id, create, request);
    }

    /// <inheritdoc/>
    public Task<TreatmentDal> TransitionTreatmentAsync(RiskActor actor, Guid id, string target, TreatmentActionRequest request)
    {
        return repository.TransitionTreatmentAsync(actor, id, target, request);
    }

    /// <inheritdoc/>
    public Task<RiskDal> StartTreatmentCycleAsync(RiskActor actor, Guid riskId, TreatmentActionRequest request)
    {
        return repository.StartTreatmentCycleAsync(actor, riskId, request);
    }

    /// <inheritdoc/>
    public Task<TreatmentDal> AssignTreatmentAsync(RiskActor actor, Guid id, bool dispatch, TreatmentActionRequest request)
    {
        return repository.AssignTreatmentAsync(actor, id, dispatch, request);
    }

    /// <inheritdoc/>
    public Task<TreatmentExecutorOption[]> ListTreatmentExecutorsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTreatmentExecutorsAsync(actor, id, offset, limit);
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalDal> SubmitTreatmentAsync(RiskActor actor, Guid id, TreatmentActionRequest request)
    {
        return repository.SubmitTreatmentAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalDal> DecideTreatmentApprovalAsync(RiskActor actor, Guid id, string decision, TreatmentActionRequest request)
    {
        return repository.DecideTreatmentApprovalAsync(actor, id, decision, request);
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalQueue> ListTreatmentApprovalsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListTreatmentApprovalsAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalSettingsDal> GetTreatmentSettingsAsync(RiskActor actor)
    {
        return repository.GetTreatmentSettingsAsync(actor);
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalSettingsDal> SaveTreatmentSettingsAsync(RiskActor actor, TreatmentThresholdsRequest request)
    {
        return repository.SaveTreatmentSettingsAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<TreatmentCommentDal[]> ListTreatmentCommentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTreatmentCommentsAsync(actor, id, offset, limit);
    }

    /// <inheritdoc/>
    public Task<TreatmentCommentDal> SaveTreatmentCommentAsync(RiskActor actor, Guid id, Guid? commentId, TreatmentCommentRequest request, bool delete)
    {
        return repository.SaveTreatmentCommentAsync(actor, id, commentId, request, delete);
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalDal[]> ListTreatmentApprovalHistoryAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTreatmentApprovalHistoryAsync(actor, id, offset, limit);
    }
}
