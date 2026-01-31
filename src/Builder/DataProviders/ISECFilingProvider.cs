using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators.Builder.ML.Sentiment;

namespace OoplesFinance.StockIndicators.Builder.DataProviders;

/// <summary>
/// Interface for SEC filing data providers.
/// Provides access to SEC EDGAR filings for analysis.
/// </summary>
public interface ISECFilingProvider : IDisposable
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether the provider is connected and ready.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets SEC filings for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol.</param>
    /// <param name="formTypes">Optional filter for form types (10-K, 10-Q, 8-K, etc.).</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="limit">Maximum number of filings to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of SEC filings.</returns>
    Task<IReadOnlyList<SECFilingData>> GetFilingsAsync(
        string symbol,
        IEnumerable<string>? formTypes = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific filing by accession number.
    /// </summary>
    /// <param name="accessionNumber">The SEC accession number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The SEC filing.</returns>
    Task<SECFilingData?> GetFilingByAccessionAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full content of a filing.
    /// </summary>
    /// <param name="accessionNumber">The SEC accession number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The filing content as text.</returns>
    Task<string> GetFilingContentAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets extracted sections from a filing.
    /// </summary>
    /// <param name="accessionNumber">The SEC accession number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of filing sections.</returns>
    Task<IReadOnlyList<FilingSection>> GetFilingSectionsAsync(
        string accessionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets insider transactions (Form 4) for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of insider transactions.</returns>
    Task<IReadOnlyList<InsiderTransaction>> GetInsiderTransactionsAsync(
        string symbol,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets institutional holdings (13F) for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of institutional holdings.</returns>
    Task<IReadOnlyList<InstitutionalHolding>> GetInstitutionalHoldingsAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams new filings in real-time for the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to monitor.</param>
    /// <param name="formTypes">Optional filter for form types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of new filings.</returns>
    IAsyncEnumerable<SECFilingData> StreamNewFilingsAsync(
        IEnumerable<string> symbols,
        IEnumerable<string>? formTypes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent filings across all companies.
    /// </summary>
    /// <param name="formTypes">Optional filter for form types.</param>
    /// <param name="limit">Maximum number of filings to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent filings.</returns>
    Task<IReadOnlyList<SECFilingData>> GetRecentFilingsAsync(
        IEnumerable<string>? formTypes = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets company information by CIK.
    /// </summary>
    /// <param name="cik">The Central Index Key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Company information.</returns>
    Task<CompanyInfo?> GetCompanyInfoAsync(
        string cik,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up CIK by ticker symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The CIK if found.</returns>
    Task<string?> LookupCikAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an SEC filing from a data provider.
/// This is the provider-level model that feeds into ML.Sentiment.SECFiling for analysis.
/// </summary>
public sealed class SECFilingData
{
    /// <summary>Gets or sets the SEC accession number (unique identifier).</summary>
    public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the stock symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the Central Index Key (CIK).</summary>
    public string Cik { get; set; } = string.Empty;

    /// <summary>Gets or sets the company name.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Gets or sets the form type (10-K, 10-Q, 8-K, etc.).</summary>
    public string FormType { get; set; } = string.Empty;

    /// <summary>Gets or sets when the filing was filed.</summary>
    public DateTime FiledAt { get; set; }

    /// <summary>Gets or sets when the filing was accepted by SEC.</summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>Gets or sets the filing period (for 10-K/10-Q).</summary>
    public string? Period { get; set; }

    /// <summary>Gets or sets the primary document URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the filing index URL.</summary>
    public string? IndexUrl { get; set; }

    /// <summary>Gets or sets the description/summary.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the primary document filename.</summary>
    public string? PrimaryDocument { get; set; }

    /// <summary>Gets or sets the size of the filing in bytes.</summary>
    public long? FileSize { get; set; }

    /// <summary>Gets or sets the fiscal year end.</summary>
    public string? FiscalYearEnd { get; set; }

    /// <summary>Gets or sets when this filing was fetched.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the data provider.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Converts this provider model to the ML sentiment analyzer model.
    /// </summary>
    public SECFiling ToSentimentAnalyzerModel(string content)
    {
        var filingType = FormType?.ToUpperInvariant() switch
        {
            "10-K" => FilingType.Form10K,
            "10-Q" => FilingType.Form10Q,
            "8-K" => FilingType.Form8K,
            "4" => FilingType.Form4,
            "S-1" => FilingType.FormS1,
            "DEF 14A" => FilingType.FormDEF14A,
            _ => FilingType.Other
        };

        return new SECFiling
        {
            Symbol = Symbol,
            FilingType = filingType,
            FilingDate = FiledAt,
            Period = Period ?? string.Empty,
            Content = content,
            Url = Url
        };
    }
}

/// <summary>
/// Represents a section extracted from an SEC filing.
/// </summary>
public sealed class FilingSection
{
    /// <summary>Gets or sets the section name (e.g., "Risk Factors", "MD&A").</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>Gets or sets the section item number (e.g., "1A", "7").</summary>
    public string? ItemNumber { get; set; }

    /// <summary>Gets or sets the section content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the start position in the document.</summary>
    public int StartPosition { get; set; }

    /// <summary>Gets or sets the end position in the document.</summary>
    public int EndPosition { get; set; }

    /// <summary>Gets or sets the word count.</summary>
    public int WordCount { get; set; }
}

/// <summary>
/// Represents an insider transaction (Form 4).
/// </summary>
public sealed class InsiderTransaction
{
    /// <summary>Gets or sets the accession number.</summary>
    public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the stock symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the insider's name.</summary>
    public string InsiderName { get; set; } = string.Empty;

    /// <summary>Gets or sets the insider's title/relationship.</summary>
    public string? InsiderTitle { get; set; }

    /// <summary>Gets or sets the insider type.</summary>
    public InsiderRelationship Relationship { get; set; }

    /// <summary>Gets or sets the transaction date.</summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>Gets or sets the transaction type.</summary>
    public InsiderTransactionType TransactionType { get; set; }

    /// <summary>Gets or sets the security type.</summary>
    public string SecurityType { get; set; } = "Common Stock";

    /// <summary>Gets or sets the number of shares.</summary>
    public decimal Shares { get; set; }

    /// <summary>Gets or sets the price per share.</summary>
    public decimal? PricePerShare { get; set; }

    /// <summary>Gets or sets the shares owned after transaction.</summary>
    public decimal? SharesOwnedAfter { get; set; }

    /// <summary>Gets or sets whether this is a direct or indirect holding.</summary>
    public OwnershipType OwnershipType { get; set; } = OwnershipType.Direct;

    /// <summary>Gets or sets the transaction code (P, S, A, D, etc.).</summary>
    public string? TransactionCode { get; set; }

    /// <summary>Gets or sets when the form was filed.</summary>
    public DateTime FiledAt { get; set; }
}

/// <summary>
/// Insider relationship types.
/// </summary>
public enum InsiderRelationship
{
    /// <summary>Chief Executive Officer.</summary>
    CEO,

    /// <summary>Chief Financial Officer.</summary>
    CFO,

    /// <summary>Chief Operating Officer.</summary>
    COO,

    /// <summary>Board Director.</summary>
    Director,

    /// <summary>Other Officer.</summary>
    Officer,

    /// <summary>10% beneficial owner.</summary>
    TenPercentOwner,

    /// <summary>Other insider.</summary>
    Other
}

/// <summary>
/// Insider transaction types.
/// </summary>
public enum InsiderTransactionType
{
    /// <summary>Open market purchase.</summary>
    Purchase,

    /// <summary>Open market sale.</summary>
    Sale,

    /// <summary>Option exercise.</summary>
    OptionExercise,

    /// <summary>Grant or award.</summary>
    Grant,

    /// <summary>Gift transaction.</summary>
    Gift,

    /// <summary>Automatic transaction (401k, ESPP, etc.).</summary>
    Automatic,

    /// <summary>Other transaction type.</summary>
    Other
}

/// <summary>
/// Ownership types.
/// </summary>
public enum OwnershipType
{
    /// <summary>Direct ownership.</summary>
    Direct,

    /// <summary>Indirect ownership (through trust, family, etc.).</summary>
    Indirect
}

/// <summary>
/// Represents an institutional holding (13F).
/// </summary>
public sealed class InstitutionalHolding
{
    /// <summary>Gets or sets the institution CIK.</summary>
    public string InstitutionCik { get; set; } = string.Empty;

    /// <summary>Gets or sets the institution name.</summary>
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>Gets or sets the stock symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of shares held.</summary>
    public long SharesHeld { get; set; }

    /// <summary>Gets or sets the market value in USD.</summary>
    public decimal MarketValue { get; set; }

    /// <summary>Gets or sets the share change from previous quarter.</summary>
    public long ShareChange { get; set; }

    /// <summary>Gets or sets the percentage change from previous quarter.</summary>
    public decimal PercentChange { get; set; }

    /// <summary>Gets or sets the percentage of portfolio.</summary>
    public decimal PortfolioPercent { get; set; }

    /// <summary>Gets or sets the reporting period end date.</summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>Gets or sets when the 13F was filed.</summary>
    public DateTime FiledAt { get; set; }
}

/// <summary>
/// Company information from SEC.
/// </summary>
public sealed class CompanyInfo
{
    /// <summary>Gets or sets the CIK.</summary>
    public string Cik { get; set; } = string.Empty;

    /// <summary>Gets or sets the company name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ticker symbols.</summary>
    public IReadOnlyList<string> Tickers { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the exchange(s).</summary>
    public IReadOnlyList<string> Exchanges { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the SIC code.</summary>
    public string? SicCode { get; set; }

    /// <summary>Gets or sets the SIC description.</summary>
    public string? SicDescription { get; set; }

    /// <summary>Gets or sets the state of incorporation.</summary>
    public string? StateOfIncorporation { get; set; }

    /// <summary>Gets or sets the fiscal year end (MMDD format).</summary>
    public string? FiscalYearEnd { get; set; }

    /// <summary>Gets or sets the business address.</summary>
    public string? BusinessAddress { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string? Phone { get; set; }
}

/// <summary>
/// Options for SEC filing providers.
/// </summary>
public sealed class SECFilingProviderOptions
{
    /// <summary>Gets or sets the API key if required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the base URL override.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets the max requests per second (EDGAR limit is 10).</summary>
    public int MaxRequestsPerSecond { get; set; } = 10;

    /// <summary>Gets or sets the user agent (required by SEC).</summary>
    public string UserAgent { get; set; } = "OoplesFinance.StockIndicators contact@example.com";

    /// <summary>Gets or sets retry options.</summary>
    public RetryOptions RetryOptions { get; set; } = new();

    /// <summary>Gets or sets whether to cache CIK lookups.</summary>
    public bool CacheCikLookups { get; set; } = true;

    /// <summary>Gets or sets whether to extract sections automatically.</summary>
    public bool AutoExtractSections { get; set; } = true;
}

/// <summary>
/// Common SEC form types.
/// </summary>
public static class SECFormTypes
{
    /// <summary>Annual report.</summary>
    public const string Form10K = "10-K";

    /// <summary>Quarterly report.</summary>
    public const string Form10Q = "10-Q";

    /// <summary>Current report (material events).</summary>
    public const string Form8K = "8-K";

    /// <summary>Insider trading report.</summary>
    public const string Form4 = "4";

    /// <summary>Institutional holdings report.</summary>
    public const string Form13F = "13F-HR";

    /// <summary>Proxy statement.</summary>
    public const string FormDEF14A = "DEF 14A";

    /// <summary>IPO registration.</summary>
    public const string FormS1 = "S-1";

    /// <summary>Beneficial ownership report.</summary>
    public const string FormSC13D = "SC 13D";

    /// <summary>Passive beneficial ownership report.</summary>
    public const string FormSC13G = "SC 13G";

    /// <summary>Annual and quarterly forms.</summary>
    public static readonly string[] PeriodicForms = { Form10K, Form10Q };

    /// <summary>All insider-related forms.</summary>
    public static readonly string[] InsiderForms = { Form4, FormSC13D, FormSC13G };

    /// <summary>All common filing types.</summary>
    public static readonly string[] AllCommonForms =
    {
        Form10K, Form10Q, Form8K, Form4, Form13F, FormDEF14A, FormS1
    };
}
