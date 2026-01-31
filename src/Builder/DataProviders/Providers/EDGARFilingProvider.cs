using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Providers;

/// <summary>
/// SEC EDGAR filing provider.
/// Provides direct access to SEC EDGAR data (official, free, authoritative source).
/// Important: SEC requires 10 requests/second rate limit and a valid User-Agent.
/// </summary>
public sealed class EDGARFilingProvider : ISECFilingProvider
{
    private readonly HttpClient _httpClient;
    private readonly SECFilingProviderOptions _options;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, string> _cikCache;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private bool _disposed;

    private const string EdgarBaseUrl = "https://www.sec.gov";
    private const string DataBaseUrl = "https://data.sec.gov";
    private const string EffBaseUrl = "https://efts.sec.gov";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates a new EDGAR filing provider.
    /// </summary>
    /// <param name="options">Provider options.</param>
    /// <param name="httpClient">Optional HTTP client (for testing).</param>
    public EDGARFilingProvider(SECFilingProviderOptions? options = null, HttpClient? httpClient = null)
    {
        _options = options ?? new SECFilingProviderOptions();
        _cikCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = _options.Timeout;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json,text/html,*/*");

        // SEC requires a valid User-Agent with contact info
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);

        _rateLimiter = new SemaphoreSlim(1, 1);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public string ProviderName => "SEC EDGAR";

    /// <inheritdoc />
    public bool IsConnected => !_disposed;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SECFilingData>> GetFilingsAsync(
        string symbol,
        IEnumerable<string>? formTypes = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var cik = await LookupCikAsync(symbol, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(cik))
        {
            return Array.Empty<SECFilingData>();
        }

        // Pad CIK to 10 digits
        var paddedCik = cik.PadLeft(10, '0');

        // Get company submissions
        var url = $"{DataBaseUrl}/submissions/CIK{paddedCik}.json";
        var response = await ExecuteRequestAsync<EdgarSubmissionsResponse>(url, cancellationToken)
            .ConfigureAwait(false);

        if (response?.Filings?.Recent is null)
        {
            return Array.Empty<SECFilingData>();
        }

        var filings = new List<SECFilingData>();
        var formTypeSet = formTypes?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recent = response.Filings.Recent;

        for (int i = 0; i < recent.AccessionNumber.Count && filings.Count < limit; i++)
        {
            var formType = recent.Form[i];

            // Filter by form type
            if (formTypeSet is not null && !formTypeSet.Contains(formType))
                continue;

            var filedDate = DateTime.Parse(recent.FilingDate[i]);

            // Filter by date
            if (startDate.HasValue && filedDate < startDate.Value)
                continue;
            if (endDate.HasValue && filedDate > endDate.Value)
                continue;

            var accessionNumber = recent.AccessionNumber[i];
            var accessionPath = accessionNumber.Replace("-", "");

            filings.Add(new SECFilingData
            {
                AccessionNumber = accessionNumber,
                Symbol = symbol,
                Cik = cik,
                CompanyName = response.Name ?? string.Empty,
                FormType = formType,
                FiledAt = filedDate,
                AcceptedAt = DateTime.TryParse(recent.AcceptanceDateTime?[i], out var accepted) ? accepted : null,
                Period = recent.ReportDate?[i],
                Url = $"{EdgarBaseUrl}/Archives/edgar/data/{cik}/{accessionPath}/{recent.PrimaryDocument?[i]}",
                IndexUrl = $"{EdgarBaseUrl}/cgi-bin/browse-edgar?action=getcompany&CIK={cik}&type={formType}&dateb=&owner=include&count=40",
                Description = recent.Items?[i],
                PrimaryDocument = recent.PrimaryDocument?[i],
                FileSize = recent.Size?[i],
                FiscalYearEnd = response.FiscalYearEnd,
                Provider = ProviderName
            });
        }

        return filings;
    }

    /// <inheritdoc />
    public async Task<SECFilingData?> GetFilingByAccessionAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default)
    {
        // Parse CIK from accession number or search for it
        var parts = accessionNumber.Split('-');
        if (parts.Length < 3)
            return null;

        // Search for the filing using full-text search
        var searchUrl = $"{EffBaseUrl}/LATEST/search-index?q=\"{accessionNumber}\"&from=0&size=1";
        var searchResult = await ExecuteRequestAsync<EdgarSearchResponse>(searchUrl, cancellationToken)
            .ConfigureAwait(false);

        if (searchResult?.Hits?.Hits?.Count > 0)
        {
            var hit = searchResult.Hits.Hits[0];
            var source = hit.Source;

            return new SECFilingData
            {
                AccessionNumber = accessionNumber,
                Cik = source?.Ciks?.FirstOrDefault() ?? string.Empty,
                CompanyName = source?.DisplayNames?.FirstOrDefault() ?? string.Empty,
                FormType = source?.Form ?? string.Empty,
                FiledAt = DateTime.TryParse(source?.FiledAt, out var filed) ? filed : DateTime.MinValue,
                Url = $"{EdgarBaseUrl}/Archives/edgar/data/{source?.Ciks?.FirstOrDefault()}/{accessionNumber.Replace("-", "")}/",
                Provider = ProviderName
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<string> GetFilingContentAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default)
    {
        // First, get the filing metadata to find the document URL
        var filing = await GetFilingByAccessionAsync(accessionNumber, cancellationToken)
            .ConfigureAwait(false);

        if (filing is null || string.IsNullOrWhiteSpace(filing.Url))
        {
            return string.Empty;
        }

        // Get the filing index to find the primary document
        var cik = filing.Cik.TrimStart('0');
        var accessionPath = accessionNumber.Replace("-", "");
        var indexUrl = $"{EdgarBaseUrl}/Archives/edgar/data/{cik}/{accessionPath}/";

        var indexHtml = await ExecuteRawRequestAsync(indexUrl, cancellationToken)
            .ConfigureAwait(false);

        // Parse index to find .htm or .txt primary document
        var documentUrl = ParsePrimaryDocumentUrl(indexHtml, indexUrl);

        if (string.IsNullOrWhiteSpace(documentUrl))
        {
            return string.Empty;
        }

        // Get the actual document
        var content = await ExecuteRawRequestAsync(documentUrl, cancellationToken)
            .ConfigureAwait(false);

        // Strip HTML if necessary
        if (content.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            content = StripHtmlTags(content);
        }

        return content;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilingSection>> GetFilingSectionsAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default)
    {
        var content = await GetFilingContentAsync(accessionNumber, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<FilingSection>();
        }

        return ExtractSections(content);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InsiderTransaction>> GetInsiderTransactionsAsync(
        string symbol,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var filings = await GetFilingsAsync(
            symbol,
            new[] { SECFormTypes.Form4 },
            startDate,
            endDate,
            limit: 100,
            cancellationToken).ConfigureAwait(false);

        var transactions = new List<InsiderTransaction>();

        foreach (var filing in filings)
        {
            try
            {
                // Get the XML content for Form 4
                var cik = filing.Cik.TrimStart('0');
                var accessionPath = filing.AccessionNumber.Replace("-", "");
                var xmlUrl = $"{EdgarBaseUrl}/Archives/edgar/data/{cik}/{accessionPath}/{filing.PrimaryDocument}";

                var xmlContent = await ExecuteRawRequestAsync(xmlUrl, cancellationToken)
                    .ConfigureAwait(false);

                var parsed = ParseForm4Xml(xmlContent, filing);
                transactions.AddRange(parsed);
            }
            catch
            {
                // Continue with next filing if parsing fails
            }
        }

        return transactions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InstitutionalHolding>> GetInstitutionalHoldingsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        // Search for 13F filings mentioning this symbol
        var searchUrl = $"{EffBaseUrl}/LATEST/search-index?q=form:\"13F-HR\"%20AND%20\"{symbol}\"&from=0&size=20";
        var searchResult = await ExecuteRequestAsync<EdgarSearchResponse>(searchUrl, cancellationToken)
            .ConfigureAwait(false);

        var holdings = new List<InstitutionalHolding>();

        if (searchResult?.Hits?.Hits is null)
            return holdings;

        foreach (var hit in searchResult.Hits.Hits)
        {
            var source = hit.Source;
            if (source is null) continue;

            holdings.Add(new InstitutionalHolding
            {
                InstitutionCik = source.Ciks?.FirstOrDefault() ?? string.Empty,
                InstitutionName = source.DisplayNames?.FirstOrDefault() ?? string.Empty,
                Symbol = symbol,
                FiledAt = DateTime.TryParse(source.FiledAt, out var filed) ? filed : DateTime.MinValue,
                PeriodEndDate = DateTime.TryParse(source.PeriodOfReport, out var period) ? period : DateTime.MinValue
            });
        }

        return holdings;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SECFilingData> StreamNewFilingsAsync(
        IEnumerable<string> symbols,
        IEnumerable<string>? formTypes = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var formTypeSet = formTypes?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lastSeenAccessions = new Dictionary<string, HashSet<string>>();

        foreach (var symbol in symbolList)
        {
            lastSeenAccessions[symbol] = new HashSet<string>();
        }

        // Initialize with current filings
        foreach (var symbol in symbolList)
        {
            var current = await GetFilingsAsync(symbol, formTypes, null, null, 10, cancellationToken)
                .ConfigureAwait(false);

            foreach (var filing in current)
            {
                lastSeenAccessions[symbol].Add(filing.AccessionNumber);
            }
        }

        // Poll for new filings
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in symbolList)
            {
                var filings = await GetFilingsAsync(symbol, formTypes, null, null, 10, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var filing in filings)
                {
                    if (lastSeenAccessions[symbol].Add(filing.AccessionNumber))
                    {
                        yield return filing;
                    }
                }
            }

            // Poll every 15 minutes (SEC filings aren't super frequent)
            await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SECFilingData>> GetRecentFilingsAsync(
        IEnumerable<string>? formTypes = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var formQuery = formTypes is not null
            ? string.Join(" OR ", formTypes.Select(f => $"form:\"{f}\""))
            : "form:\"10-K\" OR form:\"10-Q\" OR form:\"8-K\"";

        var searchUrl = $"{EffBaseUrl}/LATEST/search-index?q={Uri.EscapeDataString(formQuery)}&from=0&size={limit}&sort=-filed";
        var searchResult = await ExecuteRequestAsync<EdgarSearchResponse>(searchUrl, cancellationToken)
            .ConfigureAwait(false);

        if (searchResult?.Hits?.Hits is null)
            return Array.Empty<SECFilingData>();

        return searchResult.Hits.Hits.Select(hit =>
        {
            var source = hit.Source;
            return new SECFilingData
            {
                AccessionNumber = source?.Adsh ?? string.Empty,
                Cik = source?.Ciks?.FirstOrDefault() ?? string.Empty,
                CompanyName = source?.DisplayNames?.FirstOrDefault() ?? string.Empty,
                FormType = source?.Form ?? string.Empty,
                FiledAt = DateTime.TryParse(source?.FiledAt, out var filed) ? filed : DateTime.MinValue,
                Provider = ProviderName
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<CompanyInfo?> GetCompanyInfoAsync(
        string cik,
        CancellationToken cancellationToken = default)
    {
        var paddedCik = cik.PadLeft(10, '0');
        var url = $"{DataBaseUrl}/submissions/CIK{paddedCik}.json";

        var response = await ExecuteRequestAsync<EdgarSubmissionsResponse>(url, cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
            return null;

        return new CompanyInfo
        {
            Cik = cik,
            Name = response.Name ?? string.Empty,
            Tickers = response.Tickers ?? Array.Empty<string>(),
            Exchanges = response.Exchanges ?? Array.Empty<string>(),
            SicCode = response.Sic,
            SicDescription = response.SicDescription,
            StateOfIncorporation = response.StateOfIncorporation,
            FiscalYearEnd = response.FiscalYearEnd,
            BusinessAddress = FormatAddress(response.Addresses?.Business),
            Phone = response.Phone
        };
    }

    /// <inheritdoc />
    public async Task<string?> LookupCikAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var upperSymbol = symbol.ToUpperInvariant();

        // Check cache
        if (_options.CacheCikLookups && _cikCache.TryGetValue(upperSymbol, out var cachedCik))
        {
            return cachedCik;
        }

        // Use SEC's ticker->CIK mapping
        var url = $"{EdgarBaseUrl}/cgi-bin/browse-edgar?action=getcompany&company={upperSymbol}&type=&dateb=&owner=include&count=1&search_text=&output=atom";
        var atomFeed = await ExecuteRawRequestAsync(url, cancellationToken).ConfigureAwait(false);

        // Parse CIK from atom feed
        var cikMatch = Regex.Match(atomFeed, @"CIK=(\d+)", RegexOptions.None, RegexTimeout);
        if (cikMatch.Success)
        {
            var cik = cikMatch.Groups[1].Value;
            if (_options.CacheCikLookups)
            {
                _cikCache[upperSymbol] = cik;
            }
            return cik;
        }

        // Try the company_tickers.json endpoint
        var tickersUrl = $"{EdgarBaseUrl}/files/company_tickers.json";
        var tickersResponse = await ExecuteRequestAsync<Dictionary<string, EdgarTicker>>(tickersUrl, cancellationToken)
            .ConfigureAwait(false);

        if (tickersResponse is not null)
        {
            var match = tickersResponse.Values.FirstOrDefault(t =>
                t.Ticker?.Equals(upperSymbol, StringComparison.OrdinalIgnoreCase) ?? false);

            if (match is not null)
            {
                var cik = match.CikStr;
                if (_options.CacheCikLookups && !string.IsNullOrWhiteSpace(cik))
                {
                    _cikCache[upperSymbol] = cik;
                }
                return cik;
            }
        }

        return null;
    }

    private async Task<T?> ExecuteRequestAsync<T>(
        string url,
        CancellationToken cancellationToken)
    {
        var content = await ExecuteRawRequestAsync(url, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private async Task<string> ExecuteRawRequestAsync(
        string url,
        CancellationToken cancellationToken)
    {
        await RateLimitAsync(cancellationToken).ConfigureAwait(false);

        for (int attempt = 0; attempt <= _options.RetryOptions.MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                if ((int)response.StatusCode == 429)
                {
                    var delay = CalculateBackoff(attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return string.Empty;
                }
            }
            catch (HttpRequestException) when (attempt < _options.RetryOptions.MaxRetries)
            {
                var delay = CalculateBackoff(attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        return string.Empty;
    }

    private async Task RateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // SEC EDGAR: max 10 requests per second
            var minInterval = TimeSpan.FromMilliseconds(100);
            var elapsed = DateTime.UtcNow - _lastRequestTime;

            if (elapsed < minInterval)
            {
                await Task.Delay(minInterval - elapsed, cancellationToken).ConfigureAwait(false);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var delay = TimeSpan.FromSeconds(
            _options.RetryOptions.InitialDelay.TotalSeconds *
            Math.Pow(_options.RetryOptions.BackoffMultiplier, attempt));

        return delay > _options.RetryOptions.MaxDelay ? _options.RetryOptions.MaxDelay : delay;
    }

    private static string ParsePrimaryDocumentUrl(string indexHtml, string baseUrl)
    {
        // Look for .htm primary document
        var htmMatch = Regex.Match(indexHtml, @"href=""([^""]+\.htm)""", RegexOptions.IgnoreCase, RegexTimeout);
        if (htmMatch.Success)
        {
            var href = htmMatch.Groups[1].Value;
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : baseUrl.TrimEnd('/') + "/" + href;
        }

        // Fall back to .txt
        var txtMatch = Regex.Match(indexHtml, @"href=""([^""]+\.txt)""", RegexOptions.IgnoreCase, RegexTimeout);
        if (txtMatch.Success)
        {
            var href = txtMatch.Groups[1].Value;
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : baseUrl.TrimEnd('/') + "/" + href;
        }

        return string.Empty;
    }

    private static string StripHtmlTags(string html)
    {
        // Remove script and style elements
        html = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase, RegexTimeout);

        // Replace common entities
        html = html.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"");

        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ", RegexOptions.None, RegexTimeout);

        // Normalize whitespace
        html = Regex.Replace(html, @"\s+", " ", RegexOptions.None, RegexTimeout);

        return html.Trim();
    }

    private static IReadOnlyList<FilingSection> ExtractSections(string content)
    {
        var sections = new List<FilingSection>();

        var sectionPatterns = new (string Pattern, string Name, string? ItemNumber)[]
        {
            (@"(?i)ITEM\s*1A[\s\.\-:]+RISK\s*FACTORS", "Risk Factors", "1A"),
            (@"(?i)ITEM\s*7[\s\.\-:]+MANAGEMENT", "MD&A", "7"),
            (@"(?i)ITEM\s*1[\s\.\-:]+BUSINESS(?!\s*ADDRESS)", "Business", "1"),
            (@"(?i)ITEM\s*3[\s\.\-:]+LEGAL\s*PROCEEDINGS", "Legal Proceedings", "3"),
            (@"(?i)ITEM\s*8[\s\.\-:]+FINANCIAL\s*STATEMENTS", "Financial Statements", "8"),
            (@"(?i)ITEM\s*9A[\s\.\-:]+CONTROLS", "Controls and Procedures", "9A"),
        };

        foreach (var (pattern, name, itemNumber) in sectionPatterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.None, RegexTimeout);
            if (match.Success)
            {
                var start = match.Index;
                var end = content.Length;

                // Find next section start
                foreach (var (nextPattern, _, _) in sectionPatterns)
                {
                    if (nextPattern == pattern) continue;

                    var nextMatch = Regex.Match(content.Substring(start + match.Length), nextPattern, RegexOptions.None, RegexTimeout);
                    if (nextMatch.Success && nextMatch.Index + start + match.Length < end)
                    {
                        end = nextMatch.Index + start + match.Length;
                    }
                }

                var sectionContent = content.Substring(start, Math.Min(end - start, 50000));
                var wordCount = sectionContent.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

                sections.Add(new FilingSection
                {
                    SectionName = name,
                    ItemNumber = itemNumber,
                    Content = sectionContent,
                    StartPosition = start,
                    EndPosition = end,
                    WordCount = wordCount
                });
            }
        }

        return sections;
    }

    private static List<InsiderTransaction> ParseForm4Xml(string xml, SECFilingData filing)
    {
        var transactions = new List<InsiderTransaction>();

        // Simple regex-based parsing of Form 4 XML
        var ownerName = Regex.Match(xml, @"<rptOwnerName>([^<]+)</rptOwnerName>", RegexOptions.None, RegexTimeout).Groups[1].Value;
        var isOfficer = xml.Contains("<isOfficer>1</isOfficer>");
        var isDirector = xml.Contains("<isDirector>1</isDirector>");
        var isTenPercent = xml.Contains("<isTenPercentOwner>1</isTenPercentOwner>");

        var relationship = isTenPercent ? InsiderRelationship.TenPercentOwner :
                          isDirector ? InsiderRelationship.Director :
                          isOfficer ? InsiderRelationship.Officer :
                          InsiderRelationship.Other;

        var transactionMatches = Regex.Matches(xml,
            @"<transactionAmounts>.*?<transactionShares>.*?<value>([^<]+)</value>.*?<transactionPricePerShare>.*?<value>([^<]*)</value>.*?<transactionAcquiredDisposedCode>.*?<value>([^<]+)</value>",
            RegexOptions.Singleline, RegexTimeout);

        foreach (Match match in transactionMatches)
        {
            if (decimal.TryParse(match.Groups[1].Value, out var shares))
            {
                decimal.TryParse(match.Groups[2].Value, out var price);
                var acquireDispose = match.Groups[3].Value;

                transactions.Add(new InsiderTransaction
                {
                    AccessionNumber = filing.AccessionNumber,
                    Symbol = filing.Symbol,
                    InsiderName = ownerName,
                    Relationship = relationship,
                    TransactionDate = filing.FiledAt,
                    TransactionType = acquireDispose == "A" ? InsiderTransactionType.Purchase : InsiderTransactionType.Sale,
                    Shares = shares,
                    PricePerShare = price > 0 ? price : null,
                    FiledAt = filing.FiledAt
                });
            }
        }

        return transactions;
    }

    private static string FormatAddress(EdgarAddress? address)
    {
        if (address is null) return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(address.Street1)) parts.Add(address.Street1);
        if (!string.IsNullOrWhiteSpace(address.Street2)) parts.Add(address.Street2);
        if (!string.IsNullOrWhiteSpace(address.City)) parts.Add(address.City);
        if (!string.IsNullOrWhiteSpace(address.StateOrCountry)) parts.Add(address.StateOrCountry);
        if (!string.IsNullOrWhiteSpace(address.ZipCode)) parts.Add(address.ZipCode);

        return string.Join(", ", parts);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _rateLimiter.Dispose();
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

#region EDGAR API Response Models

internal sealed class EdgarSubmissionsResponse
{
    [JsonPropertyName("cik")]
    public string? Cik { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tickers")]
    public IReadOnlyList<string>? Tickers { get; set; }

    [JsonPropertyName("exchanges")]
    public IReadOnlyList<string>? Exchanges { get; set; }

    [JsonPropertyName("sic")]
    public string? Sic { get; set; }

    [JsonPropertyName("sicDescription")]
    public string? SicDescription { get; set; }

    [JsonPropertyName("stateOfIncorporation")]
    public string? StateOfIncorporation { get; set; }

    [JsonPropertyName("fiscalYearEnd")]
    public string? FiscalYearEnd { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("addresses")]
    public EdgarAddresses? Addresses { get; set; }

    [JsonPropertyName("filings")]
    public EdgarFilings? Filings { get; set; }
}

internal sealed class EdgarAddresses
{
    [JsonPropertyName("business")]
    public EdgarAddress? Business { get; set; }

    [JsonPropertyName("mailing")]
    public EdgarAddress? Mailing { get; set; }
}

internal sealed class EdgarAddress
{
    [JsonPropertyName("street1")]
    public string? Street1 { get; set; }

    [JsonPropertyName("street2")]
    public string? Street2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("stateOrCountry")]
    public string? StateOrCountry { get; set; }

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }
}

internal sealed class EdgarFilings
{
    [JsonPropertyName("recent")]
    public EdgarRecentFilings? Recent { get; set; }
}

internal sealed class EdgarRecentFilings
{
    [JsonPropertyName("accessionNumber")]
    public IReadOnlyList<string> AccessionNumber { get; set; } = Array.Empty<string>();

    [JsonPropertyName("filingDate")]
    public IReadOnlyList<string> FilingDate { get; set; } = Array.Empty<string>();

    [JsonPropertyName("reportDate")]
    public IReadOnlyList<string>? ReportDate { get; set; }

    [JsonPropertyName("acceptanceDateTime")]
    public IReadOnlyList<string>? AcceptanceDateTime { get; set; }

    [JsonPropertyName("act")]
    public IReadOnlyList<string>? Act { get; set; }

    [JsonPropertyName("form")]
    public IReadOnlyList<string> Form { get; set; } = Array.Empty<string>();

    [JsonPropertyName("fileNumber")]
    public IReadOnlyList<string>? FileNumber { get; set; }

    [JsonPropertyName("filmNumber")]
    public IReadOnlyList<string>? FilmNumber { get; set; }

    [JsonPropertyName("items")]
    public IReadOnlyList<string>? Items { get; set; }

    [JsonPropertyName("size")]
    public IReadOnlyList<long>? Size { get; set; }

    [JsonPropertyName("isXBRL")]
    public IReadOnlyList<int>? IsXBRL { get; set; }

    [JsonPropertyName("isInlineXBRL")]
    public IReadOnlyList<int>? IsInlineXBRL { get; set; }

    [JsonPropertyName("primaryDocument")]
    public IReadOnlyList<string>? PrimaryDocument { get; set; }

    [JsonPropertyName("primaryDocDescription")]
    public IReadOnlyList<string>? PrimaryDocDescription { get; set; }
}

internal sealed class EdgarSearchResponse
{
    [JsonPropertyName("hits")]
    public EdgarSearchHits? Hits { get; set; }
}

internal sealed class EdgarSearchHits
{
    [JsonPropertyName("hits")]
    public IReadOnlyList<EdgarSearchHit>? Hits { get; set; }

    [JsonPropertyName("total")]
    public EdgarSearchTotal? Total { get; set; }
}

internal sealed class EdgarSearchTotal
{
    [JsonPropertyName("value")]
    public int Value { get; set; }
}

internal sealed class EdgarSearchHit
{
    [JsonPropertyName("_source")]
    public EdgarSearchSource? Source { get; set; }
}

internal sealed class EdgarSearchSource
{
    [JsonPropertyName("ciks")]
    public IReadOnlyList<string>? Ciks { get; set; }

    [JsonPropertyName("display_names")]
    public IReadOnlyList<string>? DisplayNames { get; set; }

    [JsonPropertyName("adsh")]
    public string? Adsh { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("file_date")]
    public string? FiledAt { get; set; }

    [JsonPropertyName("period_of_report")]
    public string? PeriodOfReport { get; set; }
}

internal sealed class EdgarTicker
{
    [JsonPropertyName("cik_str")]
    public string? CikStr { get; set; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

#endregion
