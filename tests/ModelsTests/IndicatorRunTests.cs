using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The first end-to-end use of the v2 surface: objects in, values out.
/// </summary>
/// <remarks>
/// The numbers matter more than the shape here. The run is backed by the same evaluator the v1 calculations
/// use, so these assertions are really asking whether the typed surface reaches the arithmetic the library
/// has always produced - a new API returning different numbers would be a regression with better syntax.
/// </remarks>
public sealed class IndicatorRunTests
{
    private const int BarCount = 300;

    private static IBarSource Source() => Bars.From(SeededBars(BarCount));

    private static IReadOnlyList<Bar> SeededBars(int count)
    {
        var random = new Random(11);
        var bars = new List<Bar>(count);
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        var last = 100d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(
                start.AddMinutes(i),
                open,
                Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()),
                close,
                random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }

    [Fact]
    public async Task AnIndicatorObjectGoesInAndItsSeriesComesOut()
    {
        var rsi = new Rsi(14);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Source())
            .ConfigureIndicators(rsi)
            .BuildAsync();

        run.BarCount.Should().Be(BarCount);
        run.IsComplete.Should().BeTrue("a finite source has run out by the time BuildAsync returns");

        var values = run[rsi].ToArray();
        values.Should().HaveCount(BarCount, "an indicator returns one value per bar");
        values.Should().Contain(v => v != 0, "an RSI over a random walk is not flat");
        values.Should().OnlyContain(v => !double.IsNaN(v) && !double.IsInfinity(v));
    }

    [Fact]
    public async Task SeveralIndicatorsShareOneReadOfTheBars()
    {
        var rsi = new Rsi(14);
        var cci = new Cci(20);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Source())
            .ConfigureIndicators(rsi, cci)
            .BuildAsync();

        run[rsi].ToArray().Should().HaveCount(BarCount);
        run[cci].ToArray().Should().HaveCount(BarCount);
        run[rsi].ToArray().Should().NotEqual(run[cci].ToArray(), "they are different indicators");
    }

    [Fact]
    public async Task TheTypedSurfaceAgreesWithTheCalculationItStandsFor()
    {
        var bars = SeededBars(BarCount);
        var rsi = new Rsi(14);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(rsi)
            .BuildAsync();

        // The same bars through the v1 surface. Bit for bit, not within a tolerance: the typed surface is a
        // different way to ask for the same computation, not a different computation.
        var stockData = new StockData(
            bars.Select(b => b.Open).ToList(),
            bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(),
            bars.Select(b => b.Close).ToList(),
            bars.Select(b => b.Volume).ToList(),
            bars.Select(b => b.Time).ToList());

        var expected = stockData.CalculateRelativeStrengthIndex(length: 14).CustomValuesList;

        run[rsi].ToArray().Should().Equal(expected);
    }

    [Fact]
    public async Task AnIndicatorThatWasNotConfiguredIsNamedRatherThanReturningNothing()
    {
        var configured = new Rsi(14);
        var notConfigured = new Cci(20);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Source())
            .ConfigureIndicators(configured)
            .BuildAsync();

        var act = () => run[notConfigured].ToArray();

        act.Should().Throw<KeyNotFoundException>().WithMessage("*ConfigureIndicators*");
    }

    [Fact]
    public async Task BuildingWithoutASourceSaysSo()
    {
        var act = async () => await new StockIndicatorBuilder()
            .ConfigureIndicators(new Rsi(14))
            .BuildAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ConfigureSource*");
    }
}
