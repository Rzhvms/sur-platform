using Infrastructure.Constants;
using Infrastructure.Observability;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Application.Models;
using Application.Ports.Notifications;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Identity;

/// <inheritdoc/>
/// <param name="configuration">Параметры SMTP из окружения</param>
/// <param name="operations">Метрики попыток отправки</param>
internal sealed class SmtpIdentityInvitationSender(IConfiguration configuration, OperationsMetrics operations) : IIdentityInvitationSender
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

    /// <summary>Вид передачи для метрик</summary>
    private const string SmtpOperation = "smtp";

    /// <summary>Письмо передано SMTP</summary>
    private const string Sent = "sent";

    /// <summary>SMTP не подтвердил передачу</summary>
    private const string Failed = "error";

    /// <summary>Текст при отсутствии действующих зон ответственности</summary>
    private const string NoResponsibilities = "отсутствуют";

    /// <inheritdoc/>
    public async Task SendAsync(IdentityInvitation invitation, string password, CancellationToken cancellationToken)
    {
        if (configuration.GetValue(RecoverySettings.PauseOutbound, false))
        {
            throw new InvalidOperationException(MissingSmtp);
        }

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
        var started = Stopwatch.GetTimestamp();
        try
        {
            await client.SendMailAsync(message, cancellationToken);
            operations.Outcome(SmtpOperation, Sent);
        }
        catch (Exception error) when (error is SmtpException or IOException or System.Net.Sockets.SocketException or OperationCanceledException)
        {
            operations.Outcome(SmtpOperation, Failed);
            throw;
        }
        finally
        {
            operations.Duration(SmtpOperation, started);
        }
    }
}
