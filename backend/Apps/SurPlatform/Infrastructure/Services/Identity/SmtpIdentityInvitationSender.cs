using Infrastructure.Constants;
using System.Net;
using System.Net.Mail;
using Application.Models;
using Application.Ports.Notifications;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Identity;

/// <inheritdoc/>
/// <param name="configuration">Параметры SMTP из окружения</param>
internal sealed class SmtpIdentityInvitationSender(IConfiguration configuration) : IIdentityInvitationSender
{
    /// <summary>
    /// Smtp section
    /// </summary>
    private const string SmtpSection = "Identity:Smtp";

    /// <summary>
    /// Sender key
    /// </summary>
    private const string SenderKey = "From";

    /// <summary>
    /// Missing smtp
    /// </summary>
    private const string MissingSmtp = "Для доставки доступа требуется настройка SMTP";

    /// <summary>
    /// Port key
    /// </summary>
    private const string PortKey = "Port";

    /// <summary>
    /// Username key
    /// </summary>
    private const string UsernameKey = "Username";

    /// <summary>
    /// Password key
    /// </summary>
    private const string PasswordKey = "Password";

    /// <summary>
    /// Invitation subject
    /// </summary>
    private const string InvitationSubject = "Доступ к СУР";

    /// <summary>
    /// Role separator
    /// </summary>
    private const string RoleSeparator = ", ";

    /// <summary>Текст при отсутствии действующих зон ответственности</summary>
    private const string NoResponsibilities = "отсутствуют";

    /// <inheritdoc/>
    public async Task SendAsync(IdentityInvitation invitation, string password, CancellationToken cancellationToken)
    {
        var smtp = configuration.GetSection(SmtpSection);
        var host = smtp[ClientConfigurationKeys.Host];
        var sender = smtp[SenderKey];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) || invitation.Email is null)
        {
            throw new InvalidOperationException(MissingSmtp);
        }

        using var client = new SmtpClient(host, smtp.GetValue(PortKey, 587))
        {
            EnableSsl = true
        };
        var username = smtp[UsernameKey];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, smtp[PasswordKey]);
        }

        var responsibilities = invitation.Responsibilities.Length == 0
            ? NoResponsibilities
            : string.Join(RoleSeparator, invitation.Responsibilities);
        using var message = new MailMessage(sender, invitation.Email)
        {
            Subject = InvitationSubject,
            Body = $"""
                {invitation.FullName}

                Временный пароль: {password}
                Роли: {string.Join(RoleSeparator, invitation.Roles)}
                Зоны ответственности: {responsibilities}.
                При первом входе смените временный пароль.
                """
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
