using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Services.Jwt;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Infrastructure.Services.Jwt.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Jwt;

/// <summary>
/// Сервис генерации и проверки JWT
/// </summary>
public sealed class JwtGenerationService : IJwtGenerationService
{
    private const string JwtAuthenticationType = "Bearer";

    private readonly JwtSettings _settings;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _validationKey;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Инициализирует сервис генерации и проверки JWT
    /// </summary>
    public JwtGenerationService(IOptions<JwtSettings> settings, IRsaKeyProvider keyProvider, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keyProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _signingKey = keyProvider.SigningKey;
        _validationKey = keyProvider.ValidationKey;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public string GenerateAccessToken(AccessTokenClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.UserId == Guid.Empty || claims.SessionId == Guid.Empty)
        {
            throw new ArgumentException("Идентификаторы пользователя и сессии обязательны", nameof(claims));
        }

        ArgumentNullException.ThrowIfNull(claims.RightIdList);
        var now = _timeProvider.GetUtcNow();

        return CreateToken(
            issuer: _settings.AccessTokenSettings.Issuer,
            audience: _settings.AccessTokenSettings.Audience,
            claims: BuildAccessTokenClaims(claims),
            notBefore: now.UtcDateTime,
            expires: now.AddSeconds(_settings.AccessTokenSettings.LifeTimeInSeconds).UtcDateTime,
            signingKey: _signingKey);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var buffer = new byte[_settings.RefreshTokenSettings.Length];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }

    /// <inheritdoc />
    public int GetRefreshTokenLifetimeInMinutes()
        => _settings.RefreshTokenSettings.LifeTimeInMinutes;

    /// <inheritdoc />
    public Guid GetUserIdFromToken(string token)
    {
        var principal = ValidateToken(token, validateLifetime: false);

        var rawUserId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var userId))
        {
            throw new InvalidTokenException("Token does not contain a valid UserId.");
        }

        return userId;
    }

    /// <inheritdoc />
    public bool IsTokenValid(string token, bool validateLifeTime)
    {
        try
        {
            _ = ValidateToken(token, validateLifetime: validateLifeTime);
            return true;
        }
        catch
        {
            return false;
        }
    }

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

        var uniqueRightIds = new HashSet<Guid>();
        foreach (var rightId in data.RightIdList)
        {
            if (rightId == Guid.Empty)
            {
                throw new ArgumentException("Идентификатор права не может быть пустым", nameof(data));
            }

            if (uniqueRightIds.Add(rightId))
            {
                claims.Add(new Claim(AccessTokenClaimTypes.Right, rightId.ToString("D")));
            }
        }

        return claims;
    }

    /// <summary>
    /// Формирование токена
    /// </summary>
    private string CreateToken(string issuer, string audience, IEnumerable<Claim> claims, 
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
    private ClaimsPrincipal ValidateToken(string token, bool validateLifetime)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidTokenException("Token is empty.");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = validateLifetime,
            ValidIssuer = _settings.AccessTokenSettings.Issuer,
            ValidAudience = _settings.AccessTokenSettings.Audience,
            IssuerSigningKey = _validationKey,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        try
        {
            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            throw new InvalidTokenException("Token is invalid.", ex);
        }
    }
}