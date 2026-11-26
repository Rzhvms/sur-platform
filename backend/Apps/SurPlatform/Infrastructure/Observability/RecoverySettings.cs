namespace Infrastructure.Observability;

/// <summary>Настройка безопасного старта восстановленной установки</summary>
internal static class RecoverySettings
{
    /// <summary>Приостанавливает фоновую Jira и начальную настройку администратора</summary>
    internal const string PauseOutbound = "Operations:PauseOutbound";
}
