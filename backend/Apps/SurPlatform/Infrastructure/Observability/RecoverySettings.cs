namespace Infrastructure.Observability;

/// <summary>Настройка безопасного старта восстановленной установки</summary>
internal static class RecoverySettings
{
    /// <summary>Приостанавливает фоновую Jira и выдачу временных паролей через SMTP</summary>
    internal const string PauseOutbound = "Operations:PauseOutbound";
}
