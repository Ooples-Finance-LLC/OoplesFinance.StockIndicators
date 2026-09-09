namespace OoplesFinance.StockIndicators.Tests.IntegrationTests;

using OoplesFinance.StockIndicators.Builder.Trading;

/// <summary>
/// Resolves the Alpaca paper-trading credentials used by the integration tests.
/// </summary>
/// <remarks>
/// These tests talk to the live Alpaca paper-trading API. A clean checkout has no
/// credentials, so without this gate every one of them fails with
/// <c>RestClientErrorException : Unauthorized</c> and turns CI red for a reason that
/// has nothing to do with the code under test. Tests consult <see cref="Available"/>
/// and skip themselves when the credentials are absent; supplying
/// <c>ALPACA_API_KEY</c> and <c>ALPACA_API_SECRET</c> makes them run for real.
/// </remarks>
internal static class AlpacaTestCredentials
{
    /// <summary>The environment variable holding the Alpaca API key id.</summary>
    public const string ApiKeyVariable = "ALPACA_API_KEY";

    /// <summary>The environment variable holding the Alpaca API secret key.</summary>
    public const string ApiSecretVariable = "ALPACA_API_SECRET";

    /// <summary>The message shown on the skipped tests when credentials are missing.</summary>
    public const string SkipReason =
        "Alpaca paper-trading credentials are not configured. Set the " +
        ApiKeyVariable + " and " + ApiSecretVariable + " environment variables to run these tests.";

    /// <summary>Gets the configured Alpaca API key, or an empty string when unset.</summary>
    public static string ApiKey => Environment.GetEnvironmentVariable(ApiKeyVariable) ?? string.Empty;

    /// <summary>Gets the configured Alpaca API secret, or an empty string when unset.</summary>
    public static string ApiSecret => Environment.GetEnvironmentVariable(ApiSecretVariable) ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether both credentials are present, so the
    /// integration tests can reach the Alpaca API.
    /// </summary>
    public static bool Available =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);

    /// <summary>
    /// Builds the Alpaca options used by the integration tests. Always paper trading.
    /// </summary>
    public static AlpacaOptions CreateOptions() => new()
    {
        ApiKey = ApiKey,
        ApiSecret = ApiSecret,
        UsePaper = true
    };
}
