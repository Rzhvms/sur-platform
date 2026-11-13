namespace Application.Constants;

/// <summary>
/// Коды ролей, обязательных для организационных связей
/// </summary>
public static class OrganizationRoles
{
    /// <summary>
    /// Генеральный директор клиента
    /// </summary>
    public const string Ceo = "CEO";

    /// <summary>
    /// Держатель бюджета дирекции
    /// </summary>
    public const string Director = "DIRECTOR";

    /// <summary>
    /// Специалист по оценке рисков
    /// </summary>
    public const string RiskSpecialist = "RISK_SPECIALIST";

    /// <summary>
    /// Владелец процесса
    /// </summary>
    public const string Owner = "OWNER";
}
