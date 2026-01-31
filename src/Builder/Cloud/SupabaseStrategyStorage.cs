namespace OoplesFinance.StockIndicators.Builder.Cloud;

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OoplesFinance.StockIndicators.Builder.VisualBuilder;

/// <summary>
/// Cloud storage service for strategies using Supabase.
/// Handles all strategy persistence for the SaaS platform.
/// </summary>
public sealed class SupabaseStrategyStorage : IStrategyStorage, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the SupabaseStrategyStorage class.
    /// </summary>
    public SupabaseStrategyStorage(SupabaseConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.ProjectUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("apikey", config.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Saves a strategy to the cloud.
    /// </summary>
    public async Task<string> SaveStrategyAsync(StrategyDefinition strategy, string userId, CancellationToken ct = default)
    {
        if (strategy is null) throw new ArgumentNullException(nameof(strategy));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var record = new StrategyRecord
        {
            Id = strategy.Id,
            UserId = userId,
            Name = strategy.Name,
            Description = strategy.Description,
            Version = strategy.Version,
            Content = strategy.ToJson(),
            IsPublic = false,
            CreatedAt = strategy.CreatedAt,
            ModifiedAt = DateTime.UtcNow,
            Tags = new List<string>()
        };

        var json = JsonSerializer.Serialize(record, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Upsert - insert or update if exists
        var response = await _httpClient.PostAsync(
            $"/rest/v1/strategies?on_conflict=id",
            content,
            ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return strategy.Id;
    }

    /// <summary>
    /// Loads a strategy from the cloud.
    /// </summary>
    public async Task<StrategyDefinition?> LoadStrategyAsync(string strategyId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyId)) throw new ArgumentException("Strategy ID is required", nameof(strategyId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var response = await _httpClient.GetAsync(
            $"/rest/v1/strategies?id=eq.{Uri.EscapeDataString(strategyId)}&or=(user_id.eq.{Uri.EscapeDataString(userId)},is_public.eq.true)&select=*",
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return null;

        var records = await DeserializeResponseAsync<List<StrategyRecord>>(response, ct).ConfigureAwait(false);
        var record = records?.FirstOrDefault();

        if (record?.Content is null) return null;

        return StrategyDefinition.FromJson(record.Content);
    }

    /// <summary>
    /// Deletes a strategy from the cloud.
    /// </summary>
    public async Task<bool> DeleteStrategyAsync(string strategyId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyId)) throw new ArgumentException("Strategy ID is required", nameof(strategyId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/rest/v1/strategies?id=eq.{Uri.EscapeDataString(strategyId)}&user_id=eq.{Uri.EscapeDataString(userId)}");

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Lists all strategies for a user.
    /// </summary>
    public async Task<IReadOnlyList<StrategyMetadata>> ListStrategiesAsync(string userId, int limit = 100, int offset = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var response = await _httpClient.GetAsync(
            $"/rest/v1/strategies?user_id=eq.{Uri.EscapeDataString(userId)}&select=id,name,description,version,is_public,created_at,modified_at,tags&order=modified_at.desc&limit={limit}&offset={offset}",
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return Array.Empty<StrategyMetadata>();

        var records = await DeserializeResponseAsync<List<StrategyRecord>>(response, ct).ConfigureAwait(false);
        return records?.Select(r => new StrategyMetadata
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Version = r.Version,
            IsPublic = r.IsPublic,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            Tags = r.Tags ?? new List<string>()
        }).ToList() ?? new List<StrategyMetadata>();
    }

    /// <summary>
    /// Searches strategies by name or description.
    /// </summary>
    public async Task<IReadOnlyList<StrategyMetadata>> SearchStrategiesAsync(string query, string userId, int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required", nameof(query));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var escapedQuery = Uri.EscapeDataString($"%{query}%");

        var response = await _httpClient.GetAsync(
            $"/rest/v1/strategies?or=(name.ilike.{escapedQuery},description.ilike.{escapedQuery})&or=(user_id.eq.{Uri.EscapeDataString(userId)},is_public.eq.true)&select=id,name,description,version,is_public,created_at,modified_at,tags&order=modified_at.desc&limit={limit}",
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return Array.Empty<StrategyMetadata>();

        var records = await DeserializeResponseAsync<List<StrategyRecord>>(response, ct).ConfigureAwait(false);
        return records?.Select(r => new StrategyMetadata
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Version = r.Version,
            IsPublic = r.IsPublic,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            Tags = r.Tags ?? new List<string>()
        }).ToList() ?? new List<StrategyMetadata>();
    }

    /// <summary>
    /// Gets public strategies for the marketplace.
    /// </summary>
    public async Task<IReadOnlyList<StrategyMetadata>> GetPublicStrategiesAsync(int limit = 50, int offset = 0, string? category = null, CancellationToken ct = default)
    {
        var url = $"/rest/v1/strategies?is_public=eq.true&select=id,name,description,version,user_id,created_at,modified_at,tags,download_count,rating&order=download_count.desc&limit={limit}&offset={offset}";

        if (!string.IsNullOrWhiteSpace(category))
        {
            url += $"&tags=cs.{{{Uri.EscapeDataString(category)}}}";
        }

        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return Array.Empty<StrategyMetadata>();

        var records = await DeserializeResponseAsync<List<StrategyRecord>>(response, ct).ConfigureAwait(false);
        return records?.Select(r => new StrategyMetadata
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Version = r.Version,
            IsPublic = r.IsPublic,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            Tags = r.Tags ?? new List<string>(),
            AuthorId = r.UserId,
            DownloadCount = r.DownloadCount,
            Rating = r.Rating
        }).ToList() ?? new List<StrategyMetadata>();
    }

    /// <summary>
    /// Publishes a strategy to the marketplace.
    /// </summary>
    public async Task<bool> PublishStrategyAsync(string strategyId, string userId, bool isPublic, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyId)) throw new ArgumentException("Strategy ID is required", nameof(strategyId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var patch = new { is_public = isPublic, modified_at = DateTime.UtcNow };
        var json = JsonSerializer.Serialize(patch, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(new HttpMethod("PATCH"),
            $"/rest/v1/strategies?id=eq.{Uri.EscapeDataString(strategyId)}&user_id=eq.{Uri.EscapeDataString(userId)}")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Copies a public strategy to user's library.
    /// </summary>
    public async Task<string?> ForkStrategyAsync(string strategyId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyId)) throw new ArgumentException("Strategy ID is required", nameof(strategyId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        // Load the public strategy
        var response = await _httpClient.GetAsync(
            $"/rest/v1/strategies?id=eq.{Uri.EscapeDataString(strategyId)}&is_public=eq.true&select=*",
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return null;

        var records = await DeserializeResponseAsync<List<StrategyRecord>>(response, ct).ConfigureAwait(false);
        var sourceRecord = records?.FirstOrDefault();

        if (sourceRecord?.Content is null) return null;

        // Create a copy with new ID and user
        var strategy = StrategyDefinition.FromJson(sourceRecord.Content);
        var newId = Guid.NewGuid().ToString();
        strategy.Id = newId;
        strategy.Name = $"{strategy.Name} (Fork)";
        strategy.CreatedAt = DateTime.UtcNow;
        strategy.ModifiedAt = DateTime.UtcNow;

        await SaveStrategyAsync(strategy, userId, ct).ConfigureAwait(false);

        // Increment the original's download count
        await IncrementDownloadCountAsync(strategyId, ct).ConfigureAwait(false);

        return newId;
    }

    /// <summary>
    /// Updates strategy tags.
    /// </summary>
    public async Task<bool> UpdateTagsAsync(string strategyId, string userId, IReadOnlyList<string> tags, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyId)) throw new ArgumentException("Strategy ID is required", nameof(strategyId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var patch = new { tags, modified_at = DateTime.UtcNow };
        var json = JsonSerializer.Serialize(patch, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(new HttpMethod("PATCH"),
            $"/rest/v1/strategies?id=eq.{Uri.EscapeDataString(strategyId)}&user_id=eq.{Uri.EscapeDataString(userId)}")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Rates a public strategy.
    /// </summary>
    public async Task<bool> RateStrategyAsync(string strategyId, string userId, int rating, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyId)) throw new ArgumentException("Strategy ID is required", nameof(strategyId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");

        // Save the rating
        var ratingRecord = new
        {
            strategy_id = strategyId,
            user_id = userId,
            rating,
            created_at = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(ratingRecord, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            "/rest/v1/strategy_ratings?on_conflict=strategy_id,user_id",
            content,
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return false;

        // Update the strategy's average rating
        await UpdateAverageRatingAsync(strategyId, ct).ConfigureAwait(false);

        return true;
    }

    private async Task IncrementDownloadCountAsync(string strategyId, CancellationToken ct)
    {
        // Use Supabase RPC for atomic increment
        var rpcCall = new { p_strategy_id = strategyId };
        var json = JsonSerializer.Serialize(rpcCall, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _httpClient.PostAsync("/rest/v1/rpc/increment_download_count", content, ct).ConfigureAwait(false);
    }

    private async Task UpdateAverageRatingAsync(string strategyId, CancellationToken ct)
    {
        // Use Supabase RPC for calculating average
        var rpcCall = new { p_strategy_id = strategyId };
        var json = JsonSerializer.Serialize(rpcCall, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _httpClient.PostAsync("/rest/v1/rpc/update_average_rating", content, ct).ConfigureAwait(false);
    }

    private async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
    {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content)) return null;
        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Configuration for Supabase connection.
/// </summary>
public sealed class SupabaseConfig
{
    /// <summary>Gets or sets the Supabase project URL.</summary>
    public string ProjectUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the Supabase API key (anon or service role).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets whether to use service role key for admin operations.</summary>
    public bool UseServiceRole { get; set; } = false;
}

/// <summary>
/// Interface for strategy storage operations.
/// </summary>
public interface IStrategyStorage
{
    /// <summary>Saves a strategy.</summary>
    Task<string> SaveStrategyAsync(StrategyDefinition strategy, string userId, CancellationToken ct = default);

    /// <summary>Loads a strategy.</summary>
    Task<StrategyDefinition?> LoadStrategyAsync(string strategyId, string userId, CancellationToken ct = default);

    /// <summary>Deletes a strategy.</summary>
    Task<bool> DeleteStrategyAsync(string strategyId, string userId, CancellationToken ct = default);

    /// <summary>Lists user's strategies.</summary>
    Task<IReadOnlyList<StrategyMetadata>> ListStrategiesAsync(string userId, int limit = 100, int offset = 0, CancellationToken ct = default);

    /// <summary>Searches strategies.</summary>
    Task<IReadOnlyList<StrategyMetadata>> SearchStrategiesAsync(string query, string userId, int limit = 50, CancellationToken ct = default);
}

/// <summary>
/// Strategy metadata without the full content.
/// </summary>
public sealed class StrategyMetadata
{
    /// <summary>Gets or sets the strategy ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the strategy name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the strategy is public.</summary>
    public bool IsPublic { get; set; }

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the last modified timestamp.</summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>Gets or sets the tags.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Gets or sets the author ID (for public strategies).</summary>
    public string? AuthorId { get; set; }

    /// <summary>Gets or sets the download/fork count.</summary>
    public int DownloadCount { get; set; }

    /// <summary>Gets or sets the average rating.</summary>
    public decimal Rating { get; set; }
}

/// <summary>
/// Internal record for Supabase storage.
/// </summary>
internal sealed class StrategyRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("modified_at")]
    public DateTime ModifiedAt { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("rating")]
    public decimal Rating { get; set; }
}
