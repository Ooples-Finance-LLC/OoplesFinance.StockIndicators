using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("TA MFI settings", DisableParallelization = true)]
public sealed class MoneyFlowSettingsCollection { }

[Collection("TA MFI settings")]
public sealed class MoneyFlowIndexComparisonTests
{
    private static Bar[] Raw(double[] prices, double[]? volumes = null) =>
        prices
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, volumes?[i] ?? 1))
            .ToArray();

    [Theory]
    [InlineData(false, 2, 0)]
    [InlineData(true, 3, 0)]
    [InlineData(true, 3, 4)]
    [InlineData(false, int.MaxValue, 0)]
    [InlineData(true, 2, int.MaxValue)]
    public async Task IndependentLifecycleContracts(bool ta, int period, int suppressed)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowMoneyFlowIndex),
                "window money flow",
                () => new WindowMoneyFlowIndex(period, ta, suppressed)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownCoefficientsFlatTiesAndThresholdBoundaries()
    {
        foreach (var ta in new[] { false, true })
        {
            var result = MoneyFlowIndexComparison.Owned(Raw([1, 2, 1, 1]), 2, ta).Outputs["Value"];
            Assert.Equal(new[] { false, false, true, true }, result.Present);
            Assert.Equal(200d / 3, result.Values[2]);
            Assert.Equal(0, result.Values[3]);
            Assert.Equal(
                ta ? 0 : 100,
                MoneyFlowIndexComparison.Owned(Raw([3, 3, 3]), 2, ta).Outputs["Value"].Values[2]
            );
        }
        foreach (var total in new[] { Math.BitDecrement(1d), 1d, Math.BitIncrement(1d) })
        {
            var result = MoneyFlowIndexComparison
                .Owned(Raw([0, 1, 1], [0, total, 0]), 2, true)
                .Outputs["Value"]
                .Values[2];
            Assert.Equal(total < 1 ? 0 : 100, result);
        }
        var suppression = MoneyFlowIndexComparison
            .Owned(Raw([1, 2, 1, 2, 1, 2, 1]), 2, true, 3)
            .Outputs["Value"];
        Assert.Equal(new[] { false, false, false, false, false, true, true }, suppression.Present);
    }

    [Fact]
    public void IndependentTinyWideAndLazyPeriodCases()
    {
        foreach (
            var prices in new[]
            {
                new[] { double.Epsilon, 2 * double.Epsilon, double.Epsilon, 2 * double.Epsilon },
                new[]
                {
                    double.MaxValue / 2,
                    double.MaxValue,
                    double.MaxValue / 2,
                    double.MaxValue,
                },
                new[] { 1d, Math.BitIncrement(1), 1, Math.BitDecrement(1) },
            }
        )
        foreach (var volume in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var ta in new[] { false, true })
        foreach (var period in new[] { 2, 3, int.MaxValue })
        {
            var bars = Raw(prices, Enumerable.Repeat(volume, prices.Length).ToArray());
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(MoneyFlowIndexComparison.Reference(bars, period, ta)),
                MoneyFlowIndexComparison.Owned(bars, period, ta),
                "wide/tiny MFI",
                IndicatorErrorBudget.Exact
            );
        }
        Assert.Equal(
            200d / 3,
            MoneyFlowIndexComparison
                .Owned(
                    Raw(
                        [double.Epsilon, 2 * double.Epsilon, double.Epsilon],
                        [double.Epsilon, double.Epsilon, double.Epsilon]
                    ),
                    2,
                    false
                )
                .Outputs["Value"]
                .Values[2]
        );
        Assert.Throws<IndicatorOutputException>(() =>
            MoneyFlowIndexComparison.Owned(Raw([1, 2, -2]), 2, false)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowMoneyFlowIndex(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowMoneyFlowIndex(2, false, -1));
    }

    [Fact]
    public void NativeUnderflowOverflowAndCancellationRemainVisible()
    {
        var tiny = new[] { double.Epsilon, 2 * double.Epsilon, double.Epsilon };
        var volumes = Enumerable.Repeat(double.Epsilon, 3).ToArray();
        var packed = new double[3];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mfi<double>(tiny, tiny, tiny, volumes, System.Range.All, packed, out _, 2)
        );
        Assert.Equal(0, packed[0]);
        var huge = new[] { 1d, 2d, 1d };
        var hugeVolume = Enumerable.Repeat(double.MaxValue, 3).ToArray();
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mfi<double>(huge, huge, huge, hugeVolume, System.Range.All, packed, out _, 2)
        );
        Assert.True(double.IsNaN(packed[0]));
        Assert.True(
            double.IsFinite(
                MoneyFlowIndexComparison
                    .Owned(Raw(huge, hugeVolume), 2, true)
                    .Outputs["Value"]
                    .Values[2]
            )
        );
        var data = CompetitorData.FromOhlcv(
            [1, 2, 1],
            [1, 2, 1],
            [1, 2, 1],
            [1, 2, 1],
            [0, 1e-20, 1]
        );
        Assert.Equal(0, data.Quotes.GetMfi(2).Last().Mfi);
        Assert.True(
            MoneyFlowIndexComparison.Owned(data.IndicatorBars, 2, false).Outputs["Value"].Values[2]
                > 0
        );
        var collapsed = CompetitorData.FromOhlcv(tiny, tiny, tiny, tiny, volumes);
        Assert.Equal(100, collapsed.Quotes.GetMfi(2).Last().Mfi);
    }

    [Fact]
    public void NativeDefaultSortingLookbackRangesAliasingAndFloat()
    {
        var data = CompetitorData.Create(40);
        Assert.Equal(
            data.Quotes.GetMfi(3).Select(r => (r.Date, r.Mfi)),
            data.Quotes.AsEnumerable().Reverse().GetMfi(3).Select(r => (r.Date, r.Mfi))
        );
        Assert.Equal(
            data.Quotes.GetMfi(14).Select(r => r.Mfi),
            data.Quotes.GetMfi().Select(r => r.Mfi)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetMfi(1).ToArray());
        Assert.Equal(14, Functions.MfiLookback());
        Assert.Equal(-1, Functions.MfiLookback(1));
        var packed = new double[data.Count];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mfi<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                data.Volumes,
                5..12,
                packed,
                out var range,
                3
            )
        );
        Assert.Equal(5..13, range);
        var expected = MoneyFlowIndexComparison
            .NativeReference(data.IndicatorBars.Skip(2).Take(11).ToArray(), 3, true)
            .Skip(3)
            .Select(v => v!.Value);
        Assert.Equal(expected, packed.Take(8));
        var alias = (double[])data.Closes.Clone();
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mfi<double>(
                data.Highs,
                data.Lows,
                alias,
                data.Volumes,
                System.Range.All,
                alias,
                out _,
                3
            )
        );
        Assert.Equal(
            MoneyFlowIndexComparison
                .NativeReference(data.IndicatorBars, 3, true)
                .Skip(3)
                .Select(v => v!.Value),
            alias.Take(data.Count - 3)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Mfi<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                data.Volumes,
                System.Range.All,
                packed,
                out _,
                1
            )
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Mfi<double>(
                new[] { 1d },
                new[] { 1d },
                new[] { 1d },
                new[] { 1d },
                System.Range.All,
                new double[1],
                out _,
                2
            )
        );
        float[] prices = [1, 2, 1],
            volume = [1, 1, 1],
            output = new float[3];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mfi<float>(prices, prices, prices, volume, System.Range.All, output, out _, 2)
        );
        Assert.Equal(100f * (2f / 3f), output[0]);
        foreach (var pair in MoneyFlowIndexComparison.Pairs)
        foreach (var period in new[] { 2, 3, 14 })
            ComparisonVerifier.Check(pair, data, period);
    }

    [Fact]
    public void TaUnstableSettingsAndRangedStartupMatchIndependentReference()
    {
        var old = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Mfi);
        var data = CompetitorData.Create(30);
        try
        {
            foreach (var unstable in new[] { 0, 1, 3 })
            {
                TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Mfi, unstable);
                foreach (var start in new[] { 0, 8 })
                {
                    var packed = new double[30];
                    Assert.Equal(
                        TaCore.RetCode.Success,
                        Functions.Mfi<double>(
                            data.Highs,
                            data.Lows,
                            data.Closes,
                            data.Volumes,
                            new System.Range(start, 20),
                            packed,
                            out var range,
                            3
                        )
                    );
                    var first = Math.Max(start, 3 + unstable);
                    Assert.Equal(new System.Range(first, 21), range);
                    var bars = data
                        .IndicatorBars.Skip(first - 3 - unstable)
                        .Take(21 - (first - 3 - unstable))
                        .ToArray();
                    Assert.Equal(
                        MoneyFlowIndexComparison
                            .NativeReference(bars, 3, true, unstable)
                            .Skip(3 + unstable)
                            .Select(v => v!.Value),
                        packed.Take(21 - first)
                    );
                }
                ComparisonVerifier.Compare(
                    VolumePriceComparison.Mask(
                        MoneyFlowIndexComparison.Reference(data.IndicatorBars, 3, true, unstable)
                    ),
                    MoneyFlowIndexComparison.Owned(data.IndicatorBars, 3, true, unstable),
                    "suppressed MFI",
                    IndicatorErrorBudget.Exact
                );
            }
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Mfi, int.MaxValue);
            Assert.True(Functions.MfiLookback(2) < 0);
        }
        finally
        {
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Mfi, old);
        }
    }

    [Fact]
    public async Task CloseChainingRetainsHighLowAndVolume()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (var ta in new[] { false, true })
        {
            var indicator = new WindowMoneyFlowIndex(3, ta);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = MoneyFlowIndexComparison.Reference(bars, 3, ta);
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
        }
    }

    [Fact]
    public void ValuesPresenceAndFlatConventionMutationsAreDetected()
    {
        foreach (var pair in MoneyFlowIndexComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        var ta = MoneyFlowIndexComparison.Create(true);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                ta with
                {
                    Library = MoneyFlowIndexComparison.Create(false).Ooples,
                },
                CompetitorData.FromCloses([1, 1, 1, 1]),
                2
            )
        );
    }
}
