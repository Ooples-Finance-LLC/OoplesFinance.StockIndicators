using System.Net;
using System.Text;
using FluentAssertions;
using OoplesFinance.StockIndicators.DevConsole;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

public class FXMacroDataExampleTests
{
    [Fact]
    public void ParseDailyFxPreservesOhlcAndUsesValueFallback()
    {
        const string Json = """
            {
              "data": [
                {"date":"2024-01-03","open":"1.1000","high":1.1200,"low":1.0900,"close":1.1100},
                {"date":"2024-01-02","val":"1.0950"},
                {"date":"invalid","val":1.0000}
              ]
            }
            """;

        var rows = FXMacroDataClient.ParseDailyFx(Json);

        rows.Should().HaveCount(2);
        rows[0].Date.Should().Be(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        rows[0].Open.Should().Be(1.0950);
        rows[0].Close.Should().Be(1.0950);
        rows[0].Volume.Should().Be(0d);
        rows[1].Open.Should().Be(1.1000);
        rows[1].High.Should().Be(1.1200);
        rows[1].Low.Should().Be(1.0900);
        rows[1].Close.Should().Be(1.1100);
    }

    [Fact]
    public async Task GetDailyFxBuildsBoundedRequestAndParsesRows()
    {
        var handler = new RecordingHandler(
            """{"data":[{"date":"2024-01-02","val":1.095}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new FXMacroDataClient(httpClient, "test-key", "https://example.test/v1/");

        var rows = await client.GetDailyFxAsync(
            "EUR",
            "USD",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        // The key is sent as a header, and must not appear in the URL - a query string is recorded
        // by origin logs, proxies and APM traces, and by anything that echoes the request URI.
        handler.RequestUri.Should().Be(
            new Uri("https://example.test/v1/forex/eur/usd?start_date=2024-01-01&end_date=2024-01-31"));
        handler.RequestUri!.Query.Should().NotContain("api_key");
        handler.RequestUri.ToString().Should().NotContain("test-key");
        handler.ApiKeyHeader.Should().Be("test-key");
        rows.Should().ContainSingle();
        rows[0].Close.Should().Be(1.095);
    }

    [Theory]
    [InlineData("EU", "USD")]
    [InlineData("EUR", "US1")]
    public void BuildUriRejectsInvalidCurrencyCodes(string baseCurrency, string quoteCurrency)
    {
        using var httpClient = new HttpClient();
        var client = new FXMacroDataClient(httpClient, "test-key");

        var action = () => client.BuildUri(
            baseCurrency,
            quoteCurrency,
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        action.Should().Throw<ArgumentException>();
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        internal Uri? RequestUri { get; private set; }

        internal string? ApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("X-API-Key", out var values)
                ? string.Join(",", values)
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
