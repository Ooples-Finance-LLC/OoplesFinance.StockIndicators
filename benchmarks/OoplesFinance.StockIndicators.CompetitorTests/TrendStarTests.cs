using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TrendStarTests
{
    public static IEnumerable<object[]> Names =>
        TrendStarComparison.Names.Select(n => new object[] { n });
    public static IEnumerable<object[]> Cases =>
        TrendStarComparison.Names.SelectMany(n =>
            Enumerable.Range(0, TrendStarComparison.Cases.Length).Select(i => new object[] { n, i })
        );

    [Theory, MemberData(nameof(Cases))]
    public void GoldenMidpointGapBodyAndDojiDefinitions(string name, int index)
    {
        var c = TrendStarComparison.Cases[index];
        var expected = TrendStarComparison.Kind(name)
            is TrendStarKind.MorningDoji
                or TrendStarKind.EveningDoji
            ? c.Doji
            : c.Plain;
        foreach (var trend in new[] { 1, 3, 5 })
        {
            var data = TrendStarComparison.Golden(c, name, trend);
            var pair = TrendStarComparison.Pair(name);
            Assert.Equal(expected, pair.Ooples(data, trend).Outputs["Value"].Values[^1]);
            Assert.Equal(expected, pair.Competitor(data, trend).Outputs["Value"].Values[^1]);
            ComparisonVerifier.Check(pair, data, trend);
        }
    }

    [Theory, MemberData(nameof(Names))]
    public void ExactAndNativeDecimalRoundingRemainIndependentlyChecked(string name)
    {
        const decimal tolerance = .1666666666666666666666666667m;
        var c = TrendStarComparison.Cases.Single(c => c.Name == "upper midpoint boundary");
        var data = TrendStarComparison.Golden(c, name, shift: -2);
        var pair = TrendStarComparison.Pair(name, tolerance: tolerance);
        Assert.Equal(1, pair.Ooples(data, 3).Outputs["Value"].Values[^1]);
        Assert.Equal(1, pair.Reference!(data, 3).Outputs["Value"].Values[^1]);
        Assert.Equal(0, pair.Competitor(data, 3).Outputs["Value"].Values[^1]);
        Assert.Equal(0, pair.CompetitorReference!(data, 3).Outputs["Value"].Values[^1]);
        ComparisonVerifier.Check(pair, data, 3);
        var corrupt = pair with
        {
            Competitor = (_, _) =>
                new ComparisonSeries(2, Enumerable.Repeat(99d, data.Count).ToArray()),
        };
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(corrupt, data, 3));
        corrupt = pair with { Library = (_, _) => new ComparisonSeries(2, new double[data.Count]) };
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(corrupt, data, 3));
    }

    [Theory, MemberData(nameof(Names))]
    public void ZeroMidpointIsNotAMatchAndPinsNativeUndefinedRatio(string name)
    {
        var c = TrendStarComparison.Cases.Single(c => c.Name == "signal");
        var data = TrendStarComparison.Golden(c, name, shift: -5);
        var pair = TrendStarComparison.Pair(name);
        Assert.Equal(0, pair.Ooples(data, 3).Outputs["Value"].Values[^1]);
        Assert.Equal(0, pair.Reference!(data, 3).Outputs["Value"].Values[^1]);
        Assert.Throws<DivideByZeroException>(() => pair.Competitor(data, 3));
        Assert.Throws<DivideByZeroException>(() => pair.CompetitorReference!(data, 3));
    }

    [Theory, MemberData(nameof(Names))]
    public void ParametersAndTupleGenericRoutes(string name)
    {
        var c = TrendStarComparison.Cases.Single(c => c.Name == "signal");
        var data = TrendStarComparison.Golden(c, name);
        var inputs = data.Candles.Select(b => (b.Open, b.High, b.Low, b.Close)).ToArray();
        foreach (var period in new[] { 1, 7, 20 })
        foreach (var q in new[] { 0m, .75m, 1m })
        foreach (var tolerance in new[] { 0m, .1m, 1m, decimal.MaxValue })
        {
            var pair = TrendStarComparison.Pair(name, period, .25m, q, .25m, tolerance);
            ComparisonVerifier.Check(pair, data, 3);
        }
        IEnumerable<bool?> tuple = name switch
        {
            "MorningStar" => new TC.MorningStarByTuple(inputs).Compute(),
            "EveningStar" => new TC.EveningStarByTuple(inputs).Compute(),
            "EveningDojiStar" => new TC.EveningDojiStarByTuple(inputs).Compute(),
            _ => new TC.MoringinDojiStarByTuple(inputs).Compute(),
        };
        IEnumerable<bool?> generic = name switch
        {
            "MorningStar" => new TC.MorningStar<(decimal, decimal, decimal, decimal), bool?>(
                inputs,
                x => x
            ).Compute(),
            "EveningStar" => new TC.EveningStar<(decimal, decimal, decimal, decimal), bool?>(
                inputs,
                x => x
            ).Compute(),
            "EveningDojiStar" => new TC.EveningDojiStar<
                (decimal, decimal, decimal, decimal),
                bool?
            >(inputs, x => x).Compute(),
            _ => new TC.MorningDojiStar<(decimal, decimal, decimal, decimal), bool?>(
                inputs,
                x => x
            ).Compute(),
        };
        var expected = TrendStarComparison.Pair(name).Competitor(data, 3).Outputs["Value"].Values;
        Assert.Equal(
            expected,
            tuple.Select(v =>
                v.HasValue
                    ? v.Value
                        ? 1d
                        : 0
                    : double.NaN
            )
        );
        Assert.Equal(
            expected,
            generic.Select(v =>
                v.HasValue
                    ? v.Value
                        ? 1d
                        : 0
                    : double.NaN
            )
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferenceLifecycleAndHugePeriods(string name)
    {
        foreach (
            var (trend, period, tolerance) in new[]
            {
                (1, 1, 0m),
                (3, 7, .125m),
                (5, 20, decimal.MaxValue),
                (int.MaxValue, int.MaxValue, .1m),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(TrendStarPattern),
                    name,
                    () => TrendStarComparison.Create(name, trend, period, tolerance: tolerance)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendStarComparison.Create(name, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendStarComparison.Create(name, period: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendStarComparison.Create(name, tolerance: -1)
        );
        foreach (var q in new[] { -.1m, 1.1m })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrendStarComparison.Create(name, shortQ: q)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrendStarComparison.Create(name, longQ: q)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrendStarComparison.Create(name, doji: q)
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrendStarPattern((TrendStarKind)99));
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeSubnormalAndNegativeMidpointsRetainSignals(string name)
    {
        var data = TrendStarComparison.Golden(
            TrendStarComparison.Cases.Single(c => c.Name == "signal"),
            name
        );
        foreach (var scale in new[] { 8 * double.Epsilon, Math.ScaleB(1d, 1019) })
        {
            var bars = data
                .IndicatorBars.Select(b => new Bar(
                    b.Time,
                    b.Open * scale,
                    b.High * scale,
                    b.Low * scale,
                    b.Close * scale,
                    1
                ))
                .ToArray();
            var indicator = TrendStarComparison.Create(name);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            Assert.Equal(1, run[indicator.Outputs[0]].ToArray()[^1]);
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task FirstBodyMidpointMayOverflowBeforeCancellation(string name)
    {
        var data = TrendStarComparison.Golden(
            TrendStarComparison.Cases.Single(c => c.Name == "signal"),
            name
        );
        var sign = Math.Sign(data.IndicatorBars[^3].Open);
        double Map(double value) => (sign * .5 + value / 64) * double.MaxValue;
        var bars = data
            .IndicatorBars.Select(b => new Bar(
                b.Time,
                Map(b.Open),
                Map(b.High),
                Map(b.Low),
                Map(b.Close),
                1
            ))
            .ToArray();
        Assert.True(double.IsInfinity(bars[^3].Open + bars[^3].Close));
        var indicator = TrendStarComparison.Create(name);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(1, run[indicator.Outputs[0]].ToArray()[^1]);
    }

    [Fact]
    public void FixtureFitsExistingCandlePairs()
    {
        var data = TrendStarComparison.Fixture(3);
        foreach (var pair in ComparisonPairs.All.Where(p => p.Id.Contains(".Candle")))
            ComparisonVerifier.Check(pair, data, 3);
    }
}
