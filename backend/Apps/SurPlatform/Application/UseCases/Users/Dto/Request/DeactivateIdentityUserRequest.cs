namespace Application.UseCases.Users.Dto.Request;

/// <summary>Подтверждение деактивации прочитанной учётной записи</summary>
public sealed record DeactivateIdentityUserRequest
{
    /// <summary>Ожидаемая версия учётной записи</summary>
    public Guid Version { get; init; }
}
