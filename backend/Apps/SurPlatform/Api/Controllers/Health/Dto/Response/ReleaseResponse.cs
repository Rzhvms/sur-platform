namespace Api.Controllers.Health.Dto.Response;

/// <summary>Публичные метаданные API для проверки совместимости SPA</summary>
/// <param name="Version">Версия развёрнутого API</param>
/// <param name="Revision">Идентификатор исходной ревизии</param>
/// <param name="ContractVersion">Поддерживаемая версия HTTP-контракта</param>
public sealed record ReleaseResponse(string Version, string Revision, int ContractVersion);
