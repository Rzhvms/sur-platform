using System.Buffers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Settings;
using Microsoft.IdentityModel.Tokens;

namespace Application.Helpers.Jwt;

/// <summary>
/// Сервис генерации и проверки JWT
/// </summary>
public static class JwtGenerationHelper
{
    private const string JwtAuthenticationType = "Bearer";
    private const int StackTokenByteLength = 256;
    private const int RefreshHashByteLength = 32;

    /// <summary>
    /// Генерирует access token
    /// </summary>
    public static string GenerateAccessToken(AccessTokenClaims claims, DateTime sessionExpiresAt,
        JwtSettings settings, RsaSecurityKey signingKey, DateTime currentDateTime)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(signingKey);

        if (currentDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Текущее время должно быть задано в UTC", nameof(currentDateTime));
        }

        if (claims.UserId == Guid.Empty || claims.SessionId == Guid.Empty)
        {
            throw new ArgumentException("Идентификаторы пользователя и сессии обязательны", nameof(claims));
        }

        ArgumentNullException.ThrowIfNull(claims.RightIdList);
        var expiresAt = currentDateTime.AddSeconds(settings.AccessTokenSettings.LifeTimeInSeconds);

        if (sessionExpiresAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Время окончания сессии должно быть задано в UTC", nameof(sessionExpiresAt));
        }

        if (sessionExpiresAt < expiresAt)
        {
            expiresAt = sessionExpiresAt;
        }

        var expiresDateTime = DateTimeOffset.FromUnixTimeSeconds(new DateTimeOffset(expiresAt).ToUnixTimeSeconds());

        if (expiresDateTime <= currentDateTime)
        {
            throw new InvalidTokenException("Срок действия сессии истек");
        }

        return CreateToken(
            issuer: settings.AccessTokenSettings.Issuer,
            audience: settings.AccessTokenSettings.Audience,
            claims: BuildAccessTokenClaims(claims),
            notBefore: currentDateTime,
            expires: expiresDateTime.UtcDateTime,
            signingKey: signingKey);
    }

    /// <summary>
    /// Генерирует refresh token
    /// </summary>
    public static string GenerateRefreshToken(JwtSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var buffer = new byte[settings.RefreshTokenSettings.Length];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }

    /// <summary>
    /// Вычисляет хеш случайного refresh-токена для хранения в базе
    /// </summary>
    public static string HashRefreshToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var byteCount = Encoding.UTF8.GetByteCount(token);
        byte[]? rentedBuffer = null;
        
        var tokenBytes = byteCount <= StackTokenByteLength
            ? stackalloc byte[StackTokenByteLength]
            : rentedBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
        
        Span<byte> hash = stackalloc byte[RefreshHashByteLength];

        try
        {
            var written = Encoding.UTF8.GetBytes(token.AsSpan(), tokenBytes);
            SHA256.HashData(tokenBytes[..written], hash);

            return Convert.ToHexString(hash);
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Возвращает время жизни refresh token в минутах
    /// </summary>
    public static int GetRefreshTokenLifetimeInMinutes(JwtSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.RefreshTokenSettings.LifeTimeInMinutes;
    }

    /// <summary>
    /// Извлекает идентификатор пользователя из токена
    /// </summary>
    public static Guid GetUserIdFromToken(string token, JwtSettings settings, RsaSecurityKey validationKey)
    {
        var principal = ValidateToken(token, validateLifetime: false, settings, validationKey);

        var rawUserId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var userId))
        {
            throw new InvalidTokenException("Токен не содержит валидный идентификатор пользователя");
        }

        return userId;
    }

    /// <summary>
    /// Проверяет валидность токена
    /// </summary>
    public static bool IsTokenValid(string token, bool validateLifeTime, JwtSettings settings, RsaSecurityKey validationKey)
    {
        try
        {
            _ = ValidateToken(token, validateLifetime: validateLifeTime, settings, validationKey);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    #region Private methods
    
    /// <summary>
    /// Формирует идентификаторы пользователя, сессии и уникальных прав
    /// </summary>
    private static IReadOnlyCollection<Claim> BuildAccessTokenClaims(AccessTokenClaims data)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, data.UserId.ToString("D")),
            new(AccessTokenClaimTypes.SessionId, data.SessionId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var uniqueRightIdSet = new HashSet<Guid>();
        foreach (var rightId in data.RightIdList)
        {
            if (rightId == Guid.Empty)
            {
                throw new ArgumentException("Идентификатор права не может быть пустым", nameof(data));
            }

            if (uniqueRightIdSet.Add(rightId))
            {
                claims.Add(new Claim(AccessTokenClaimTypes.Right, rightId.ToString("D")));
            }
        }

        return claims;
    }

    /// <summary>
    /// Формирование токена
    /// </summary>
    private static string CreateToken(string issuer, string audience, IEnumerable<Claim> claims, 
        DateTime notBefore, DateTime expires, RsaSecurityKey signingKey)
    {
        var identity = new ClaimsIdentity(claims, JwtAuthenticationType);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        var token = handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: audience,
            subject: identity,
            notBefore: notBefore,
            expires: expires,
            issuedAt: notBefore,
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    /// <summary>
    /// Валидация токена
    /// </summary>
    private static ClaimsPrincipal ValidateToken(string token, bool validateLifetime, JwtSettings settings, RsaSecurityKey validationKey)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(validationKey);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidTokenException("Token is empty");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = validateLifetime,
            ValidIssuer = settings.AccessTokenSettings.Issuer,
            ValidAudience = settings.AccessTokenSettings.Audience,
            IssuerSigningKey = validationKey,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        try
        {
            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            throw new InvalidTokenException("Token is invalid", ex);
        }
    }
    
    #endregion
}