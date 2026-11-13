namespace Application.Constants;

/// <summary>
/// Коды отказов административных операций Identity
/// </summary>
public static class IdentityErrorCodes
{
    /// <summary>
    /// Машиночитаемый код Identity для access denied
    /// </summary>
    public const string AccessDenied = "ACCESS_DENIED";

    /// <summary>
    /// Машиночитаемый код Identity для active role required
    /// </summary>
    public const string ActiveRoleRequired = "ACTIVE_ROLE_REQUIRED";

    /// <summary>
    /// Машиночитаемый код Identity для active user fields invalid
    /// </summary>
    public const string ActiveUserFieldsInvalid = "ACTIVE_USER_FIELDS_INVALID";

    /// <summary>
    /// Машиночитаемый код Identity для admin role protected
    /// </summary>
    public const string AdminRoleProtected = "ADMIN_ROLE_PROTECTED";

    /// <summary>
    /// Системный каталог конфликтует с уже существующими записями
    /// </summary>
    public const string CatalogConflict = "IDENTITY_CATALOG_CONFLICT";

    /// <summary>
    /// Машиночитаемый код Identity для email already exists
    /// </summary>
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";

    /// <summary>
    /// Машиночитаемый код Identity для email required
    /// </summary>
    public const string EmailRequired = "EMAIL_REQUIRED";

    /// <summary>
    /// Машиночитаемый код Identity для first admin not found
    /// </summary>
    public const string FirstAdminNotFound = "FIRST_ADMIN_NOT_FOUND";

    /// <summary>
    /// Машиночитаемый код Identity для full name required
    /// </summary>
    public const string FullNameRequired = "FULL_NAME_REQUIRED";

    /// <summary>
    /// Машиночитаемый код Identity для inactive user fields invalid
    /// </summary>
    public const string InactiveUserFieldsInvalid = "INACTIVE_USER_FIELDS_INVALID";

    /// <summary>
    /// Машиночитаемый код Identity для last administrator
    /// </summary>
    public const string LastAdministrator = "LAST_ADMINISTRATOR";

    /// <summary>
    /// Машиночитаемый код Identity для role deactivation blocked last role
    /// </summary>
    public const string RoleDeactivationBlockedLastRole = "ROLE_DEACTIVATION_BLOCKED_LAST_ROLE";

    /// <summary>
    /// Машиночитаемый код Identity для role not found
    /// </summary>
    public const string RoleNotFound = "ROLE_NOT_FOUND";

    /// <summary>
    /// Машиночитаемый код Identity для stale version
    /// </summary>
    public const string StaleVersion = "STALE_VERSION";

    /// <summary>
    /// Машиночитаемый код Identity для unknown right
    /// </summary>
    public const string UnknownRight = "UNKNOWN_RIGHT";

    /// <summary>
    /// Машиночитаемый код Identity для user already active
    /// </summary>
    public const string UserAlreadyActive = "USER_ALREADY_ACTIVE";

    /// <summary>
    /// Машиночитаемый код Identity для user not found
    /// </summary>
    public const string UserNotFound = "USER_NOT_FOUND";
}
