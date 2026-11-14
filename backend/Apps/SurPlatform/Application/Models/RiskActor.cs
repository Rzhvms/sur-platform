namespace Application.Models;

/// <summary>Участник команды из проверенного клиентского JWT</summary>
/// <param name="UserId">Пользователь</param>
/// <param name="SessionId">Действующая сессия</param>
/// <param name="AccessVersion">Версия прав из JWT</param>
public sealed record RiskActor(Guid UserId, Guid SessionId, long AccessVersion);
