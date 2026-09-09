using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace OoplesFinance.StockIndicators.Builder.Cloud;

/// <summary>
/// API Gateway for rate limiting, authentication, and request routing.
/// </summary>
public sealed class ApiGateway
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _rateLimiters = new();
    private readonly ConcurrentDictionary<string, ApiKey> _apiKeys = new();
    private readonly ConcurrentDictionary<string, ApiEndpoint> _endpoints = new();
    private readonly ApiGatewayOptions _options;
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    /// <summary>
    /// Creates a new API Gateway.
    /// </summary>
    public ApiGateway(ApiGatewayOptions? options = null)
    {
        _options = options ?? new ApiGatewayOptions();
        RegisterDefaultEndpoints();
    }

    /// <summary>
    /// Processes an incoming API request.
    /// </summary>
    public async Task<ApiResponse> ProcessRequestAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Cleanup old rate limit buckets periodically
            CleanupIfNeeded();

            // Validate API key
            var authResult = ValidateApiKey(request.ApiKey);
            if (!authResult.IsValid)
            {
                return CreateErrorResponse(401, authResult.ErrorMessage ?? "Unauthorized", request, startTime);
            }

            var apiKey = authResult.ApiKey;
            if (apiKey is null)
            {
                return CreateErrorResponse(401, "Invalid API key", request, startTime);
            }

            // Check if API key is active
            if (!apiKey.IsActive)
            {
                return CreateErrorResponse(403, "API key is deactivated", request, startTime);
            }

            // Check rate limits
            var rateLimitResult = CheckRateLimit(apiKey);
            if (!rateLimitResult.IsAllowed)
            {
                return CreateRateLimitResponse(rateLimitResult, request, startTime);
            }

            // Validate endpoint
            if (!_endpoints.TryGetValue(request.Endpoint, out var endpoint))
            {
                return CreateErrorResponse(404, $"Endpoint not found: {request.Endpoint}", request, startTime);
            }

            // Check method
            if (!endpoint.AllowedMethods.Contains(request.Method))
            {
                return CreateErrorResponse(405, $"Method {request.Method} not allowed for {request.Endpoint}", request, startTime);
            }

            // Check permissions
            if (!HasPermission(apiKey, endpoint.RequiredPermission))
            {
                return CreateErrorResponse(403, $"Insufficient permissions for {request.Endpoint}", request, startTime);
            }

            // Load tenant context
            var tenant = GetTenantForApiKey(apiKey);
            if (tenant is null)
            {
                return CreateErrorResponse(403, "Tenant not found", request, startTime);
            }

            // Execute the request
            using (TenantContext.BeginScope(tenant))
            {
                try
                {
                    // Validate tenant operation
                    var operation = endpoint.OperationType;
                    if (operation.HasValue)
                    {
                        tenant.ValidateOperation(operation.Value);
                    }

                    // Execute handler
                    var result = await endpoint.Handler(request, cancellationToken).ConfigureAwait(false);

                    // Update usage
                    tenant.IncrementUsage(ResourceType.ApiCalls);
                    apiKey.IncrementUsage();

                    return CreateSuccessResponse(result, request, startTime, rateLimitResult);
                }
                catch (TenantOperationNotAllowedException ex)
                {
                    return CreateErrorResponse(403, ex.Message, request, startTime);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return CreateErrorResponse(408, "Request timeout", request, startTime);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(500, _options.ExposeDetailedErrors ? ex.Message : "Internal server error", request, startTime);
        }
    }

    /// <summary>
    /// Registers an API endpoint.
    /// </summary>
    public void RegisterEndpoint(ApiEndpoint endpoint)
    {
        _endpoints[endpoint.Path] = endpoint;
    }

    /// <summary>
    /// Generates a new API key for a tenant.
    /// </summary>
    public ApiKey GenerateApiKey(string tenantId, string name, IReadOnlyList<ApiPermission> permissions)
    {
        var keyValue = GenerateSecureKey();
        var hashedKey = HashApiKey(keyValue);

        var apiKey = new ApiKey
        {
            KeyId = Guid.NewGuid().ToString(),
            HashedKey = hashedKey,
            PlainTextKey = keyValue, // Only returned once at creation
            TenantId = tenantId,
            Name = name,
            Permissions = permissions.ToList(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            RateLimitPerMinute = GetDefaultRateLimit(tenantId)
        };

        _apiKeys[hashedKey] = apiKey;
        return apiKey;
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    public bool RevokeApiKey(string keyId)
    {
        var key = _apiKeys.Values.FirstOrDefault(k => k.KeyId == keyId);
        if (key is null) return false;

        key.IsActive = false;
        key.RevokedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Gets API key usage statistics.
    /// </summary>
    public ApiKeyUsageStats GetUsageStats(string keyId)
    {
        var key = _apiKeys.Values.FirstOrDefault(k => k.KeyId == keyId);
        if (key is null)
        {
            return new ApiKeyUsageStats { KeyId = keyId };
        }

        return new ApiKeyUsageStats
        {
            KeyId = keyId,
            TotalRequests = key.TotalRequests,
            RequestsToday = key.RequestsToday,
            LastUsedAt = key.LastUsedAt,
            CreatedAt = key.CreatedAt
        };
    }

    private void RegisterDefaultEndpoints()
    {
        RegisterEndpoint(new ApiEndpoint
        {
            Path = "/api/v1/health",
            AllowedMethods = new[] { "GET" },
            RequiredPermission = ApiPermission.Read,
            Handler = (req, ct) => Task.FromResult<object>(new { status = "healthy", timestamp = DateTime.UtcNow })
        });

        RegisterEndpoint(new ApiEndpoint
        {
            Path = "/api/v1/account",
            AllowedMethods = new[] { "GET" },
            RequiredPermission = ApiPermission.Read,
            Handler = (req, ct) => Task.FromResult<object>(new { message = "Account endpoint" })
        });

        RegisterEndpoint(new ApiEndpoint
        {
            Path = "/api/v1/positions",
            AllowedMethods = new[] { "GET" },
            RequiredPermission = ApiPermission.Read,
            Handler = (req, ct) => Task.FromResult<object>(new { message = "Positions endpoint" })
        });

        RegisterEndpoint(new ApiEndpoint
        {
            Path = "/api/v1/orders",
            AllowedMethods = new[] { "GET", "POST" },
            RequiredPermission = ApiPermission.Trade,
            OperationType = OperationType.LiveTrade,
            Handler = (req, ct) => Task.FromResult<object>(new { message = "Orders endpoint" })
        });

        RegisterEndpoint(new ApiEndpoint
        {
            Path = "/api/v1/strategies",
            AllowedMethods = new[] { "GET", "POST", "PUT", "DELETE" },
            RequiredPermission = ApiPermission.Write,
            Handler = (req, ct) => Task.FromResult<object>(new { message = "Strategies endpoint" })
        });

        RegisterEndpoint(new ApiEndpoint
        {
            Path = "/api/v1/backtest",
            AllowedMethods = new[] { "POST" },
            RequiredPermission = ApiPermission.Write,
            Handler = (req, ct) => Task.FromResult<object>(new { message = "Backtest endpoint" })
        });
    }

    private AuthenticationResult ValidateApiKey(string? apiKey)
    {
        if (apiKey is null || string.IsNullOrEmpty(apiKey))
        {
            return new AuthenticationResult { IsValid = false, ErrorMessage = "API key is required" };
        }

        var hashedKey = HashApiKey(apiKey);

        if (!_apiKeys.TryGetValue(hashedKey, out var key))
        {
            return new AuthenticationResult { IsValid = false, ErrorMessage = "Invalid API key" };
        }

        return new AuthenticationResult { IsValid = true, ApiKey = key };
    }

    private RateLimitResult CheckRateLimit(ApiKey apiKey)
    {
        var bucket = _rateLimiters.GetOrAdd(apiKey.KeyId, _ => new RateLimitBucket(apiKey.RateLimitPerMinute));

        return bucket.TryConsume();
    }

    private bool HasPermission(ApiKey apiKey, ApiPermission required)
    {
        if (apiKey.Permissions.Contains(ApiPermission.Admin)) return true;
        if (required == ApiPermission.Read && apiKey.Permissions.Contains(ApiPermission.Read)) return true;
        if (required == ApiPermission.Write && (apiKey.Permissions.Contains(ApiPermission.Write) || apiKey.Permissions.Contains(ApiPermission.Admin))) return true;
        if (required == ApiPermission.Trade && apiKey.Permissions.Contains(ApiPermission.Trade)) return true;

        return apiKey.Permissions.Contains(required);
    }

    private TenantContext? GetTenantForApiKey(ApiKey apiKey)
    {
        // In a real implementation, this would look up the tenant from a database
        return new TenantContext(apiKey.TenantId, $"Tenant-{apiKey.TenantId}", SubscriptionTier.Professional);
    }

    private int GetDefaultRateLimit(string tenantId)
    {
        // In a real implementation, this would look up the tenant's tier
        return 1000;
    }

    private void CleanupIfNeeded()
    {
        if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5) return;

        lock (_cleanupLock)
        {
            if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5) return;

            // Remove expired rate limit buckets
            var expiredKeys = _rateLimiters
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _rateLimiters.TryRemove(key, out _);
            }

            _lastCleanup = DateTime.UtcNow;
        }
    }

    private static string GenerateSecureKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(bytes);
    }

    private static ApiResponse CreateErrorResponse(int statusCode, string message, ApiRequest request, DateTime startTime)
    {
        return new ApiResponse
        {
            StatusCode = statusCode,
            Success = false,
            ErrorMessage = message,
            RequestId = request.RequestId,
            LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds
        };
    }

    private static ApiResponse CreateRateLimitResponse(RateLimitResult result, ApiRequest request, DateTime startTime)
    {
        return new ApiResponse
        {
            StatusCode = 429,
            Success = false,
            ErrorMessage = "Rate limit exceeded",
            RequestId = request.RequestId,
            LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
            Headers = new Dictionary<string, string>
            {
                ["X-RateLimit-Limit"] = result.Limit.ToString(),
                ["X-RateLimit-Remaining"] = result.Remaining.ToString(),
                ["X-RateLimit-Reset"] = result.ResetAt.ToString("O"),
                ["Retry-After"] = ((int)(result.ResetAt - DateTime.UtcNow).TotalSeconds).ToString()
            }
        };
    }

    private static ApiResponse CreateSuccessResponse(object data, ApiRequest request, DateTime startTime, RateLimitResult rateLimit)
    {
        return new ApiResponse
        {
            StatusCode = 200,
            Success = true,
            Data = data,
            RequestId = request.RequestId,
            LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
            Headers = new Dictionary<string, string>
            {
                ["X-RateLimit-Limit"] = rateLimit.Limit.ToString(),
                ["X-RateLimit-Remaining"] = rateLimit.Remaining.ToString(),
                ["X-RateLimit-Reset"] = rateLimit.ResetAt.ToString("O")
            }
        };
    }
}

/// <summary>
/// API Gateway configuration options.
/// </summary>
public sealed class ApiGatewayOptions
{
    /// <summary>Gets or sets whether to expose detailed error messages.</summary>
    public bool ExposeDetailedErrors { get; set; } = false;

    /// <summary>Gets or sets the default rate limit per minute.</summary>
    public int DefaultRateLimitPerMinute { get; set; } = 60;

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets whether to enable request logging.</summary>
    public bool EnableRequestLogging { get; set; } = true;
}

/// <summary>
/// API request.
/// </summary>
public sealed class ApiRequest
{
    /// <summary>Gets the request ID.</summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Gets the API key.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Gets the endpoint path.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Gets the HTTP method.</summary>
    public string Method { get; init; } = "GET";

    /// <summary>Gets the request headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the query parameters.</summary>
    public IReadOnlyDictionary<string, string> QueryParams { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the request body.</summary>
    public object? Body { get; init; }

    /// <summary>Gets the client IP address.</summary>
    public string? ClientIp { get; init; }

    /// <summary>Gets the request timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// API response.
/// </summary>
public sealed class ApiResponse
{
    /// <summary>Gets the HTTP status code.</summary>
    public int StatusCode { get; init; }

    /// <summary>Gets whether the request was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the response data.</summary>
    public object? Data { get; init; }

    /// <summary>Gets the error message if any.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the request ID.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets the response latency in milliseconds.</summary>
    public double LatencyMs { get; init; }

    /// <summary>Gets the response headers.</summary>
    public Dictionary<string, string> Headers { get; init; } = new();
}

/// <summary>
/// API endpoint definition.
/// </summary>
public sealed class ApiEndpoint
{
    /// <summary>Gets the endpoint path.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Gets the allowed HTTP methods.</summary>
    public IReadOnlyList<string> AllowedMethods { get; init; } = Array.Empty<string>();

    /// <summary>Gets the required permission.</summary>
    public ApiPermission RequiredPermission { get; init; }

    /// <summary>Gets the operation type for tenant validation.</summary>
    public OperationType? OperationType { get; init; }

    /// <summary>Gets the request handler.</summary>
    public Func<ApiRequest, CancellationToken, Task<object>> Handler { get; init; } =
        (_, _) => Task.FromResult<object>(new { });
}

/// <summary>
/// API permissions.
/// </summary>
public enum ApiPermission
{
    /// <summary>Read-only access.</summary>
    Read,

    /// <summary>Write access (create/update/delete).</summary>
    Write,

    /// <summary>Trading access.</summary>
    Trade,

    /// <summary>Admin access (all permissions).</summary>
    Admin
}

/// <summary>
/// API key.
/// </summary>
public sealed class ApiKey
{
    /// <summary>Gets the key ID.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>Gets the hashed key value.</summary>
    public string HashedKey { get; init; } = string.Empty;

    /// <summary>Gets the plain text key (only available at creation).</summary>
    public string? PlainTextKey { get; internal set; }

    /// <summary>Gets the tenant ID.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Gets the key name/description.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the permissions.</summary>
    public List<ApiPermission> Permissions { get; init; } = new();

    /// <summary>Gets when the key was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Gets or sets whether the key is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets when the key was revoked.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Gets or sets the rate limit per minute.</summary>
    public int RateLimitPerMinute { get; set; }

    /// <summary>Gets the total requests made.</summary>
    public long TotalRequests { get; private set; }

    /// <summary>Gets the requests made today.</summary>
    public int RequestsToday { get; private set; }

    /// <summary>Gets when the key was last used.</summary>
    public DateTime? LastUsedAt { get; private set; }

    private DateTime _lastResetDate = DateTime.UtcNow.Date;

    internal void IncrementUsage()
    {
        var today = DateTime.UtcNow.Date;
        if (today > _lastResetDate)
        {
            RequestsToday = 0;
            _lastResetDate = today;
        }

        TotalRequests++;
        RequestsToday++;
        LastUsedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// API key usage statistics.
/// </summary>
public sealed class ApiKeyUsageStats
{
    /// <summary>Gets the key ID.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>Gets the total requests.</summary>
    public long TotalRequests { get; init; }

    /// <summary>Gets the requests today.</summary>
    public int RequestsToday { get; init; }

    /// <summary>Gets when the key was last used.</summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>Gets when the key was created.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Authentication result.
/// </summary>
internal sealed class AuthenticationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public ApiKey? ApiKey { get; init; }
}

/// <summary>
/// Rate limit bucket using token bucket algorithm.
/// </summary>
internal sealed class RateLimitBucket
{
    private readonly int _maxTokens;
    private readonly double _refillRate;
    private double _tokens;
    private DateTime _lastRefill;
    private readonly object _lock = new();

    public RateLimitBucket(int tokensPerMinute)
    {
        _maxTokens = tokensPerMinute;
        _refillRate = tokensPerMinute / 60.0; // Tokens per second
        _tokens = tokensPerMinute;
        _lastRefill = DateTime.UtcNow;
    }

    public bool IsExpired => (DateTime.UtcNow - _lastRefill).TotalMinutes > 10;

    public RateLimitResult TryConsume()
    {
        lock (_lock)
        {
            Refill();

            if (_tokens >= 1)
            {
                _tokens -= 1;
                return new RateLimitResult
                {
                    IsAllowed = true,
                    Limit = _maxTokens,
                    Remaining = (int)_tokens,
                    ResetAt = DateTime.UtcNow.AddSeconds((_maxTokens - _tokens) / _refillRate)
                };
            }

            return new RateLimitResult
            {
                IsAllowed = false,
                Limit = _maxTokens,
                Remaining = 0,
                ResetAt = DateTime.UtcNow.AddSeconds(1 / _refillRate)
            };
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        _tokens = Math.Min(_maxTokens, _tokens + elapsed * _refillRate);
        _lastRefill = now;
    }
}

/// <summary>
/// Rate limit check result.
/// </summary>
internal sealed class RateLimitResult
{
    public bool IsAllowed { get; init; }
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public DateTime ResetAt { get; init; }
}
