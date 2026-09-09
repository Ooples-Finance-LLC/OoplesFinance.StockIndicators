using System.Text.Json;
using OoplesFinance.StockIndicators.Builder.Trading;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Pins how a raw Alpaca account payload maps onto <see cref="BrokerAccount"/>.
/// </summary>
/// <remarks>
/// These two decisions are the reason the mapping is tested at all. Which identifier wins changes
/// the identity of every account for anything that keyed on it, and what an absent blocked-flag
/// means decides whether a broker with unknown status reads as tradable. The broker exists in the
/// first place because Alpaca omits fields it declares as required, so "absent" is not theoretical.
/// </remarks>
public class AlpacaAccountMappingTests
{
    private static AlpacaBroker.RawAlpacaAccount Parse(string json) =>
        JsonSerializer.Deserialize<AlpacaBroker.RawAlpacaAccount>(json)!;

    // ---------------------------------------------------------------- identity

    [Fact]
    public void AccountId_PrefersTheIdGuidOverAccountNumber()
    {
        var raw = Parse("""
            {"id":"904837e3-3b76-47ec-b432-046db621571b","account_number":"PA123456"}
            """);

        var account = AlpacaBroker.MapAccount(raw, isPaper: true);

        // The SDK's account.AccountId is the "id" GUID; preferring account_number would silently
        // re-identify every account.
        Assert.Equal("904837e3-3b76-47ec-b432-046db621571b", account.AccountId);
    }

    [Fact]
    public void AccountId_FallsBackToAccountNumberWhenIdIsAbsent()
    {
        var account = AlpacaBroker.MapAccount(Parse("""{"account_number":"PA123456"}"""), isPaper: true);

        Assert.Equal("PA123456", account.AccountId);
    }

    [Fact]
    public void AccountId_ThrowsWhenNeitherIdentifierIsPresent()
    {
        // Better a loud failure than a placeholder that reads like a real account id.
        var ex = Assert.Throws<InvalidOperationException>(
            () => AlpacaBroker.MapAccount(Parse("""{"equity":"1000"}"""), isPaper: true));

        Assert.Contains("account_number", ex.Message);
    }

    // ---------------------------------------------------------------- safety flag

    [Fact]
    public void TradingEnabled_IsTrueOnlyWhenBothFlagsAreExplicitlyFalse()
    {
        var raw = Parse("""{"id":"a","trading_blocked":false,"account_blocked":false}""");

        Assert.True(AlpacaBroker.MapAccount(raw, isPaper: true).TradingEnabled);
    }

    [Theory]
    [InlineData("""{"id":"a","trading_blocked":true,"account_blocked":false}""")]
    [InlineData("""{"id":"a","trading_blocked":false,"account_blocked":true}""")]
    public void TradingEnabled_IsFalseWhenEitherFlagIsSet(string json)
    {
        Assert.False(AlpacaBroker.MapAccount(Parse(json), isPaper: true).TradingEnabled);
    }

    [Theory]
    // The case that motivated this: Alpaca omitting a field it declares as required.
    [InlineData("""{"id":"a"}""")]
    [InlineData("""{"id":"a","trading_blocked":false}""")]
    [InlineData("""{"id":"a","account_blocked":false}""")]
    [InlineData("""{"id":"a","trading_blocked":null,"account_blocked":null}""")]
    public void TradingEnabled_FailsClosedWhenAFlagIsMissing(string json)
    {
        // A missing flag is unknown status, not permission. With non-nullable bools this read as
        // "trading enabled", which is the wrong way to be wrong about a safety flag.
        Assert.False(AlpacaBroker.MapAccount(Parse(json), isPaper: true).TradingEnabled);
    }

    // ---------------------------------------------------------------- money

    [Fact]
    public void MonetaryFieldsAreParsedFromAlpacaStrings()
    {
        var raw = Parse("""
            {"id":"a","equity":"12345.67","last_equity":"12000.00","cash":"500.25",
             "buying_power":"2000.50","long_market_value":"11000.00","short_market_value":"-250.00"}
            """);

        var account = AlpacaBroker.MapAccount(raw, isPaper: true);

        Assert.Equal(12345.67m, account.Equity);
        // "cash" is the same field the SDK's account.TradableCash reads, so this is unchanged.
        Assert.Equal(500.25m, account.Cash);
        Assert.Equal(2000.50m, account.BuyingPower);
        Assert.Equal(10750.00m, account.PortfolioValue);
        Assert.Equal(345.67m, account.DayPnL);
    }

    [Fact]
    public void AbsentOrUnparseableMonetaryFieldsBecomeZeroRatherThanThrowing()
    {
        var account = AlpacaBroker.MapAccount(Parse("""{"id":"a","equity":"not-a-number"}"""), isPaper: true);

        Assert.Equal(0m, account.Equity);
        Assert.Equal(0m, account.Cash);
        Assert.Equal(0.0, account.DayPnLPercent);
    }
}
