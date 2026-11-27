using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Helpers.Encryption;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;

namespace Application.UseCases.Users;

/// <inheritdoc />
internal sealed class UserUseCaseManager(IUserRepository userRepository, IRoleRepository roleRepository) : IUserUseCaseManager
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
    public async Task<CurrentUserAccessResponse> GetCurrentAccessAsync(RiskActor actor)
    {
        var access = await roleRepository.GetAccessAsync(actor.UserId);
        if (access is null || access.AccessVersion != actor.AccessVersion)
        {
            throw new IdentityCommandException(401, IdentityErrorCodes.AccessDenied);
        }

        if (access.MustChangePassword)
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }

        return new CurrentUserAccessResponse(actor.UserId, access.RightCodes);
    }

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
