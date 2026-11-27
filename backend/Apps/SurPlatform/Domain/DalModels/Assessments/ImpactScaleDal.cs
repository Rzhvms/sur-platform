using Domain.DalModels.Base;

namespace Domain.DalModels.Assessments;

/// <summary>
/// Опубликованная версия денежной шкалы тяжести последствий
/// </summary>
public sealed record ImpactScaleDal : DalModelBase
{
    /// <summary>
    /// Порядковый номер публикации шкалы в клиентской базе
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// Идентификатор пользователя, опубликовавшего шкалу
    /// </summary>
    public Guid CreatedById { get; init; }

    /// <summary>
    /// Время публикации версии шкалы в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }
}