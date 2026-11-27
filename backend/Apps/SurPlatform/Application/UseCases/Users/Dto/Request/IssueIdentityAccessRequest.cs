using Application.Constants;
using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>Повторная выдача временного доступа с конкурентной проверкой карточки</summary>
public sealed record IssueIdentityAccessRequest
{
    /// <summary>Версия прочитанной карточки</summary>
    public Guid Version { get; init; }

    /// <summary>Новый временный пароль для самостоятельной передачи пользователю</summary>
    [Required, StringLength(IdentityAccounts.MaxPasswordLength, MinimumLength = IdentityAccounts.MinPasswordLength)]
    [DataType(DataType.Password)]
    public required string TemporaryPassword { get; init; }

    /// <summary>Исключает исходный пароль из диагностического представления</summary>
    /// <returns>Имя контракта без значений полей</returns>
    public override string ToString()
    {
        return nameof(IssueIdentityAccessRequest);
    }
}
