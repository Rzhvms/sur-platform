using System.Security.Claims;
using Application.Constants;
using Application.Models;
using Core.Api.Extensions;

namespace Api.Extensions;

/// <summary>Читает идентификаторы из JWT после штатной аутентификации</summary>
internal static class RiskActorExtensions
{
    /// <summary>Формирует участника команды из проверенной сессии</summary>
    /// <param name="principal">Аутентифицированный пользователь</param>
    /// <returns>Идентификаторы и версия доступа</returns>
    internal static RiskActor GetRiskActor(this ClaimsPrincipal principal)
    {
        return new RiskActor(principal.GetUserId(),
            Guid.Parse(principal.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty),
            long.Parse(principal.FindFirst(AccessTokenClaimTypes.AccessVersion)?.Value ?? string.Empty,
                System.Globalization.CultureInfo.InvariantCulture));
    }
}
