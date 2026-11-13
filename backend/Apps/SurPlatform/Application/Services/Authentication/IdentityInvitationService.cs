using System.Security.Cryptography;
using Application.Constants;
using Application.Helpers.Encryption;
using Application.Models;
using Application.Ports.Notifications;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;

namespace Application.Services.Authentication;

/// <summary>
/// Создаёт временный секрет и доставляет его после фиксации выдачи
/// </summary>
/// <param name="repository">Хранилище выдачи текущего клиента</param>
/// <param name="sender">Транспорт письма доступа</param>
public sealed class IdentityInvitationService(IIdentityAdministrationRepository repository, IIdentityInvitationSender sender)
{
    /// <summary>
    /// Issued account missing
    /// </summary>
    private const string IssuedAccountMissing = "Выданная учётная запись отсутствует";

    /// <summary>
    /// Число случайных байтов временного пароля
    /// </summary>
    private const int PasswordBytes = 24;

    /// <summary>
    /// Предельное время передачи письма до фиксации ошибки доставки
    /// </summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Создаёт учётную запись и при необходимости сохраняет оргназначения
    /// </summary>
    /// <param name="actor">Инициатор выдачи</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="request">Данные пользователя</param>
    /// <returns>Учётная запись с текущим состоянием письма</returns>
    public Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request)
    {
        return IssueAndReadAsync(hash => repository.CreateAsync(actor, sessionId, request, hash));
    }

    /// <summary>
    /// Повторно выдаёт временный пароль с опциональной активацией
    /// </summary>
    /// <param name="actor">Инициатор выдачи</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Учётная запись получателя</param>
    /// <param name="activation">Параметры активации или null</param>
    /// <param name="version">Ожидаемая версия учётной записи</param>
    /// <returns>Карточка с текущим состоянием письма</returns>
    public Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId,
        ActivateIdentityUserRequest? activation, Guid version)
    {
        return IssueAndReadAsync(hash => repository.IssueAsync(actor, sessionId, userId, activation, version, hash));
    }

    /// <summary>
    /// Выполняет служебную первоначальную или повторную выдачу доступа
    /// </summary>
    /// <param name="email">Настроенный адрес первого администратора</param>
    /// <param name="fullName">Настроенное ФИО первого администратора</param>
    /// <param name="reissue">Запрошена повторная выдача</param>
    /// <returns>Состояние доставки или null, если запись уже существует</returns>
    public async Task<string?> BootstrapAsync(string email, string fullName, bool reissue)
    {
        var password = NewPassword();
        var invitation = await repository.BootstrapAsync(email, fullName, Hash(password), reissue);
        return invitation is null ? null : await DeliverAsync(invitation, password);
    }

    /// <summary>
    /// Сохраняет выдачу, отправляет письмо и читает итоговую карточку
    /// </summary>
    /// <param name="persist">Команда фиксации защищённого представления секрета</param>
    /// <returns>Карточка после доставки</returns>
    private async Task<IdentityUser> IssueAndReadAsync(Func<string, Task<IdentityInvitation>> persist)
    {
        var password = NewPassword();
        var invitation = await persist(Hash(password));
        await DeliverAsync(invitation, password);
        return await repository.FindAsync(invitation.UserId)
            ?? throw new InvalidOperationException(IssuedAccountMissing);
    }

    /// <summary>
    /// Передаёт только актуальную версию и сохраняет результат доставки
    /// </summary>
    /// <param name="invitation">Версия выдачи из клиентской базы</param>
    /// <param name="password">Пароль, удерживаемый в памяти запроса</param>
    /// <returns>Результат попытки доставки</returns>
    private async Task<string> DeliverAsync(IdentityInvitation invitation, string password)
    {
        if (!await repository.IsInvitationCurrentAsync(invitation.UserId, invitation.Version))
        {
            return IdentityAccounts.InvitationFailed;
        }

        var status = IdentityAccounts.InvitationFailed;
        using var timeout = new CancellationTokenSource(DeliveryTimeout);
        try
        {
            await sender.SendAsync(invitation, password, timeout.Token);
            status = IdentityAccounts.InvitationSent;
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or System.IO.IOException or System.Net.Sockets.SocketException or OperationCanceledException or
            ArgumentException or InvalidOperationException or FormatException)
        {
            // SMTP может включать в исключение адрес получателя или ответ сервера
        }

        await repository.FinishInvitationAsync(invitation.UserId, invitation.Version, status);
        return status;
    }

    /// <summary>
    /// Генерирует независимый временный пароль
    /// </summary>
    /// <returns>Пароль для однократной выдачи</returns>
    private static string NewPassword()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(PasswordBytes));
    }

    /// <summary>
    /// Хеширует пароль перед записью в клиентскую базу
    /// </summary>
    /// <param name="password">Временный пароль</param>
    /// <returns>Хеш с уникальной солью</returns>
    private static string Hash(string password)
    {
        return PasswordEncryptionHelper.HashPassword(password, PasswordEncryptionHelper.GenerateSalt());
    }
}
