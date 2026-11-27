using Application.UseCases.Risks.Interfaces;
using Application.Exceptions;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;
using Application.Ports.Repositories;
using Domain.DalModels.Assessments;
using Domain.DalModels.History;
using Domain.DalModels.Organization;
using Domain.DalModels.Risks;

namespace Application.UseCases.Risks;

/// <summary>Прикладные операции модуля рисков</summary>
/// <param name="history">История изменений и запросы выгрузки</param>
/// <param name="reviews">История и запуск пересмотра</param>
/// <param name="assessments">Оценки риска и настройки расчета</param>
/// <param name="references">Справочники и допустимые варианты выбора</param>
/// <param name="repository">Реестр, паспорт и решения по риску</param>
internal sealed class RiskUseCaseManager(
    IBusinessHistoryRepository history,
    IReviewRepository reviews,
    IRiskAssessmentRepository assessments,
    IRiskReferenceRepository references,
    IRiskRepository repository) : IRiskUseCaseManager
{
    /// <inheritdoc/>
    public Task<RiskRow[]> ListAsync(RiskActor actor, RiskListQuery filter)
    {
        return repository.ListAsync(actor, filter);
    }

    /// <inheritdoc/>
    public Task<RiskColumn[]> BoardAsync(RiskActor actor, RiskListQuery filter)
    {
        return repository.BoardAsync(actor, filter);
    }

    /// <inheritdoc/>
    public async Task<RiskColumn> BoardColumnAsync(RiskActor actor, string columnStatus, RiskListQuery filter)
    {
        if (!RiskRules.BoardStatuses.Contains(columnStatus) ||
            (filter.Status is not null && filter.Status != columnStatus))
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var columns = await repository.BoardAsync(actor, filter with { Status = columnStatus });
        return columns.Single(column => column.Status == columnStatus);
    }

    /// <inheritdoc/>
    public Task<RiskCard> GetAsync(RiskActor actor, Guid id)
    {
        return repository.GetAsync(actor, id);
    }

    /// <inheritdoc/>
    public Task<RiskDal> CreateAsync(RiskActor actor, RiskPassportRequest request)
    {
        return repository.CreateAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<RiskDal> UpdateAsync(RiskActor actor, Guid id, RiskPassportRequest request)
    {
        return repository.UpdateAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task<RiskDal> TransitionAsync(RiskActor actor, Guid id, RiskTransitionRequest request, bool close)
    {
        return repository.TransitionAsync(actor, id, request, close);
    }

    /// <inheritdoc/>
    public Task<RiskCommentDal[]> ListCommentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        return repository.ListCommentsAsync(actor, riskId, offset, limit);
    }

    /// <inheritdoc/>
    public Task<RiskCommentDal> SaveCommentAsync(RiskActor actor, Guid riskId, Guid? id, RiskCommentRequest request)
    {
        return repository.SaveCommentAsync(actor, riskId, id, request);
    }

    /// <inheritdoc/>
    public Task DeleteCommentAsync(RiskActor actor, Guid riskId, Guid id, Guid version)
    {
        return repository.DeleteCommentAsync(actor, riskId, id, version);
    }

    /// <inheritdoc/>
    public Task<RiskReference[]> ListReferencesAsync(RiskActor actor, RiskReferenceKind kind, bool available, int offset, int limit, string? search)
    {
        return references.ListReferencesAsync(actor, kind, available, offset, limit, search);
    }

    /// <inheritdoc/>
    public Task<RiskReference> SaveReferenceAsync(RiskActor actor, RiskReferenceKind kind, Guid? id, RiskReferenceRequest request)
    {
        return references.SaveReferenceAsync(actor, kind, id, request);
    }

    /// <inheritdoc/>
    public Task<JsonElement[]> HistoryAsync(RiskActor actor, BusinessPeriod period, int offset, int limit)
    {
        return history.HistoryAsync(actor, period, offset, limit);
    }

    /// <inheritdoc/>
    public Task<BusinessExportDal> RequestExportAsync(RiskActor actor, BusinessPeriod period)
    {
        return history.RequestExportAsync(actor, period);
    }

    /// <inheritdoc/>
    public Task<RiskProcessOption[]> ListProcessesAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListProcessesAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<AssetDal[]> ListAssetsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListAssetsAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<AssessmentPreview> PreviewAssessmentAsync(RiskActor actor, Guid id, AssessmentRequest request)
    {
        return assessments.PreviewAssessmentAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task<AssessmentPreview> SaveAssessmentAsync(RiskActor actor, Guid id, SaveAssessmentRequest request)
    {
        return assessments.SaveAssessmentAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task<RiskAssessmentDal[]> ListAssessmentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return assessments.ListAssessmentsAsync(actor, id, offset, limit);
    }

    /// <inheritdoc/>
    public Task<ImpactScale?> GetImpactScaleAsync(RiskActor actor, Guid? id, bool admin)
    {
        return assessments.GetImpactScaleAsync(actor, id, admin);
    }

    /// <inheritdoc/>
    public Task<ImpactScale> SaveImpactScaleAsync(RiskActor actor, ImpactScaleRequest request)
    {
        return assessments.SaveImpactScaleAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<RiskAcceptanceSettingsDal> GetAcceptanceSettingsAsync(RiskActor actor)
    {
        return assessments.GetAcceptanceSettingsAsync(actor);
    }

    /// <inheritdoc/>
    public Task<RiskAcceptanceSettingsDal> SaveAcceptanceSettingsAsync(RiskActor actor, AcceptanceSettingsRequest request)
    {
        return assessments.SaveAcceptanceSettingsAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<RiskDal> ChooseDecisionAsync(RiskActor actor, Guid id, RiskDecisionRequest request, string mode)
    {
        return repository.ChooseDecisionAsync(actor, id, request, mode);
    }

    /// <inheritdoc/>
    public AssessmentMatrix GetMatrix()
    {
        return AssessmentMethod.Describe();
    }

    /// <inheritdoc/>
    public Task<RiskProcessOption[]> ListSplitProcessesAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListSplitProcessesAsync(actor, id, offset, limit);
    }

    /// <inheritdoc/>
    public Task<RiskDal> TransferRiskAsync(RiskActor actor, Guid id, TransferRiskRequest request)
    {
        return reviews.TransferRiskAsync(actor, id, request);
    }

    /// <inheritdoc/>
    public Task<RiskReviewView[]> ListRiskReviewsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return reviews.ListRiskReviewsAsync(actor, id, offset, limit);
    }

    /// <inheritdoc/>
    public Task<RiskProcessOption[]> ListTransferProcessesAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTransferProcessesAsync(actor, id, offset, limit);
    }
}
