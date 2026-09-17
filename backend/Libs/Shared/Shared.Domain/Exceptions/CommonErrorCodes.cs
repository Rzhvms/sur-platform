namespace Shared.Domain.Exceptions;

/// <summary>
/// Стабильные коды ошибок, одинаковые для всех микросервисов
/// Предметные ошибки конкретного сервиса должны объявлять собственные коды
/// </summary>
public static class CommonErrorCodes
{
    public const string Validation = "common.validation";
    public const string EntityNotFound = "common.entity_not_found";
    public const string Conflict = "common.conflict";
    public const string AuthenticationRequired = "common.authentication_required";
    public const string AccessForbidden = "common.access_forbidden";
}