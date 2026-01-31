using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OoplesFinance.StockIndicators.Builder.Enterprise.Security;

namespace OoplesFinance.StockIndicators.Builder.Enterprise.SSO;

/// <summary>
/// OpenID Connect (OIDC) authentication provider.
/// Implements OAuth 2.0 with PKCE for secure authentication.
/// </summary>
public sealed class OIDCProvider : IAuthenticationProvider, IDisposable
{
    private readonly OidcConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly ISecurityAuditLogger _auditLogger;
    private OidcDiscoveryDocument? _discoveryDocument;
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);
    private DateTime _discoveryLastFetched;
    private readonly TimeSpan _discoveryRefreshInterval = TimeSpan.FromHours(24);

    public AuthProviderType ProviderType => AuthProviderType.OIDC;

    /// <summary>
    /// Initializes a new instance of the OIDCProvider.
    /// </summary>
    public OIDCProvider(
        OidcConfig config,
        ITokenStorage tokenStorage,
        ISecurityAuditLogger auditLogger,
        HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Initiates the OIDC authorization flow.
    /// </summary>
    public async Task<AuthorizationRequest> StartAuthorizationAsync(
        string? state = null,
        string? nonce = null,
        CancellationToken ct = default)
    {
        var discovery = await GetDiscoveryDocumentAsync(ct);

        state ??= GenerateSecureRandom(32);
        nonce ??= GenerateSecureRandom(32);

        string? codeVerifier = null;
        string? codeChallenge = null;

        if (_config.UsePkce)
        {
            codeVerifier = GenerateSecureRandom(64);
            codeChallenge = GenerateCodeChallenge(codeVerifier);
        }

        var parameters = new Dictionary<string, string>
        {
            { "client_id", _config.ClientId },
            { "redirect_uri", _config.RedirectUri },
            { "response_type", GetResponseType() },
            { "scope", string.Join(" ", _config.Scopes) },
            { "state", state },
            { "nonce", nonce }
        };

        if (_config.UsePkce && codeChallenge != null)
        {
            parameters["code_challenge"] = codeChallenge;
            parameters["code_challenge_method"] = "S256";
        }

        var queryString = string.Join("&", parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var authorizationUrl = $"{discovery.AuthorizationEndpoint}?{queryString}";

        return new AuthorizationRequest
        {
            AuthorizationUrl = authorizationUrl,
            State = state,
            Nonce = nonce,
            CodeVerifier = codeVerifier
        };
    }

    /// <summary>
    /// Handles the authorization callback and exchanges code for tokens.
    /// </summary>
    public async Task<AuthenticationResult> HandleCallbackAsync(
        string code,
        string state,
        string? codeVerifier = null,
        string? expectedNonce = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        try
        {
            var discovery = await GetDiscoveryDocumentAsync(ct);

            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", _config.ClientId },
                { "code", code },
                { "redirect_uri", _config.RedirectUri }
            };

            // Add client secret if not using PKCE or if secret is configured
            if (!string.IsNullOrEmpty(_config.ClientSecret))
            {
                tokenRequest["client_secret"] = _config.ClientSecret;
            }

            // Add PKCE code verifier
            if (_config.UsePkce && codeVerifier is { Length: > 0 } cv)
            {
                tokenRequest["code_verifier"] = cv;
            }

            var tokenResponse = await ExchangeTokenAsync(discovery.TokenEndpoint, tokenRequest, ct);

            if (!tokenResponse.Success)
            {
                await _auditLogger.LogAsync(new SecurityAuditEvent
                {
                    EventType = SecurityEventType.LoginFailure,
                    IpAddress = ipAddress ?? "unknown",
                    FailureReason = tokenResponse.Error,
                    Success = false
                });

                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.ProviderError,
                    Message = tokenResponse.Error ?? "Token exchange failed"
                });
            }

            // Validate ID token
            var idTokenClaims = ValidateIdToken(tokenResponse.IdToken!, expectedNonce);
            if (idTokenClaims == null)
            {
                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.TokenInvalid,
                    Message = "ID token validation failed"
                });
            }

            // Build user principal from claims
            var user = BuildUserPrincipal(idTokenClaims);

            // Fetch additional user info if available
            if (discovery.UserInfoEndpoint is { Length: > 0 } userInfoEndpoint &&
                tokenResponse.AccessToken is { Length: > 0 } accessToken)
            {
                var userInfo = await FetchUserInfoAsync(userInfoEndpoint, accessToken, ct);
                if (userInfo != null)
                {
                    MergeUserInfo(user, userInfo);
                }
            }

            // Store tokens
            await _tokenStorage.StoreTokensAsync(user.UserId, new StoredTokens
            {
                AccessToken = tokenResponse.AccessToken!,
                RefreshToken = tokenResponse.RefreshToken,
                IdToken = tokenResponse.IdToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            }, ct);

            await _auditLogger.LogAsync(new SecurityAuditEvent
            {
                EventType = SecurityEventType.LoginSuccess,
                UserId = user.UserId,
                Username = user.Username,
                IpAddress = ipAddress ?? "unknown",
                Success = true
            });

            return AuthenticationResult.Succeeded(
                tokenResponse.AccessToken!,
                tokenResponse.RefreshToken ?? string.Empty,
                user,
                DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn));
        }
        catch (Exception ex)
        {
            await _auditLogger.LogAsync(new SecurityAuditEvent
            {
                EventType = SecurityEventType.LoginFailure,
                IpAddress = ipAddress ?? "unknown",
                FailureReason = ex.Message,
                Success = false
            });

            return AuthenticationResult.Failed(new AuthenticationError
            {
                Code = AuthErrorCode.ProviderError,
                Message = "Authentication failed",
                Details = ex.Message
            });
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        // OIDC uses browser-based flow, this method is for token-based auth
        // If we have a stored token, validate it
        var storedTokens = await _tokenStorage.GetTokensAsync(request.Username, ct);
        if (storedTokens == null)
        {
            return AuthenticationResult.Failed(new AuthenticationError
            {
                Code = AuthErrorCode.InvalidCredentials,
                Message = "No stored tokens found. Use StartAuthorizationAsync for OIDC flow."
            });
        }

        var validation = await ValidateTokenAsync(storedTokens.AccessToken, ct);
        if (validation.IsValid && validation.User != null)
        {
            return AuthenticationResult.Succeeded(
                storedTokens.AccessToken,
                storedTokens.RefreshToken ?? string.Empty,
                validation.User,
                storedTokens.ExpiresAt);
        }

        // Try to refresh
        if (storedTokens.RefreshToken is { Length: > 0 } refreshToken)
        {
            return await RefreshTokenAsync(refreshToken, ct);
        }

        return AuthenticationResult.Failed(new AuthenticationError
        {
            Code = AuthErrorCode.TokenExpired,
            Message = "Token expired and no refresh token available"
        });
    }

    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var discovery = await GetDiscoveryDocumentAsync(ct);

            // If introspection endpoint is available, use it
            if (discovery.IntrospectionEndpoint is { Length: > 0 } introspectionEndpoint)
            {
                return await IntrospectTokenAsync(introspectionEndpoint, token, ct);
            }

            // Otherwise, validate JWT locally
            var claims = ValidateJwtToken(token);
            if (claims == null)
            {
                return new TokenValidationResult { IsValid = false, Error = "Invalid token" };
            }

            var user = BuildUserPrincipal(claims);

            return new TokenValidationResult
            {
                IsValid = true,
                User = user,
                ExpiresAt = claims.TryGetValue("exp", out var exp) ?
                    DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp.ToString()!)).UtcDateTime : null,
                Scopes = claims.TryGetValue("scope", out var scope) ?
                    scope.ToString()!.Split(' ').ToList() : []
            };
        }
        catch (Exception ex)
        {
            return new TokenValidationResult { IsValid = false, Error = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var discovery = await GetDiscoveryDocumentAsync(ct);

            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", _config.ClientId },
                { "refresh_token", refreshToken }
            };

            if (!string.IsNullOrEmpty(_config.ClientSecret))
            {
                tokenRequest["client_secret"] = _config.ClientSecret;
            }

            var tokenResponse = await ExchangeTokenAsync(discovery.TokenEndpoint, tokenRequest, ct);

            if (!tokenResponse.Success)
            {
                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.TokenExpired,
                    Message = tokenResponse.Error ?? "Token refresh failed"
                });
            }

            var idTokenClaims = ValidateIdToken(tokenResponse.IdToken!, null);
            if (idTokenClaims == null)
            {
                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.TokenInvalid,
                    Message = "ID token validation failed"
                });
            }

            var user = BuildUserPrincipal(idTokenClaims);

            await _tokenStorage.StoreTokensAsync(user.UserId, new StoredTokens
            {
                AccessToken = tokenResponse.AccessToken!,
                RefreshToken = tokenResponse.RefreshToken ?? refreshToken,
                IdToken = tokenResponse.IdToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            }, ct);

            await _auditLogger.LogAsync(new SecurityAuditEvent
            {
                EventType = SecurityEventType.TokenRefresh,
                UserId = user.UserId,
                Username = user.Username,
                Success = true
            });

            return AuthenticationResult.Succeeded(
                tokenResponse.AccessToken!,
                tokenResponse.RefreshToken ?? refreshToken,
                user,
                DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn));
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failed(new AuthenticationError
            {
                Code = AuthErrorCode.ProviderError,
                Message = "Token refresh failed",
                Details = ex.Message
            });
        }
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string token, CancellationToken ct = default)
    {
        var discovery = await GetDiscoveryDocumentAsync(ct);

        if (string.IsNullOrEmpty(discovery.RevocationEndpoint))
        {
            // Just remove from local storage
            await _tokenStorage.RemoveTokensAsync(token, ct);
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, discovery.RevocationEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "token", token },
                { "client_id", _config.ClientId }
            })
        };

        if (!string.IsNullOrEmpty(_config.ClientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        await _httpClient.SendAsync(request, ct);

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.TokenRevoked,
            Success = true
        });
    }

    /// <summary>
    /// Initiates logout with the OIDC provider.
    /// </summary>
    public async Task<string?> GetLogoutUrlAsync(string? idTokenHint = null, CancellationToken ct = default)
    {
        var discovery = await GetDiscoveryDocumentAsync(ct);

        if (string.IsNullOrEmpty(discovery.EndSessionEndpoint))
        {
            return null;
        }

        var parameters = new Dictionary<string, string>();

        if (idTokenHint is { Length: > 0 } hint)
        {
            parameters["id_token_hint"] = hint;
        }

        if (_config.PostLogoutRedirectUri is { Length: > 0 } redirectUri)
        {
            parameters["post_logout_redirect_uri"] = redirectUri;
        }

        var queryString = string.Join("&", parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{discovery.EndSessionEndpoint}?{queryString}";
    }

    private async Task<OidcDiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken ct)
    {
        await _discoveryLock.WaitAsync(ct);
        try
        {
            if (_discoveryDocument != null &&
                DateTime.UtcNow - _discoveryLastFetched < _discoveryRefreshInterval)
            {
                return _discoveryDocument;
            }

            var discoveryUrl = _config.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
#if NET461
            var response = await _httpClient.GetStringAsync(discoveryUrl);
#else
            var response = await _httpClient.GetStringAsync(discoveryUrl, ct);
#endif

            _discoveryDocument = JsonSerializer.Deserialize<OidcDiscoveryDocument>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to parse discovery document");

            _discoveryLastFetched = DateTime.UtcNow;

            return _discoveryDocument;
        }
        finally
        {
            _discoveryLock.Release();
        }
    }

    private async Task<TokenResponse> ExchangeTokenAsync(
        string tokenEndpoint,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        var response = await _httpClient.SendAsync(request, ct);
#if NET461
        var content = await response.Content.ReadAsStringAsync();
#else
        var content = await response.Content.ReadAsStringAsync(ct);
#endif

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<TokenErrorResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new TokenResponse
            {
                Success = false,
                Error = error?.ErrorDescription ?? error?.Error ?? "Token exchange failed"
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new TokenResponse { Success = false, Error = "Failed to parse token response" };

        tokenResponse.Success = !string.IsNullOrEmpty(tokenResponse.AccessToken);

        return tokenResponse;
    }

    private async Task<TokenValidationResult> IntrospectTokenAsync(
        string introspectionEndpoint,
        string token,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "token", token },
                { "client_id", _config.ClientId }
            })
        };

        if (!string.IsNullOrEmpty(_config.ClientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await _httpClient.SendAsync(request, ct);
#if NET461
        var content = await response.Content.ReadAsStringAsync();
#else
        var content = await response.Content.ReadAsStringAsync(ct);
#endif

        var introspection = JsonSerializer.Deserialize<IntrospectionResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (introspection == null || !introspection.Active)
        {
            return new TokenValidationResult { IsValid = false, Error = "Token is not active" };
        }

        return new TokenValidationResult
        {
            IsValid = true,
            ExpiresAt = introspection.Exp.HasValue ?
                DateTimeOffset.FromUnixTimeSeconds(introspection.Exp.Value).UtcDateTime : null,
            Scopes = introspection.Scope?.Split(' ').ToList() ?? []
        };
    }

    private async Task<Dictionary<string, object>?> FetchUserInfoAsync(
        string userInfoEndpoint,
        string accessToken,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

#if NET461
        var content = await response.Content.ReadAsStringAsync();
#else
        var content = await response.Content.ReadAsStringAsync(ct);
#endif
        return JsonSerializer.Deserialize<Dictionary<string, object>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private Dictionary<string, object>? ValidateIdToken(string idToken, string? expectedNonce)
    {
        var claims = ValidateJwtToken(idToken);
        if (claims == null)
        {
            return null;
        }

        // Validate issuer
        if (claims.TryGetValue("iss", out var iss) &&
            !iss.ToString()!.Equals(_config.Authority.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Validate audience
        if (claims.TryGetValue("aud", out var aud))
        {
            var audiences = aud is JsonElement element && element.ValueKind == JsonValueKind.Array
                ? element.EnumerateArray().Select(e => e.GetString()).ToList()
                : [aud.ToString()];

            if (!audiences.Contains(_config.ClientId))
            {
                return null;
            }
        }

        // Validate nonce if provided
        if (!string.IsNullOrEmpty(expectedNonce) &&
            claims.TryGetValue("nonce", out var nonce) &&
            !nonce.ToString()!.Equals(expectedNonce))
        {
            return null;
        }

        // Validate expiration
        if (claims.TryGetValue("exp", out var exp))
        {
            var expTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp.ToString()!));
            if (expTime < DateTimeOffset.UtcNow)
            {
                return null;
            }
        }

        return claims;
    }

    private static Dictionary<string, object>? ValidateJwtToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            var payload = parts[1];
            // Add padding if needed
            string paddedPayload;
            var remainder = payload.Length % 4;
            if (remainder == 2)
            {
                paddedPayload = payload + "==";
            }
            else if (remainder == 3)
            {
                paddedPayload = payload + "=";
            }
            else
            {
                paddedPayload = payload;
            }

            var payloadBytes = Convert.FromBase64String(paddedPayload.Replace('-', '+').Replace('_', '/'));
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);

            return JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private UserPrincipal BuildUserPrincipal(Dictionary<string, object> claims)
    {
        var user = new UserPrincipal();

        foreach (var mapping in _config.ClaimMapping)
        {
            if (claims.TryGetValue(mapping.Key, out var value))
            {
                var strValue = value is JsonElement element ? element.GetString() : value.ToString();

                switch (mapping.Value.ToLowerInvariant())
                {
                    case "userid":
                    case "sub":
                        user.UserId = strValue ?? string.Empty;
                        break;
                    case "email":
                        user.Email = strValue ?? string.Empty;
                        user.Username = strValue ?? string.Empty;
                        break;
                    case "displayname":
                    case "name":
                        user.DisplayName = strValue;
                        break;
                    case "groups":
                        if (value is JsonElement groupsElement && groupsElement.ValueKind == JsonValueKind.Array)
                        {
                            user.Groups = groupsElement.EnumerateArray()
                                .Select(g => g.GetString() ?? string.Empty)
                                .Where(g => !string.IsNullOrEmpty(g))
                                .ToList();
                        }
                        break;
                }
            }
        }

        // Default user ID to sub claim if not mapped
        if (string.IsNullOrEmpty(user.UserId) && claims.TryGetValue("sub", out var sub))
        {
            user.UserId = sub is JsonElement subElement ? subElement.GetString() ?? string.Empty : sub.ToString() ?? string.Empty;
        }

        return user;
    }

    private static void MergeUserInfo(UserPrincipal user, Dictionary<string, object> userInfo)
    {
        if (string.IsNullOrEmpty(user.Email) && userInfo.TryGetValue("email", out var email))
        {
            user.Email = email is JsonElement e ? e.GetString() ?? string.Empty : email.ToString() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(user.DisplayName) && userInfo.TryGetValue("name", out var name))
        {
            user.DisplayName = name is JsonElement n ? n.GetString() : name.ToString();
        }
    }

    private string GetResponseType()
    {
        return _config.ResponseType switch
        {
            OidcResponseType.Code => "code",
            OidcResponseType.Token => "token",
            OidcResponseType.IdToken => "id_token",
            OidcResponseType.CodeIdToken => "code id_token",
            _ => "code"
        };
    }

    private static string GenerateSecureRandom(int length)
    {
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public void Dispose()
    {
        _discoveryLock.Dispose();
    }
}

/// <summary>
/// OIDC discovery document.
/// </summary>
public sealed class OidcDiscoveryDocument
{
    public string Issuer { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string? UserInfoEndpoint { get; set; }
    public string? RevocationEndpoint { get; set; }
    public string? IntrospectionEndpoint { get; set; }
    public string? EndSessionEndpoint { get; set; }
    public string JwksUri { get; set; } = string.Empty;
    public IReadOnlyList<string> ScopesSupported { get; set; } = [];
    public IReadOnlyList<string> ResponseTypesSupported { get; set; } = [];
    public IReadOnlyList<string> ClaimsSupported { get; set; } = [];
}

/// <summary>
/// Authorization request result.
/// </summary>
public sealed class AuthorizationRequest
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string? CodeVerifier { get; set; }
}

/// <summary>
/// Token response from OIDC provider.
/// </summary>
public sealed class TokenResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Token error response.
/// </summary>
public sealed class TokenErrorResponse
{
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// Introspection response.
/// </summary>
public sealed class IntrospectionResponse
{
    public bool Active { get; set; }
    public string? Scope { get; set; }
    public string? ClientId { get; set; }
    public string? Username { get; set; }
    public long? Exp { get; set; }
}

/// <summary>
/// Interface for token storage.
/// </summary>
public interface ITokenStorage
{
    Task StoreTokensAsync(string userId, StoredTokens tokens, CancellationToken ct = default);
    Task<StoredTokens?> GetTokensAsync(string userId, CancellationToken ct = default);
    Task RemoveTokensAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Stored tokens.
/// </summary>
public sealed class StoredTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Interface for security audit logging.
/// </summary>
public interface ISecurityAuditLogger
{
    Task LogAsync(SecurityAuditEvent auditEvent, CancellationToken ct = default);
}
