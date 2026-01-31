namespace OoplesFinance.StockIndicators.Builder.Enterprise.Security;

/// <summary>
/// Core authentication types and interfaces for enterprise security.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Authenticates a user with the given credentials.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Validates an existing token.
    /// </summary>
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an authentication token.
    /// </summary>
    Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes a token.
    /// </summary>
    Task RevokeTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Gets the provider type.
    /// </summary>
    AuthProviderType ProviderType { get; }
}

/// <summary>
/// Authentication provider types.
/// </summary>
public enum AuthProviderType
{
    Local,
    SAML,
    OIDC,
    OAuth2,
    LDAP,
    ApiKey
}

/// <summary>
/// Authentication request.
/// </summary>
public sealed class AuthenticationRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? MfaCode { get; set; }
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, string> AdditionalClaims { get; set; } = [];
}

/// <summary>
/// Authentication result.
/// </summary>
public sealed class AuthenticationResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserPrincipal? User { get; set; }
    public AuthenticationError? Error { get; set; }
    public bool RequiresMfa { get; set; }
    public MfaChallenge? MfaChallenge { get; set; }

    public static AuthenticationResult Succeeded(string accessToken, string refreshToken, UserPrincipal user, DateTime expiresAt) =>
        new()
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = user,
            ExpiresAt = expiresAt
        };

    public static AuthenticationResult Failed(AuthenticationError error) =>
        new() { Success = false, Error = error };

    public static AuthenticationResult MfaRequired(MfaChallenge challenge) =>
        new() { Success = false, RequiresMfa = true, MfaChallenge = challenge };
}

/// <summary>
/// Authentication error.
/// </summary>
public sealed class AuthenticationError
{
    public AuthErrorCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Authentication error codes.
/// </summary>
public enum AuthErrorCode
{
    InvalidCredentials,
    AccountLocked,
    AccountDisabled,
    PasswordExpired,
    MfaRequired,
    MfaFailed,
    TokenExpired,
    TokenInvalid,
    SessionExpired,
    TenantNotFound,
    ProviderError,
    RateLimited,
    IpBlocked,
    DeviceNotTrusted
}

/// <summary>
/// MFA challenge.
/// </summary>
public sealed class MfaChallenge
{
    public string ChallengeId { get; set; } = string.Empty;
    public MfaMethod Method { get; set; }
    public string? Hint { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// MFA methods.
/// </summary>
public enum MfaMethod
{
    TOTP,
    SMS,
    Email,
    Push,
    SecurityKey,
    Backup
}

/// <summary>
/// Token validation result.
/// </summary>
public sealed class TokenValidationResult
{
    public bool IsValid { get; set; }
    public UserPrincipal? User { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<string> Scopes { get; set; } = [];
}

/// <summary>
/// User principal representing an authenticated user.
/// </summary>
public sealed class UserPrincipal
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public IReadOnlyList<string> Groups { get; set; } = [];
    public Dictionary<string, string> Claims { get; set; } = [];
    public DateTime? LastLoginAt { get; set; }
    public bool IsMfaEnabled { get; set; }
    public bool IsServiceAccount { get; set; }

    /// <summary>
    /// Checks if user has a specific role.
    /// </summary>
    public bool HasRole(string role) =>
        Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if user has a specific permission.
    /// </summary>
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
        Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if user has any of the specified permissions.
    /// </summary>
    public bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(HasPermission);

    /// <summary>
    /// Checks if user has all of the specified permissions.
    /// </summary>
    public bool HasAllPermissions(params string[] permissions) =>
        permissions.All(HasPermission);
}

/// <summary>
/// Session information.
/// </summary>
public sealed class UserSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public SessionStatus Status { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsActive => Status == SessionStatus.Active && !IsExpired;
}

/// <summary>
/// Session status.
/// </summary>
public enum SessionStatus
{
    Active,
    Expired,
    Revoked,
    LoggedOut
}

/// <summary>
/// API key for service authentication.
/// </summary>
public sealed class ApiKey
{
    public string KeyId { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public IReadOnlyList<string> Scopes { get; set; } = [];
    public IReadOnlyList<string> AllowedIps { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public long UsageCount { get; set; }
    public long? RateLimit { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsValid => IsActive && !IsExpired;
}

/// <summary>
/// Tenant configuration for multi-tenancy.
/// </summary>
public sealed class TenantConfiguration
{
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public TenantStatus Status { get; set; }
    public AuthenticationConfig Authentication { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
}

/// <summary>
/// Tenant status.
/// </summary>
public enum TenantStatus
{
    Active,
    Trial,
    Suspended,
    Cancelled
}

/// <summary>
/// Authentication configuration for a tenant.
/// </summary>
public sealed class AuthenticationConfig
{
    public bool AllowLocalAuth { get; set; } = true;
    public bool RequireMfa { get; set; }
    public bool AllowRememberMe { get; set; } = true;
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    public int MaxFailedAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    public SamlConfig? Saml { get; set; }
    public OidcConfig? Oidc { get; set; }
    public LdapConfig? Ldap { get; set; }
}

/// <summary>
/// SAML configuration.
/// </summary>
public sealed class SamlConfig
{
    public bool Enabled { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string MetadataUrl { get; set; } = string.Empty;
    public string SingleSignOnUrl { get; set; } = string.Empty;
    public string SingleLogoutUrl { get; set; } = string.Empty;
    public string Certificate { get; set; } = string.Empty;
    public string? SigningCertificate { get; set; }
    public bool SignRequests { get; set; } = true;
    public bool WantAssertionsSigned { get; set; } = true;
    public SamlNameIdFormat NameIdFormat { get; set; } = SamlNameIdFormat.EmailAddress;
    public Dictionary<string, string> AttributeMapping { get; set; } = new()
    {
        { "email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" },
        { "firstName", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname" },
        { "lastName", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname" },
        { "groups", "http://schemas.xmlsoap.org/claims/Group" }
    };
}

/// <summary>
/// SAML NameID formats.
/// </summary>
public enum SamlNameIdFormat
{
    EmailAddress,
    Persistent,
    Transient,
    Unspecified
}

/// <summary>
/// OIDC configuration.
/// </summary>
public sealed class OidcConfig
{
    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string PostLogoutRedirectUri { get; set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; set; } = ["openid", "profile", "email"];
    public bool UsePkce { get; set; } = true;
    public OidcResponseType ResponseType { get; set; } = OidcResponseType.Code;
    public Dictionary<string, string> ClaimMapping { get; set; } = new()
    {
        { "sub", "userId" },
        { "email", "email" },
        { "name", "displayName" },
        { "groups", "groups" }
    };
}

/// <summary>
/// OIDC response types.
/// </summary>
public enum OidcResponseType
{
    Code,
    Token,
    IdToken,
    CodeIdToken
}

/// <summary>
/// LDAP configuration.
/// </summary>
public sealed class LdapConfig
{
    public bool Enabled { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string BaseDn { get; set; } = string.Empty;
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
    public string UserSearchFilter { get; set; } = "(sAMAccountName={0})";
    public string GroupSearchFilter { get; set; } = "(member={0})";
    public Dictionary<string, string> AttributeMapping { get; set; } = new()
    {
        { "email", "mail" },
        { "displayName", "displayName" },
        { "firstName", "givenName" },
        { "lastName", "sn" }
    };
}

/// <summary>
/// Security configuration for a tenant.
/// </summary>
public sealed class SecurityConfig
{
    public PasswordPolicy PasswordPolicy { get; set; } = new();
    public IReadOnlyList<string> AllowedIpRanges { get; set; } = [];
    public IReadOnlyList<string> BlockedIpRanges { get; set; } = [];
    public bool EnforceIpWhitelist { get; set; }
    public int MaxConcurrentSessions { get; set; } = 5;
    public bool AllowApiKeys { get; set; } = true;
    public int MaxApiKeysPerUser { get; set; } = 10;
}

/// <summary>
/// Password policy.
/// </summary>
public sealed class PasswordPolicy
{
    public int MinLength { get; set; } = 12;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;
    public int PasswordHistoryCount { get; set; } = 5;
    public int MaxAgeDays { get; set; } = 90;
    public int MinAgeDays { get; set; } = 1;

    /// <summary>
    /// Validates a password against this policy.
    /// </summary>
    public PasswordValidationResult Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required");
            return new PasswordValidationResult { IsValid = false, Errors = errors };
        }

        if (password.Length < MinLength)
            errors.Add($"Password must be at least {MinLength} characters");

        if (password.Length > MaxLength)
            errors.Add($"Password must not exceed {MaxLength} characters");

        if (RequireUppercase && !password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter");

        if (RequireLowercase && !password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter");

        if (RequireDigit && !password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit");

        if (RequireSpecialChar && !password.Any(c => !char.IsLetterOrDigit(c)))
            errors.Add("Password must contain at least one special character");

        return new PasswordValidationResult { IsValid = errors.Count == 0, Errors = errors };
    }
}

/// <summary>
/// Password validation result.
/// </summary>
public sealed class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
}

/// <summary>
/// Audit event for security logging.
/// </summary>
public sealed class SecurityAuditEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SecurityEventType EventType { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? Action { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>
/// Security event types.
/// </summary>
public enum SecurityEventType
{
    LoginSuccess,
    LoginFailure,
    Logout,
    TokenRefresh,
    TokenRevoked,
    PasswordChanged,
    PasswordReset,
    MfaEnabled,
    MfaDisabled,
    MfaSuccess,
    MfaFailure,
    ApiKeyCreated,
    ApiKeyRevoked,
    SessionCreated,
    SessionRevoked,
    PermissionDenied,
    AccountLocked,
    AccountUnlocked,
    SuspiciousActivity
}
