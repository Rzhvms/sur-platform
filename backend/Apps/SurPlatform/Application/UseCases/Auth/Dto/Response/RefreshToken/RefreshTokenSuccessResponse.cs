namespace Application.UseCases.Auth.Dto.Response.RefreshToken;

/// <summary>
/// Выходная модель успешного обновления токена
/// </summary>
public record RefreshTokenSuccessResponse : RefreshTokenResponse
{
    /// <summary>
    /// Access Token
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Refresh Token
    /// </summary>
    public required string RefreshToken { get; set; }

    /// <summary>
    /// Дата истечения Refresh токена
    /// </summary>
    public DateTime RefreshTokenExpirationDate { get; set; }
}
