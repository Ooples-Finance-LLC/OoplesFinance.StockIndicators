using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OoplesFinance.StockIndicators.Builder.Cloud;

#if NET461
/// <summary>
/// Custom snake_case naming policy for .NET Framework 4.6.1 compatibility.
/// </summary>
internal sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public static SnakeCaseNamingPolicy Instance { get; } = new();

    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// HttpMethod.Patch polyfill for .NET Framework 4.6.1.
/// </summary>
internal static class HttpMethodExtensions
{
    public static HttpMethod Patch { get; } = new HttpMethod("PATCH");
}
#endif

/// <summary>
/// Supabase client for PostgreSQL database access.
/// Provides authentication, CRUD operations, and real-time subscriptions.
/// </summary>
public sealed class SupabaseClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpiry;
    private bool _disposed;

    /// <summary>
    /// Gets the current authenticated user ID.
    /// </summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// Gets whether the client is currently authenticated.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry;

    /// <summary>
    /// Event raised when authentication state changes.
    /// </summary>
    public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    /// <summary>
    /// Creates a new Supabase client.
    /// </summary>
    public SupabaseClient(SupabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrEmpty(options.Url))
            throw new ArgumentException("Supabase URL is required", nameof(options));
        if (string.IsNullOrEmpty(options.AnonKey))
            throw new ArgumentException("Supabase anon key is required", nameof(options));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.Url.TrimEnd('/')),
            Timeout = options.Timeout
        };

        _httpClient.DefaultRequestHeaders.Add("apikey", options.AnonKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _jsonOptions = new JsonSerializerOptions
        {
#if NET461
            PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
#else
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
#endif
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Authentication

    /// <summary>
    /// Signs up a new user with email and password.
    /// </summary>
    public async Task<AuthResult> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var request = new { email, password };
        var response = await PostAsync<AuthResponse>("/auth/v1/signup", request, cancellationToken).ConfigureAwait(false);

        if (response?.User is not null)
        {
            await SetSessionAsync(response).ConfigureAwait(false);
            return new AuthResult { Success = true, UserId = response.User.Id };
        }

        return new AuthResult { Success = false, ErrorMessage = "Failed to create account" };
    }

    /// <summary>
    /// Signs in with email and password.
    /// </summary>
    public async Task<AuthResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var request = new { email, password };
        var response = await PostAsync<AuthResponse>("/auth/v1/token?grant_type=password", request, cancellationToken).ConfigureAwait(false);

        if (response?.AccessToken is not null)
        {
            await SetSessionAsync(response).ConfigureAwait(false);
            return new AuthResult { Success = true, UserId = response.User?.Id };
        }

        return new AuthResult { Success = false, ErrorMessage = "Invalid email or password" };
    }

    /// <summary>
    /// Signs in with OAuth provider.
    /// </summary>
    public string GetOAuthSignInUrl(string provider, string redirectTo)
    {
        return $"{_options.Url}/auth/v1/authorize?provider={provider}&redirect_to={Uri.EscapeDataString(redirectTo)}";
    }

    /// <summary>
    /// Exchanges OAuth code for session.
    /// </summary>
    public async Task<AuthResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var request = new { code };
        var response = await PostAsync<AuthResponse>("/auth/v1/token?grant_type=authorization_code", request, cancellationToken).ConfigureAwait(false);

        if (response?.AccessToken is not null)
        {
            await SetSessionAsync(response).ConfigureAwait(false);
            return new AuthResult { Success = true, UserId = response.User?.Id };
        }

        return new AuthResult { Success = false, ErrorMessage = "Failed to exchange code" };
    }

    /// <summary>
    /// Refreshes the access token using the refresh token.
    /// </summary>
    public async Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(_refreshToken))
            return false;

        var request = new { refresh_token = _refreshToken };
        var response = await PostAsync<AuthResponse>("/auth/v1/token?grant_type=refresh_token", request, cancellationToken).ConfigureAwait(false);

        if (response?.AccessToken is not null)
        {
            await SetSessionAsync(response).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Signs out the current user.
    /// </summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!string.IsNullOrEmpty(_accessToken))
        {
            try
            {
                await PostAsync<object>("/auth/v1/logout", new { }, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during sign out
            }
        }

        ClearSession();
    }

    /// <summary>
    /// Sends a password reset email.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var request = new { email };
            await PostAsync<object>("/auth/v1/recover", request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Updates the user's password.
    /// </summary>
    public async Task<bool> UpdatePasswordAsync(string newPassword, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsAuthenticated)
            return false;

        try
        {
            var request = new { password = newPassword };
            await PutAsync<object>("/auth/v1/user", request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SetSessionAsync(AuthResponse response)
    {
        _accessToken = response.AccessToken;
        _refreshToken = response.RefreshToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn > 0 ? response.ExpiresIn - 60 : 3540);
        UserId = response.User?.Id;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(true, UserId));
        await Task.CompletedTask;
    }

    private void ClearSession()
    {
        _accessToken = null;
        _refreshToken = null;
        _tokenExpiry = DateTime.MinValue;
        UserId = null;

        _httpClient.DefaultRequestHeaders.Authorization = null;

        AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(false, null));
    }

    /// <summary>
    /// Sets a session from stored tokens.
    /// </summary>
    public void SetSession(string accessToken, string refreshToken, string userId)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _tokenExpiry = DateTime.UtcNow.AddMinutes(55); // Assume ~1 hour tokens
        UserId = userId;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(true, UserId));
    }

    #endregion

    #region Database Operations

    /// <summary>
    /// Creates a query builder for the specified table.
    /// </summary>
    public SupabaseQueryBuilder<T> From<T>(string table) where T : class
    {
        ThrowIfDisposed();
        EnsureAuthenticated();
        return new SupabaseQueryBuilder<T>(this, table, _jsonOptions);
    }

    /// <summary>
    /// Inserts a record into a table.
    /// </summary>
    public async Task<T?> InsertAsync<T>(string table, T record, CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        var response = await PostAsync<List<T>>($"/rest/v1/{table}", record, cancellationToken, "return=representation").ConfigureAwait(false);
        return response?.FirstOrDefault();
    }

    /// <summary>
    /// Inserts multiple records into a table.
    /// </summary>
    public async Task<List<T>?> InsertManyAsync<T>(string table, IEnumerable<T> records, CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        return await PostAsync<List<T>>($"/rest/v1/{table}", records, cancellationToken, "return=representation").ConfigureAwait(false);
    }

    /// <summary>
    /// Updates records matching the filter.
    /// </summary>
    public async Task<List<T>?> UpdateAsync<T>(string table, object updates, string filter, CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        return await PatchAsync<List<T>>($"/rest/v1/{table}?{filter}", updates, cancellationToken, "return=representation").ConfigureAwait(false);
    }

    /// <summary>
    /// Upserts a record (insert or update on conflict).
    /// </summary>
    public async Task<T?> UpsertAsync<T>(string table, T record, string onConflict, CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        var response = await PostAsync<List<T>>($"/rest/v1/{table}?on_conflict={onConflict}", record, cancellationToken, "return=representation,resolution=merge-duplicates").ConfigureAwait(false);
        return response?.FirstOrDefault();
    }

    /// <summary>
    /// Deletes records matching the filter.
    /// </summary>
    public async Task<bool> DeleteAsync(string table, string filter, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        try
        {
            await DeleteAsync<object>($"/rest/v1/{table}?{filter}", cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Calls a database function.
    /// </summary>
    public async Task<T?> RpcAsync<T>(string functionName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        return await PostAsync<T>($"/rest/v1/rpc/{functionName}", parameters ?? new { }, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region HTTP Methods

    internal async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    internal async Task<T?> PostAsync<T>(string path, object body, CancellationToken cancellationToken, string? prefer = null)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = httpContent };
        if (prefer is not null)
            request.Headers.Add("Prefer", prefer);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    internal async Task<T?> PutAsync<T>(string path, object body, CancellationToken cancellationToken, string? prefer = null)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = httpContent };
        if (prefer is not null)
            request.Headers.Add("Prefer", prefer);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    internal async Task<T?> PatchAsync<T>(string path, object body, CancellationToken cancellationToken, string? prefer = null)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

#if NET461
        var patchMethod = HttpMethodExtensions.Patch;
        var requestUri = new Uri(_httpClient.BaseAddress, path);
        using var request = new HttpRequestMessage(patchMethod, requestUri) { Content = httpContent };
#else
        using var request = new HttpRequestMessage(HttpMethod.Patch, path) { Content = httpContent };
#endif
        if (prefer is not null)
            request.Headers.Add("Prefer", prefer);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    internal async Task<T?> DeleteAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await _httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    #endregion

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("Not authenticated. Call SignInAsync first.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SupabaseClient));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Fluent query builder for Supabase tables.
/// </summary>
public sealed class SupabaseQueryBuilder<T> where T : class
{
    private readonly SupabaseClient _client;
    private readonly string _table;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<string> _filters = new();
    private string? _select;
    private string? _order;
    private int? _limit;
    private int? _offset;

    internal SupabaseQueryBuilder(SupabaseClient client, string table, JsonSerializerOptions jsonOptions)
    {
        _client = client;
        _table = table;
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Selects specific columns.
    /// </summary>
    public SupabaseQueryBuilder<T> Select(string columns = "*")
    {
        _select = columns;
        return this;
    }

    /// <summary>
    /// Adds an equality filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Eq(string column, object value)
    {
        _filters.Add($"{column}=eq.{Uri.EscapeDataString(value.ToString() ?? "")}");
        return this;
    }

    /// <summary>
    /// Adds a not-equal filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Neq(string column, object value)
    {
        _filters.Add($"{column}=neq.{Uri.EscapeDataString(value.ToString() ?? "")}");
        return this;
    }

    /// <summary>
    /// Adds a greater-than filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Gt(string column, object value)
    {
        _filters.Add($"{column}=gt.{Uri.EscapeDataString(value.ToString() ?? "")}");
        return this;
    }

    /// <summary>
    /// Adds a greater-than-or-equal filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Gte(string column, object value)
    {
        _filters.Add($"{column}=gte.{Uri.EscapeDataString(value.ToString() ?? "")}");
        return this;
    }

    /// <summary>
    /// Adds a less-than filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Lt(string column, object value)
    {
        _filters.Add($"{column}=lt.{Uri.EscapeDataString(value.ToString() ?? "")}");
        return this;
    }

    /// <summary>
    /// Adds a less-than-or-equal filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Lte(string column, object value)
    {
        _filters.Add($"{column}=lte.{Uri.EscapeDataString(value.ToString() ?? "")}");
        return this;
    }

    /// <summary>
    /// Adds a LIKE filter.
    /// </summary>
    public SupabaseQueryBuilder<T> Like(string column, string pattern)
    {
        _filters.Add($"{column}=like.{Uri.EscapeDataString(pattern)}");
        return this;
    }

    /// <summary>
    /// Adds an IN filter.
    /// </summary>
    public SupabaseQueryBuilder<T> In(string column, IEnumerable<object> values)
    {
        var valueList = string.Join(",", values.Select(v => Uri.EscapeDataString(v.ToString() ?? "")));
        _filters.Add($"{column}=in.({valueList})");
        return this;
    }

    /// <summary>
    /// Adds an IS NULL filter.
    /// </summary>
    public SupabaseQueryBuilder<T> IsNull(string column)
    {
        _filters.Add($"{column}=is.null");
        return this;
    }

    /// <summary>
    /// Adds an IS NOT NULL filter.
    /// </summary>
    public SupabaseQueryBuilder<T> IsNotNull(string column)
    {
        _filters.Add($"{column}=not.is.null");
        return this;
    }

    /// <summary>
    /// Orders results by a column.
    /// </summary>
    public SupabaseQueryBuilder<T> Order(string column, bool ascending = true)
    {
        _order = $"{column}.{(ascending ? "asc" : "desc")}";
        return this;
    }

    /// <summary>
    /// Limits the number of results.
    /// </summary>
    public SupabaseQueryBuilder<T> Limit(int count)
    {
        _limit = count;
        return this;
    }

    /// <summary>
    /// Skips a number of results.
    /// </summary>
    public SupabaseQueryBuilder<T> Offset(int count)
    {
        _offset = count;
        return this;
    }

    /// <summary>
    /// Executes the query and returns results.
    /// </summary>
    public async Task<List<T>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var path = BuildPath();
        var result = await _client.GetAsync<List<T>>(path, cancellationToken).ConfigureAwait(false);
        return result ?? new List<T>();
    }

    /// <summary>
    /// Executes the query and returns a single result.
    /// </summary>
    public async Task<T?> SingleAsync(CancellationToken cancellationToken = default)
    {
        _limit = 1;
        var results = await ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Executes the query and returns the count.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        _select = "count";
        var path = BuildPath() + "&count=exact";
        // This would need special handling for the count header
        var result = await _client.GetAsync<List<T>>(path, cancellationToken).ConfigureAwait(false);
        return result?.Count ?? 0;
    }

    private string BuildPath()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(_select))
            parts.Add($"select={_select}");

        parts.AddRange(_filters);

        if (!string.IsNullOrEmpty(_order))
            parts.Add($"order={_order}");

        if (_limit.HasValue)
            parts.Add($"limit={_limit}");

        if (_offset.HasValue)
            parts.Add($"offset={_offset}");

        var query = string.Join("&", parts);
        return $"/rest/v1/{_table}?{query}";
    }
}

/// <summary>
/// Supabase client configuration options.
/// </summary>
public sealed class SupabaseOptions
{
    /// <summary>
    /// The Supabase project URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The Supabase anon/public key.
    /// </summary>
    public string AnonKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional service role key for server-side operations.
    /// </summary>
    public string? ServiceRoleKey { get; set; }

    /// <summary>
    /// Request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Result of an authentication operation.
/// </summary>
public sealed class AuthResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event args for auth state changes.
/// </summary>
public sealed class AuthStateChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; }
    public string? UserId { get; }

    public AuthStateChangedEventArgs(bool isAuthenticated, string? userId)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
    }
}

#region Internal Response Types

internal sealed class AuthResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("user")]
    public AuthUser? User { get; set; }
}

internal sealed class AuthUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

#endregion
