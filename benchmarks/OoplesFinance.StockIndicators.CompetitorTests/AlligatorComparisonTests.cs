using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AlligatorComparisonTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task IndependentContractsVerifyDelaysLifecycleAndLazyMaximums(
        bool gator,
        int choice
    )
    {
        int[][] settings =
        [
            AlligatorComparison.Default,
            [3, 1, 2, 1, 1, 1],
            [int.MaxValue, 3, int.MaxValue - 1, 2, int.MaxValue - 2, 1],
            [3, int.MaxValue, 2, int.MaxValue - 1, 1, int.MaxValue - 2],
        ];
        var type = gator ? typeof(GatorWithDetails) : typeof(AlligatorWithDetails);
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                type,
                choice.ToString(),
                () => AlligatorComparison.Indicator(gator, settings[choice])
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EachLineAndExpansionFlagStartsAtItsOwnOffset()
    {
        var data = AlligatorComparison.Fixture();
        foreach (var pair in AlligatorComparison.Pairs)
            ComparisonVerifier.Check(pair, data, 20);
        var alligator = AlligatorComparison.Pairs[0].Ooples(data, 20).Outputs;
        foreach (var (name, index) in new[] { ("Jaw", 20), ("Teeth", 12), ("Lips", 7) })
        {
            Assert.All(alligator[name].Present!.Take(index), v => Assert.False(v));
            Assert.All(alligator[name].Present!.Skip(index), v => Assert.True(v));
        }
        var gator = AlligatorComparison.Pairs[1].Ooples(data, 20).Outputs;
        foreach (
            var (name, index) in new[]
            {
                ("Upper", 20),
                ("Lower", 12),
                ("UpperIsExpanding", 21),
                ("LowerIsExpanding", 13),
            }
        )
        {
            Assert.All(gator[name].Present!.Take(index), v => Assert.False(v));
            Assert.All(gator[name].Present!.Skip(index), v => Assert.True(v));
        }
    }

    [Fact]
    public async Task TinyAndMaximumMidpointsSurviveAveragingAndDelays()
    {
        foreach (var price in new[] { double.Epsilon, double.MaxValue, -double.MaxValue })
        {
            var bars = Enumerable
                .Range(0, 8)
                .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), price, price, price, price, 1))
                .ToArray();
            foreach (var gator in new[] { false, true })
            {
                var indicator = AlligatorComparison.Indicator(gator, [3, 1, 2, 1, 1, 1]);
                using var run = await new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator)
                    .BuildAsync();
                var count = gator ? 4 : 3;
                for (var slot = 0; slot < count; slot++)
                {
                    var values = run[indicator.Outputs[slot]].ToArray();
                    var presence = run[indicator.Outputs[slot + count]].ToArray();
                    for (var i = 0; i < bars.Length; i++)
                        Assert.Equal(gator || presence[i] == 0 ? 0 : price, values[i]);
                }
            }
        }
    }

    [Fact]
    public async Task ChainingUsesUpstreamPricesForBothIndicators()
    {
        var data = AlligatorComparison.Fixture();
        foreach (var gator in new[] { false, true })
        {
            var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
            var indicator = AlligatorComparison.Indicator(gator, AlligatorComparison.Default);
            indicator.Of(source);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(source, indicator)
                .BuildAsync();
            var prices = run[source.Value].ToArray();
            var expected = AlligatorComparison
                .Create(gator, AlligatorComparison.Default)
                .Ooples(
                    CompetitorData.FromOhlcv(
                        prices,
                        prices,
                        prices,
                        prices,
                        new double[prices.Length]
                    ),
                    20
                )
                .Outputs;
            var names = gator ? AlligatorComparison.GatorNames : AlligatorComparison.AlligatorNames;
            for (var slot = 0; slot < names.Length; slot++)
            {
                var values = run[indicator.Outputs[slot]].ToArray();
                var flags = run[indicator.Outputs[slot + names.Length]].ToArray();
                for (var i = 0; i < prices.Length; i++)
                {
                    Assert.Equal(expected[names[slot]].Present![i], flags[i] > 0);
                    if (flags[i] > 0)
                        Assert.Equal(expected[names[slot]].Values[i], values[i]);
                }
            }
        }
    }

    [Fact]
    public void NativeQuoteTupleReusableAndCustomAlligatorRoutesMatch()
    {
        var data = AlligatorComparison.Fixture();
        var tuples = data.Quotes.Select(q => (q.Date, (double)(q.High + q.Low) / 2)).ToArray();
        var reusable = tuples
            .Select(q => new AwesomeResult(q.Date) { Oscillator = q.Item2 })
            .Cast<IReusableResult>();
        var expected = data.Quotes.GetAlligator().ToArray();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetAlligator().ToArray(),
                reusable.GetAlligator().ToArray(),
                data.Quotes.AsEnumerable().Reverse().GetAlligator().ToArray(),
            }
        )
        {
            Assert.Equal(expected.Select(r => r.Date), rows.Select(r => r.Date));
            Assert.Equal(expected.Select(r => r.Jaw), rows.Select(r => r.Jaw));
            Assert.Equal(expected.Select(r => r.Teeth), rows.Select(r => r.Teeth));
            Assert.Equal(expected.Select(r => r.Lips), rows.Select(r => r.Lips));
        }
        var expectedGator = data.Quotes.GetGator().ToArray();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetGator().ToArray(),
                reusable.GetGator().ToArray(),
                expected.GetGator().ToArray(),
            }
        )
        {
            Assert.Equal(expectedGator.Select(r => r.Date), rows.Select(r => r.Date));
            Assert.Equal(expectedGator.Select(r => r.Upper), rows.Select(r => r.Upper));
            Assert.Equal(expectedGator.Select(r => r.Lower), rows.Select(r => r.Lower));
            Assert.Equal(
                expectedGator.Select(r => r.UpperIsExpanding),
                rows.Select(r => r.UpperIsExpanding)
            );
            Assert.Equal(
                expectedGator.Select(r => r.LowerIsExpanding),
                rows.Select(r => r.LowerIsExpanding)
            );
        }
    }

    [Fact]
    public void SuppliedLinesHandleMissingValuesExactExpansionAndIndependentEnumeration()
    {
        AlligatorSample[] rows =
        [
            new(DateTime.UnixEpoch, 0, 0, 0),
            new(DateTime.UnixEpoch.AddDays(1), double.Epsilon, 0, -double.Epsilon),
            new(DateTime.UnixEpoch.AddDays(2), double.Epsilon, 0, -double.Epsilon),
            new(DateTime.UnixEpoch.AddDays(3), null, 0, null),
            new(DateTime.UnixEpoch.AddDays(4), 1, 0, -1),
        ];
        var result = GatorWithDetails.FromLines(rows).ToArray();
        Assert.Equal(
            new bool?[] { null, true, false, false, null },
            result.Select(r => r.UpperIsExpanding)
        );
        Assert.Equal(
            new bool?[] { null, true, false, false, null },
            result.Select(r => r.LowerIsExpanding)
        );
        Assert.Equal(
            new double?[] { 0, double.Epsilon, double.Epsilon, null, 1 },
            result.Select(r => r.Upper)
        );
        Assert.Equal(
            new double?[] { 0, -double.Epsilon, -double.Epsilon, null, -1 },
            result.Select(r => r.Lower)
        );
        var native = rows.Select(r => new AlligatorResult(r.Time)
            {
                Jaw = r.Jaw,
                Teeth = r.Teeth,
                Lips = r.Lips,
            })
            .GetGator()
            .ToArray();
        Assert.Equal(native.Select(r => r.Upper), result.Select(r => r.Upper));
        Assert.Equal(native.Select(r => r.Lower), result.Select(r => r.Lower));
        Assert.Equal(
            native.Select(r => r.UpperIsExpanding),
            result.Select(r => r.UpperIsExpanding)
        );
        Assert.Equal(
            native.Select(r => r.LowerIsExpanding),
            result.Select(r => r.LowerIsExpanding)
        );
        var enumerable = GatorWithDetails.FromLines(rows);
        Assert.Equal(enumerable.ToArray(), enumerable.ToArray());
        using var first = enumerable.GetEnumerator();
        using var second = enumerable.GetEnumerator();
        Assert.True(first.MoveNext());
        Assert.True(first.MoveNext());
        Assert.True(second.MoveNext());
        Assert.Null(second.Current.UpperIsExpanding);
        Assert.Empty(GatorWithDetails.FromLines([]));
    }

    [Fact]
    public void SuppliedLinesUseExactDifferencesAndRejectInvalidFinalOutputs()
    {
        var random = new Random(781);
        var rows = Enumerable
            .Range(0, 100)
            .Select(i => new AlligatorSample(
                DateTime.UnixEpoch.AddMinutes(i),
                Math.ScaleB(random.NextDouble() - .5, i - 50),
                i % 9 == 0 ? null : Math.ScaleB(random.NextDouble(), i - 50),
                Math.ScaleB(random.NextDouble(), i - 50)
            ))
            .ToArray();
        var result = GatorWithDetails.FromLines(rows).ToArray();
        for (var i = 0; i < rows.Length; i++)
        {
            double? Difference(double? a, double? b, int sign) =>
                a.HasValue && b.HasValue
                    ? sign * Math.Abs(Round(Units(a.Value) - Units(b.Value), Grid))
                    : null;
            var upper = Difference(rows[i].Jaw, rows[i].Teeth, 1);
            var lower = Difference(rows[i].Teeth, rows[i].Lips, -1);
            Assert.Equal(upper, result[i].Upper);
            Assert.Equal(lower, result[i].Lower);
            Assert.Equal(rows[i].Time, result[i].Time);
            Assert.Equal(
                i > 0 && result[i - 1].Upper.HasValue ? (bool?)(upper > result[i - 1].Upper) : null,
                result[i].UpperIsExpanding
            );
            Assert.Equal(
                i > 0 && result[i - 1].Lower.HasValue ? (bool?)(lower < result[i - 1].Lower) : null,
                result[i].LowerIsExpanding
            );
        }
        Assert.Throws<ArgumentNullException>(() => GatorWithDetails.FromLines(null!));
        foreach (
            var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                GatorWithDetails.FromLines([new(DateTime.UnixEpoch, value, 0, 0)]).ToArray()
            );
        Assert.Throws<ArithmeticException>(() =>
            GatorWithDetails
                .FromLines([new(DateTime.UnixEpoch, double.MaxValue, -double.MaxValue, 0)])
                .ToArray()
        );
        Assert.Throws<ArithmeticException>(() =>
            GatorWithDetails
                .FromLines([
                    new(DateTime.UnixEpoch, double.MaxValue, double.MaxValue, -double.MaxValue),
                ])
                .ToArray()
        );
        var native = new[]
        {
            new AlligatorResult(DateTime.UnixEpoch)
            {
                Jaw = double.MaxValue,
                Teeth = -double.MaxValue,
                Lips = 0,
            },
        }.GetGator().Single();
        Assert.Equal(double.PositiveInfinity, native.Upper);
    }

    [Fact]
    public void ParameterOrderingUsesWideSumsAndNativeOverflowRemainsExplicit()
    {
        int[][] invalid =
        [
            [3, 1, 2, 1, 0, 1],
            [3, 1, 1, 1, 1, 1],
            [2, 1, 2, 1, 1, 1],
            [3, 0, 2, 1, 1, 1],
            [3, 1, 2, 0, 1, 1],
            [3, 1, 2, 1, 1, 0],
            [3, 1, 2, 4, 1, 1],
            [5, 4, 3, 1, 1, 4],
        ];
        foreach (var s in invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AlligatorComparison.Indicator(false, s)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AlligatorComparison.Indicator(true, s)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AlligatorComparison
                    .Fixture()
                    .Quotes.GetAlligator(s[0], s[1], s[2], s[3], s[4], s[5])
                    .ToArray()
            );
        }
        _ = new AlligatorWithDetails(3, int.MaxValue, 2, int.MaxValue - 1, 1, int.MaxValue - 2);
        Assert.Throws<OverflowException>(() =>
            AlligatorComparison
                .Fixture()
                .Quotes.GetAlligator(3, int.MaxValue, 2, int.MaxValue - 1, 1, int.MaxValue - 2)
                .ToArray()
        );
    }

    [Fact]
    public void EveryOutputAndPresenceRejectsCorruption()
    {
        var data = AlligatorComparison.Fixture();
        foreach (var pair in AlligatorComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                if (presence)
                    output.Present![^1] = false;
                else
                    output.Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    20
                )
            );
        }
    }
}
