using Api.Constants;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Health;

/// <summary>Публичные версии хоста и поддерживаемого контракта SPA</summary>
/// <param name="configuration">Публичные метаданные развёрнутого релиза</param>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[Route(ReleaseRoute)]
public sealed class ReleaseController(IConfiguration configuration) : ControllerBase
{
    /// <summary>Маршрут согласования версии SPA и API</summary>
    private const string ReleaseRoute = "health/release";

    /// <summary>Публичная версия API</summary>
    private const string VersionSetting = "Release:Version";

    /// <summary>Идентификатор исходной ревизии API</summary>
    private const string RevisionSetting = "Release:Revision";

    /// <summary>Исходная версия API</summary>
    private const string DefaultVersion = "0.1.0";

    /// <summary>Ревизия до маркировки кандидата выпуска</summary>
    private const string UnrecordedRevision = "unrecorded";

    /// <summary>Версия контракта, поддерживаемая текущим хостом</summary>
    private const int SupportedContract = 1;

    /// <summary>Читает публичные метаданные без кеширования</summary>
    /// <returns>Версия хоста и поддерживаемого контракта</returns>
    [HttpGet]
    [ProducesResponseType<Dto.Response.ReleaseResponse>(StatusCodes.Status200OK)]
    public Dto.Response.ReleaseResponse Get()
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        return new Dto.Response.ReleaseResponse(
            configuration[VersionSetting] ?? DefaultVersion,
            configuration[RevisionSetting] ?? UnrecordedRevision,
            SupportedContract);
    }
}
