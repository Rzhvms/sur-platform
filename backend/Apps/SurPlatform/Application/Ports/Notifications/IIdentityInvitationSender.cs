using Application.Models;

namespace Application.Ports.Notifications;

/// <summary>
/// Доставка временного доступа из памяти текущей операции
/// </summary>
public interface IIdentityInvitationSender
{
    /// <summary>
    /// Передаёт письмо серверу SMTP
    /// </summary>
    /// <param name="invitation">Версия и адрес доставки</param>
    /// <param name="password">Одноразовый секрет из памяти операции</param>
    /// <param name="cancellationToken">Ограничение времени доставки</param>
    Task SendAsync(IdentityInvitation invitation, string password, CancellationToken cancellationToken);
}
