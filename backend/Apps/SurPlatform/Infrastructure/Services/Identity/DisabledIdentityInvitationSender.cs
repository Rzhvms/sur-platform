using Application.Models;
using Application.Ports.Notifications;

namespace Infrastructure.Services.Identity;

/// <summary>
/// Возвращает отказ доставки на время отключения почтового транспорта
/// </summary>
internal sealed class DisabledIdentityInvitationSender : IIdentityInvitationSender
{
    /// <summary>Причина, по которой доставка не может быть подтверждена</summary>
    private const string DeliveryDisabled = "Отправка приглашений отключена";

    /// <inheritdoc/>
    public Task SendAsync(IdentityInvitation invitation, string password, CancellationToken cancellationToken)
    {
        return Task.FromException(new InvalidOperationException(DeliveryDisabled));
    }
}
