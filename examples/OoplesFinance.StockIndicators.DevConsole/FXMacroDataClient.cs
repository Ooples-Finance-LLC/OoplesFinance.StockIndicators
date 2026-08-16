using System.Globalization;
using System.Text.Json;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.DevConsole;

/// <summary>
/// Focused data-loading helper for the FXMacroData developer-console example.
/// This is intentionally not part of the indicator library's public API.
/// </summary>
internal sealed class FXMacroDataClient
{
    private const string DefaultBaseUrl = "https://api.fxmacrodata.com/v1";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    internal FXMacroDataClient(
        HttpClient httpClient,
        string apiKey,
        string baseUrl = DefaultBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    internal async Task<IReadOnlyList<TickerData>> GetDailyFxAsync(
        string baseCurrency,
        string quoteCurrency,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End date must not precede start date.");
        }

        var uri = BuildUri(baseCurrency, quoteCurrency, start, end);
        using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseDailyFx(json);
    }

    internal Uri BuildUri(string baseCurrency, string quoteCurrency, DateOnly start, DateOnly end)
    {
        var baseCode = NormalizeCurrency(baseCurrency, nameof(baseCurrency));
        var quoteCode = NormalizeCurrency(quoteCurrency, nameof(quoteCurrency));
        var query = string.Join(
            "&",
            $"start_date={Uri.EscapeDataString(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
            $"end_date={Uri.EscapeDataString(end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
            $"api_key={Uri.EscapeDataString(_apiKey)}");

        return new Uri($"{_baseUrl}/forex/{baseCode}/{quoteCode}?{query}", UriKind.Absolute);
    }

    internal static IReadOnlyList<TickerData> ParseDailyFx(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("FXMacroData response does not contain a data array.");
        }

        var rows = new List<TickerData>();
        foreach (var row in data.EnumerateArray())
        {
            if (!TryReadDate(row, out var date))
            {
                continue;
            }

            var fallback = ReadNumber(row, "close")
                ?? ReadNumber(row, "val")
                ?? ReadNumber(row, "value")
                ?? ReadNumber(row, "rate");
            if (fallback is null)
            {
                continue;
            }

            rows.Add(new TickerData
            {
                Date = date,
                Open = ReadNumber(row, "open") ?? fallback.Value,
                High = ReadNumber(row, "high") ?? fallback.Value,
                Low = ReadNumber(row, "low") ?? fallback.Value,
                Close = ReadNumber(row, "close") ?? fallback.Value,
                Volume = 0d
            });
        }

        return rows
            .GroupBy(row => row.Date)
            .Select(group => group.Last())
            .OrderBy(row => row.Date)
            .ToArray();
    }

    private static string NormalizeCurrency(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Currency codes must contain exactly three ASCII letters.", parameterName);
        }
        return normalized;
    }

    private static bool TryReadDate(JsonElement row, out DateTime date)
    {
        date = default;
        if (!row.TryGetProperty("date", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return DateTime.TryParse(
            value.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out date);
    }

    private static double? ReadNumber(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }
        return null;
    }
}
