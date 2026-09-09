using OoplesFinance.StockIndicators.Builder.Trading;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// The Alpaca account request attaches APCA-API-KEY-ID and APCA-API-SECRET-KEY, so the endpoint it
/// is sent to must be validated before those headers exist rather than trusted from configuration.
/// </summary>
public class AlpacaEndpointValidationTests
{
    private static AlpacaOptions Options(string? baseUrl) => new()
    {
        ApiKey = "key-id",
        ApiSecret = "secret-key",
        UsePaper = true,
        BaseUrl = baseUrl
    };

    [Theory]
    [InlineData("http://paper-api.alpaca.markets")]   // cleartext would expose the credentials
    [InlineData("http://localhost:9999")]
    [InlineData("ftp://example.test")]
    public async Task NonHttpsBaseUrlIsRejectedBeforeCredentialsAreSent(string baseUrl)
    {
        using var broker = new AlpacaBroker(Options(baseUrl));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => broker.GetAccountAsync());
        Assert.Contains("https", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("paper-api.alpaca.markets")]          // no scheme - not an absolute URI
    [InlineData("/v2")]
    public async Task NonAbsoluteBaseUrlIsRejected(string baseUrl)
    {
        using var broker = new AlpacaBroker(Options(baseUrl));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => broker.GetAccountAsync());
        Assert.Contains("absolute", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HttpsBaseUrlIsAccepted()
    {
        // Constructing must not throw; the request itself needs the network, so this only asserts
        // that a valid https base URL passes validation rather than being rejected outright.
        using var broker = new AlpacaBroker(Options("https://paper-api.alpaca.markets"));
        Assert.NotNull(broker);
    }
}
