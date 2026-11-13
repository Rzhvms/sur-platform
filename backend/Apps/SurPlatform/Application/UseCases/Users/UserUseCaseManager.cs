using Application.Helpers.Encryption;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;

namespace Application.UseCases.Users;

/// <inheritdoc />
internal sealed class UserUseCaseManager(IUserRepository userRepository) : IUserUseCaseManager
{
    /// <summary>
    /// Incorrect password
    /// </summary>
    private const string IncorrectPassword = "Неверный текущий пароль";

    /// <summary>
    /// Password changed
    /// </summary>
    private const string PasswordChanged = "Пароль изменён";

    /// <summary>
    /// Credentials changed
    /// </summary>
    private const string CredentialsChanged = "Учётные данные изменились или срок доступа истёк";

    /// <inheritdoc/>
    public async Task<ChangeUserPasswordResponse> ChangePasswordAsync(Guid id, ChangeUserPasswordRequest request)
    {
        var credential = await userRepository.GetCredentialAsync(id);

        var isAccess = PasswordEncryptionHelper.VerifyPassword(request.OldPassword, credential.Password);

        if (!isAccess)
        {
            return new ChangeUserPasswordResponse
            {
                IsSuccess = false,
                Message = IncorrectPassword
            };
        }

        var currentDateTime = DateTime.UtcNow;
        var hashedSalt = PasswordEncryptionHelper.GenerateSalt();
        var hashedPassword = PasswordEncryptionHelper.HashPassword(request.NewPassword, hashedSalt);

        var changed = await userRepository.ChangeUserPasswordAsync(id, hashedPassword, currentDateTime, credential.Password);

        return new ChangeUserPasswordResponse
        {
            IsSuccess = changed,
            Message = changed ? PasswordChanged : CredentialsChanged
        };
    }
}
