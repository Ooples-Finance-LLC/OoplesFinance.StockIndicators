using System.Net.Http;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Compliance;

/// <summary>
/// Detects user region for regulatory compliance purposes.
/// Uses IP geolocation and user confirmation.
/// </summary>
public interface IRegionDetector
{
    /// <summary>Detects the user's region based on IP address.</summary>
    Task<RegionInfo> DetectRegionAsync(CancellationToken ct = default);

    /// <summary>Gets the regulatory framework for a region.</summary>
    RegulatoryFramework GetFrameworkForRegion(string countryCode);

    /// <summary>Confirms the user's region (manual override).</summary>
    Task<RegionInfo> ConfirmRegionAsync(string countryCode, CancellationToken ct = default);

    /// <summary>Gets feature restrictions for a region.</summary>
    FeatureRestrictions GetFeatureRestrictions(string countryCode);
}

/// <summary>
/// Implementation of region detection service.
/// </summary>
public sealed class RegionDetector : IRegionDetector
{
    private readonly HttpClient _httpClient;
    private RegionInfo? _cachedRegion;

    public RegionDetector(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<RegionInfo> DetectRegionAsync(CancellationToken ct = default)
    {
        if (_cachedRegion is not null)
        {
            return _cachedRegion;
        }

        try
        {
            // Use a free IP geolocation service
            // Note: CancellationToken overload not available in net461, using basic overload
            var response = await _httpClient.GetStringAsync("https://ipapi.co/json/");
            var geoData = JsonSerializer.Deserialize<IpApiResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (geoData is not null)
            {
                _cachedRegion = new RegionInfo
                {
                    CountryCode = geoData.CountryCode ?? "US",
                    CountryName = geoData.CountryName ?? "United States",
                    Region = geoData.Region ?? string.Empty,
                    City = geoData.City ?? string.Empty,
                    Timezone = geoData.Timezone ?? "America/New_York",
                    Currency = geoData.Currency ?? "USD",
                    IsEU = IsEuropeanUnion(geoData.CountryCode ?? "US"),
                    DetectedAt = DateTime.UtcNow,
                    IsConfirmed = false
                };

                return _cachedRegion;
            }
        }
        catch
        {
            // Fall back to US if detection fails
        }

        _cachedRegion = new RegionInfo
        {
            CountryCode = "US",
            CountryName = "United States",
            Timezone = "America/New_York",
            Currency = "USD",
            IsEU = false,
            DetectedAt = DateTime.UtcNow,
            IsConfirmed = false
        };

        return _cachedRegion;
    }

    public RegulatoryFramework GetFrameworkForRegion(string countryCode)
    {
        // EU countries use MiFID II
        if (IsEuropeanUnion(countryCode))
        {
            return RegulatoryFramework.MiFIDII;
        }

        // US uses SEC/FINRA rules
        if (countryCode == "US")
        {
            return RegulatoryFramework.SEC_FINRA;
        }

        // UK post-Brexit uses FCA
        if (countryCode == "GB")
        {
            return RegulatoryFramework.FCA;
        }

        // Australia uses ASIC
        if (countryCode == "AU")
        {
            return RegulatoryFramework.ASIC;
        }

        // Canada uses IIROC/CSA
        if (countryCode == "CA")
        {
            return RegulatoryFramework.IIROC_CSA;
        }

        // Default to strictest applicable (SEC + MiFID II combined)
        return RegulatoryFramework.International;
    }

    public async Task<RegionInfo> ConfirmRegionAsync(string countryCode, CancellationToken ct = default)
    {
        _cachedRegion = new RegionInfo
        {
            CountryCode = countryCode,
            CountryName = GetCountryName(countryCode),
            IsEU = IsEuropeanUnion(countryCode),
            DetectedAt = DateTime.UtcNow,
            IsConfirmed = true
        };

        return _cachedRegion;
    }

    public FeatureRestrictions GetFeatureRestrictions(string countryCode)
    {
        var restrictions = new FeatureRestrictions();

        // Crypto restrictions by region
        if (CryptoRestrictedCountries.Contains(countryCode))
        {
            restrictions.CryptoTradingAllowed = false;
            restrictions.CryptoRestrictedReason = "Cryptocurrency trading is restricted in your region.";
        }

        // CFD restrictions (US doesn't allow retail CFDs)
        if (countryCode == "US")
        {
            restrictions.CFDsAllowed = false;
            restrictions.CFDRestrictedReason = "CFD trading is not available for US retail investors.";
        }

        // Leverage restrictions
        restrictions.MaxLeverage = countryCode switch
        {
            "US" => 4.0m, // Reg T allows 2:1, day trading 4:1
            _ when IsEuropeanUnion(countryCode) => 30.0m, // ESMA caps at 30:1 for forex
            "AU" => 30.0m, // ASIC matches ESMA
            "GB" => 30.0m, // FCA matches ESMA
            _ => 50.0m // Default
        };

        // Options restrictions
        if (OptionsRestrictedCountries.Contains(countryCode))
        {
            restrictions.OptionsAllowed = false;
            restrictions.OptionsRestrictedReason = "Options trading is not available in your region.";
        }

        // Binary options (banned in EU, UK)
        if (IsEuropeanUnion(countryCode) || countryCode == "GB")
        {
            restrictions.BinaryOptionsAllowed = false;
        }

        return restrictions;
    }

    private static bool IsEuropeanUnion(string countryCode)
    {
        return EUCountries.Contains(countryCode);
    }

    private static string GetCountryName(string countryCode) => countryCode switch
    {
        "US" => "United States",
        "GB" => "United Kingdom",
        "DE" => "Germany",
        "FR" => "France",
        "CA" => "Canada",
        "AU" => "Australia",
        "JP" => "Japan",
        "CH" => "Switzerland",
        _ => countryCode
    };

    private static readonly HashSet<string> EUCountries = new()
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
        "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    private static readonly HashSet<string> CryptoRestrictedCountries = new()
    {
        "CN", // China
        "BD", // Bangladesh
        "EG", // Egypt
        "MA", // Morocco
        "NP", // Nepal
        "PK", // Pakistan
        "QA"  // Qatar
    };

    private static readonly HashSet<string> OptionsRestrictedCountries = new()
    {
        // Add countries where options trading is restricted
    };

    private sealed class IpApiResponse
    {
        public string? CountryCode { get; set; }
        public string? CountryName { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
        public string? Timezone { get; set; }
        public string? Currency { get; set; }
    }
}

#region Types

/// <summary>
/// Information about a user's detected region.
/// </summary>
public sealed record RegionInfo
{
    public string CountryCode { get; init; } = "US";
    public string CountryName { get; init; } = "United States";
    public string Region { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Timezone { get; init; } = "America/New_York";
    public string Currency { get; init; } = "USD";
    public bool IsEU { get; init; }
    public DateTime DetectedAt { get; init; }
    public bool IsConfirmed { get; init; }
}

/// <summary>
/// Regulatory frameworks by region.
/// </summary>
public enum RegulatoryFramework
{
    /// <summary>US Securities and Exchange Commission / FINRA rules.</summary>
    SEC_FINRA,

    /// <summary>EU Markets in Financial Instruments Directive II.</summary>
    MiFIDII,

    /// <summary>UK Financial Conduct Authority (post-Brexit).</summary>
    FCA,

    /// <summary>Australian Securities and Investments Commission.</summary>
    ASIC,

    /// <summary>Investment Industry Regulatory Organization of Canada / Canadian Securities Administrators.</summary>
    IIROC_CSA,

    /// <summary>Combined international standards (strictest of all).</summary>
    International
}

/// <summary>
/// Feature restrictions based on region.
/// </summary>
public sealed class FeatureRestrictions
{
    public bool CryptoTradingAllowed { get; set; } = true;
    public string? CryptoRestrictedReason { get; set; }

    public bool CFDsAllowed { get; set; } = true;
    public string? CFDRestrictedReason { get; set; }

    public bool OptionsAllowed { get; set; } = true;
    public string? OptionsRestrictedReason { get; set; }

    public bool BinaryOptionsAllowed { get; set; } = true;

    public decimal MaxLeverage { get; set; } = 50.0m;
}

#endregion
