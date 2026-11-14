namespace Application.UseCases.Users.Dto.Response;

/// <summary>Права текущего пользователя в выбранной клиентской базе</summary>
/// <param name="UserId">Идентификатор пользователя из аутентифицированной сессии</param>
/// <param name="RightCodes">Уникальные стабильные коды эффективных прав</param>
public sealed record CurrentUserAccessResponse(Guid UserId, string[] RightCodes);
