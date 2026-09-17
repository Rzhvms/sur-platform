using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IdentityApi.Application.Exceptions;
using IdentityApi.Application.Jwt.Generators.Interfaces;
using IdentityApi.Application.Jwt.Models;
using IdentityApi.Infrastructure.Jwt.Rsa;
using IdentityApi.Infrastructure.Jwt.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityApi.Infrastructure.Jwt;

/// <inheritdoc/>
internal sealed class JwtGenerationService : IJwtGenerationService
{
    /// <summary>
    /// Тип access-токена
    /// </summary>
    private const string AccessTokenType = "at+jwt";
    
    /// <summary>
    /// Допустимые алгоритмы подписи access-токена
    /// </summary>
    private static readonly string[] ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
    
    /// <summary>
    /// Допустимые типы JWT
    /// </summary>
    private static readonly string[] ValidTokenTypes = [AccessTokenType];

    /// <summary>
    /// Системные claims, формируемые сервисом и недоступные для переопределения
    /// </summary>
    private static readonly HashSet<string> ReservedClaimTypes = new(StringComparer.Ordinal)
    {
        JwtRegisteredClaimNames.Sub,
        JwtRegisteredClaimNames.Jti,
        JwtRegisteredClaimNames.Iss,
        JwtRegisteredClaimNames.Aud,
        JwtRegisteredClaimNames.Iat,
        JwtRegisteredClaimNames.Nbf,
        JwtRegisteredClaimNames.Exp
    };

    /// <summary>
    /// Настройки JWT
    /// </summary>
    private readonly JwtSettings _settings;
    
    /// <summary>
    /// Провайдер RSA-ключей для подписи и проверки JWT
    /// </summary>
    private readonly IRsaKeyProvider _keyProvider;
    
    /// <summary>
    /// Провайдер текущего времени
    /// </summary>
    private readonly TimeProvider _timeProvider;

    ///
    public JwtGenerationService(IOptions<JwtSettings> settings, IRsaKeyProvider keyProvider, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keyProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _settings = settings.Value;
        _keyProvider = keyProvider;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public string GenerateAccessToken(JwtUserData user)
    {
        ArgumentNullException.ThrowIfNull(user);
        
        if (user.UserId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор пользователя не может быть пустым", nameof(user));
        }

        var now = _timeProvider.GetUtcNow();
        var claims = BuildAccessTokenClaims(user);
        
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(claims, "Bearer"),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddSeconds(_settings.AccessTokenLifetimeSeconds).UtcDateTime,
            SigningCredentials = new SigningCredentials(_keyProvider.SigningKey, SecurityAlgorithms.RsaSha256),
            TokenType = AccessTokenType
        };

        var handler = CreateTokenHandler();
        
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <inheritdoc/>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(_settings.RefreshTokenLengthBytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    /// <inheritdoc/>
    public TimeSpan GetRefreshTokenLifetime() => TimeSpan.FromMinutes(_settings.RefreshTokenLifetimeMinutes);

    /// <inheritdoc/>
    public Guid GetUserIdFromExpiredAccessToken(string token)
    {
        var principal = ValidateToken(token, validateLifetime: false, out var validatedToken);

        // При отключенной проверке срока действия отдельно проверяем, что токен уже вступил в силу
        if (validatedToken.ValidFrom > _timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new InvalidTokenException("Access-токен еще не вступил в силу");
        }

        var rawUserId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        if (!Guid.TryParse(rawUserId, out var userId))
        {
            throw new InvalidTokenException("Access-токен не содержит корректный идентификатор пользователя");
        }

        return userId;
    }

    /// <inheritdoc/>
    public bool IsAccessTokenValid(string token)
    {
        try
        {
            _ = ValidateToken(token, validateLifetime: true, out _);
            return true;
        }
        catch (InvalidTokenException)
        {
            return false;
        }
    }

    /// <summary>
    /// Формирование набора claims для access-токена
    /// </summary>
    private static IReadOnlyCollection<Claim> BuildAccessTokenClaims(JwtUserData user)
    {
        var additionalClaimCount = user.Claims?.Count ?? 0;
        
        var claims = new List<Claim>(6 + additionalClaimCount)
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        AddClaimIfPresent(claims, JwtRegisteredClaimNames.Email, user.Email);
        AddClaimIfPresent(claims, JwtRegisteredClaimNames.GivenName, user.FirstName);
        AddClaimIfPresent(claims, JwtRegisteredClaimNames.FamilyName, user.LastName);
        AddClaimIfPresent(claims, "patronymic", user.Patronymic);

        if (user.Claims is null) return claims;
        
        foreach (var claim in user.Claims)
        {
            if (ReservedClaimTypes.Contains(claim.Type))
            {
                throw new ArgumentException($"Claim '{claim.Type}' формируется сервисом и не может быть переопределен", nameof(user));
            }

            claims.Add(claim);
        }

        return claims;
    }

    /// <summary>
    /// Валидация подписи, параметров и содержимого access-токена
    /// </summary>
    private ClaimsPrincipal ValidateToken(string token, bool validateLifetime, out SecurityToken validatedToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidTokenException("Access-токен не передан");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = _keyProvider.ValidationKey,
            ValidAlgorithms = ValidAlgorithms,
            ValidTypes = ValidTokenTypes,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            return CreateTokenHandler().ValidateToken(token, parameters, out validatedToken);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException or FormatException)
        {
            throw new InvalidTokenException("Access-токен не прошел проверку", exception);
        }
    }

    /// <summary>
    /// Создание обработчика JWT без автоматического преобразования типов claims
    /// </summary>
    private static JwtSecurityTokenHandler CreateTokenHandler() => new()
    {
        MapInboundClaims = false
    };

    /// <summary>
    /// Добавление claim, если его значение задано
    /// </summary>
    private static void AddClaimIfPresent(ICollection<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(type, value));
        }
    }
}