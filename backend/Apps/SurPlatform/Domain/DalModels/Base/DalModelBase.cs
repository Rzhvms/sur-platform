namespace Domain.DalModels.Base;

/// <summary>
/// Основа записи с идентификатором Guid
/// </summary>
public abstract record DalModelBase
{
    /// <summary>
    /// Идентификатор записи в клиентской базе
    /// </summary>
    public Guid Id { get; init; }
}