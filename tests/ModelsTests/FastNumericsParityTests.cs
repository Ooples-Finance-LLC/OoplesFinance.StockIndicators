using OoplesFinance.StockIndicators.Builder;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Indicators.Fast;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Holds every fast kernel to the exact route: same published series to within floating-point rounding, on
/// every output, from both source shapes and in both history modes.
/// </summary>
[Collection("IndicatorValuesDispatch")]
public sealed class FastNumericsParityTests
{
    // Relative to the larger of the exact value and the price scale, so a value that is legitimately near
    // zero is not held to a relative bound it cannot meet.
    private const double Tolerance = 1e-9;

    /// <summary>One configuration per kernel branch. Every type with a fast kernel must appear here.</summary>
    public static IEnumerable<(string Name, Func<IIndicator> Create)> Cases() =>
    [
        ("Sma(1)", () => new Sma(1)),
        ("Sma(20)", () => new Sma(20)),
        ("Sma(400)", () => new Sma(400)),
        ("FirstValueEma(1)", () => new FirstValueEma(1)),
        ("FirstValueEma(20)", () => new FirstValueEma(20)),
        ("SeededExponentialAverage(20,2,Shared)", () => new SeededExponentialAverage(20, 2, ExponentialSeedMode.Shared)),
        ("SeededExponentialAverage(20,3,Shared)", () => new SeededExponentialAverage(20, 3, ExponentialSeedMode.Shared)),
        ("SeededExponentialAverage(20,2,Cascaded)", () => new SeededExponentialAverage(20, 2, ExponentialSeedMode.Cascaded)),
        ("SeededExponentialAverage(5,3,Cascaded)", () => new SeededExponentialAverage(5, 3, ExponentialSeedMode.Cascaded)),
        ("SeededExponentialAverage(1,3,Cascaded)", () => new SeededExponentialAverage(1, 3, ExponentialSeedMode.Cascaded)),
    ];

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in Cases()) data.Add(name);
        return data;
    }

    [Fact]
    public void EveryFastKernelTypeHasAParityCase()
    {
        var covered = Cases().Select(c => c.Create().GetType()).ToHashSet();
        var missing = FastExecution.KernelTypes.Where(type => !covered.Contains(type)).Select(type => type.Name).ToArray();
        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task FastAgreesWithExactOnEveryOutput(string name)
    {
        var create = Cases().Single(c => c.Name == name).Create;
        foreach (var (fixture, bars) in Fixtures())
        {
            var exactIndicator = create();
            using var exact = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(exactIndicator).BuildAsync();
            foreach (var columnar in new[] { false, true })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = create();
                var source = columnar
                    ? Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
                        bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(),
                        bars.Select(b => b.Volume).ToArray(), bars.Select(b => b.Time).ToArray())
                    : Bars.From(bars);
                var builder = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(indicator)
                    .ConfigureNumerics(IndicatorNumerics.Fast).ConfigureHistory(history);
                using var fast = await builder.BuildAsync();
                var where = $"{name} / {fixture} / columnar={columnar} / {history}";
                Assert.True(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal), where + ": did not take the fast route");
                Assert.Equal(bars.Length, fast.BarCount);
                Assert.Equal(exactIndicator.Outputs.Count, indicator.Outputs.Count);
                for (var slot = 0; slot < indicator.Outputs.Count; slot++)
                {
                    var expected = exact[exactIndicator.Outputs[slot]];
                    var actual = fast[indicator.Outputs[slot]];
                    Assert.Equal(expected.Length, actual.Length);
                    for (var i = 0; i < expected.Length; i++)
                    {
                        if (double.IsNaN(expected[i]))
                        {
                            Assert.True(double.IsNaN(actual[i]), $"{where}: slot {slot}, bar {i} expected NaN, was {actual[i]:R}");
                            continue;
                        }
                        var scale = Math.Max(Math.Abs(expected[i]), Math.Abs(bars[i].Close));
                        Assert.True(Math.Abs(expected[i] - actual[i]) <= Tolerance * Math.Max(scale, 1e-300),
                            $"{where}: slot {slot}, bar {i} expected {expected[i]:R}, was {actual[i]:R}");
                    }
                }
                if (bars.Length != 0)
                    Assert.Equal(bars[^1].Close, fast.Latest.Bar.Close);
            }
        }
    }

    [Fact]
    public async Task FastValidatesOnlyTheFieldsItReads()
    {
        var bars = Walk(64, 11);
        bars[10] = new Bar(bars[10].Time, bars[10].Open, bars[10].High, bars[10].Low, bars[10].Close, double.NaN);
        var sma = new Sma(5);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma)
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        Assert.Equal(bars.Length, run[sma].Length);

        bars[12] = new Bar(bars[12].Time, bars[12].Open, bars[12].High, bars[12].Low, double.PositiveInfinity, 1);
        foreach (var columnar in new[] { false, true })
        {
            var source = columnar
                ? Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
                    bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray())
                : Bars.From(bars);
            var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
                .ConfigureSource(source).ConfigureIndicators(new Sma(5)).ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync());
            Assert.Contains("Close must be finite.", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExactStaysTheDefaultAndUnsupportedBuildsKeepTheExactRoute()
    {
        var bars = Walk(64, 3);
        var plain = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(5));
        using (await plain.BuildAsync())
            Assert.False(plain.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));

        // An indicator reading another indicator's series has no fast kernel, so the whole build stays exact.
        var chained = new Sma(5).Of(new Sma(3));
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(chained)
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using (await builder.BuildAsync())
            Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
    }

    private static IEnumerable<(string Name, Bar[] Bars)> Fixtures()
    {
        yield return ("empty", []);
        yield return ("one bar", Walk(1, 5));
        yield return ("shorter than most windows", Walk(7, 5));
        yield return ("walk 300", Walk(300, 17));
        yield return ("walk 2000", Walk(2000, 29));
        yield return ("flat", Enumerable.Range(0, 250).Select(i => new Bar(Day(i), 50, 50, 50, 50, 1000)).ToArray());
    }

    private static DateTime Day(int index) => new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index);

    /// <summary>A deterministic cent-grid random walk with consistent OHLC and varying volume.</summary>
    private static Bar[] Walk(int count, int seed)
    {
        var random = RandomHelper.CreateSeededRandom(seed);
        var bars = new Bar[count];
        double close = 100;
        for (var i = 0; i < count; i++)
        {
            var open = close;
            close = Math.Round(Math.Max(1, close + (random.NextDouble() - .5) * 2), 2);
            var high = Math.Round(Math.Max(open, close) + random.NextDouble(), 2);
            var low = Math.Round(Math.Max(.01, Math.Min(open, close) - random.NextDouble()), 2);
            bars[i] = new Bar(Day(i), open, high, low, close, 1000 + random.Next(0, 5000));
        }
        return bars;
    }
}
