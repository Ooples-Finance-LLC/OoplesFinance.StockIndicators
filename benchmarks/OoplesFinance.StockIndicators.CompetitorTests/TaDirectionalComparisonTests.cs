using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("TA directional settings", DisableParallelization = true)]
public sealed class DirectionalSettingsCollection { }

[Collection("TA directional settings")]
public sealed class TaDirectionalComparisonTests
{
    private static Bar[] Points(params double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(PriorDirectionalMeasure.PositiveMovement)]
    [InlineData(PriorDirectionalMeasure.NegativeMovement)]
    [InlineData(PriorDirectionalMeasure.PositiveIndicator)]
    [InlineData(PriorDirectionalMeasure.NegativeIndicator)]
    [InlineData(PriorDirectionalMeasure.Index)]
    [InlineData(PriorDirectionalMeasure.Average)]
    [InlineData(PriorDirectionalMeasure.Rating)]
    public async Task IndependentRationalLifecycleContracts(PriorDirectionalMeasure measure)
    {
        var configurations = new List<(int, int)>
        {
            (2, 0),
            (3, 2),
            (int.MaxValue, 0),
            (2, int.MaxValue),
        };
        if (measure <= PriorDirectionalMeasure.NegativeIndicator)
            configurations.Add((1, int.MaxValue));
        foreach (var config in configurations)
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(PriorSeededDirectionalMeasure),
                    $"prior directional {measure}",
                    () => new PriorSeededDirectionalMeasure(measure, config.Item1, config.Item2)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void FullValuesMatchIndependentGridAndNativeStageReferences()
    {
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (
            var p in measure <= PriorDirectionalMeasure.NegativeIndicator
                ? new[] { 1, 2, 3, 14 }
                : new[] { 2, 3, 14 }
        )
        foreach (var unstable in new[] { 0, 3 })
        {
            using var settings = new TaDirectionalComparison.Settings(measure, unstable);
            var pair = TaDirectionalComparison.Pair(measure, unstable);
            ComparisonVerifier.Check(pair, CompetitorData.Create(50), p);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 40), p);
        }
    }

    [Fact]
    public void KnownSeedsAndUnstableSuppressionAreDistinct()
    {
        var bars = Points(Enumerable.Range(0, 12).Select(i => (double)i).ToArray());
        var plus = TaDirectionalComparison
            .Owned(bars, 3, PriorDirectionalMeasure.PositiveMovement)
            .Outputs["Value"];
        Assert.Equal(2, plus.Values[2]);
        Assert.Equal(7d / 3, plus.Values[3]);
        var di = TaDirectionalComparison
            .Owned(bars, 3, PriorDirectionalMeasure.PositiveIndicator)
            .Outputs["Value"];
        Assert.Equal(Enumerable.Range(0, bars.Length).Select(i => i >= 3), di.Present);
        Assert.All(di.Values.Skip(3), v => Assert.Equal(100, v));
        var adx = TaDirectionalComparison.Owned(bars, 3, PriorDirectionalMeasure.Average).Outputs[
            "Value"
        ];
        var rating = TaDirectionalComparison.Owned(bars, 3, PriorDirectionalMeasure.Rating).Outputs[
            "Value"
        ];
        Assert.Equal(Enumerable.Range(0, bars.Length).Select(i => i >= 5), adx.Present);
        Assert.Equal(Enumerable.Range(0, bars.Length).Select(i => i >= 7), rating.Present);
        Assert.All(adx.Values.Skip(5), v => Assert.Equal(100, v));
        Assert.All(rating.Values.Skip(7), v => Assert.Equal(100, v));
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        {
            var result = TaDirectionalComparison.Owned(bars, 3, measure, 2).Outputs["Value"];
            Assert.Equal(
                Enumerable
                    .Range(0, bars.Length)
                    .Select(i => i >= TaDirectionalComparison.Lookback(3, measure, 2)),
                result.Present
            );
        }
        foreach (
            var measure in new[]
            {
                PriorDirectionalMeasure.PositiveMovement,
                PriorDirectionalMeasure.NegativeMovement,
                PriorDirectionalMeasure.PositiveIndicator,
                PriorDirectionalMeasure.NegativeIndicator,
            }
        )
            ComparisonVerifier.Compare(
                TaDirectionalComparison.Owned(bars, 1, measure),
                TaDirectionalComparison.Owned(bars, 1, measure, int.MaxValue),
                "period one ignores suppression",
                IndicatorErrorBudget.Exact
            );
    }

    [Fact]
    public void SubnormalLossOfDirectionRetainsDxAndAdx()
    {
        var e = double.Epsilon;
        var bars = Points(0, e, 2 * e, 2 * e, 2 * e, 2 * e, 2 * e, 2 * e);
        foreach (
            var measure in new[]
            {
                PriorDirectionalMeasure.Index,
                PriorDirectionalMeasure.Average,
                PriorDirectionalMeasure.Rating,
            }
        )
        {
            var result = TaDirectionalComparison.Owned(bars, 2, measure);
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(TaDirectionalComparison.Reference(bars, 2, measure)),
                result,
                "subnormal retention",
                IndicatorErrorBudget.Exact
            );
            Assert.Equal(100, result.Outputs["Value"].Values[^1]);
        }
        var flat = TaDirectionalComparison
            .Owned(Points(new double[10]), 2, PriorDirectionalMeasure.Average)
            .Outputs["Value"];
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i >= 3), flat.Present);
        Assert.All(flat.Values.Skip(3), v => Assert.Equal(0, v));
    }

    [Fact]
    public void ExtendedRangesExactTiesAndScalarOverflowAreCovered()
    {
        var bars = Points(
            -double.MaxValue,
            double.MaxValue,
            -double.MaxValue,
            double.MaxValue,
            -double.MaxValue,
            double.MaxValue
        );
        foreach (
            var measure in Enum.GetValues<PriorDirectionalMeasure>()
                .Where(m => m >= PriorDirectionalMeasure.PositiveIndicator)
        )
        {
            var result = TaDirectionalComparison.Owned(bars, 2, measure);
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(TaDirectionalComparison.Reference(bars, 2, measure)),
                result,
                "extended directional",
                IndicatorErrorBudget.Exact
            );
            Assert.All(
                result.Outputs["Value"].Values.Where((_, i) => result.Outputs["Value"].Present![i]),
                v => Assert.True(double.IsFinite(v))
            );
        }
        Assert.Throws<IndicatorOutputException>(() =>
            TaDirectionalComparison.Owned(bars, 1, PriorDirectionalMeasure.PositiveMovement)
        );
        var tied = new[]
        {
            new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, 0),
            new Bar(DateTime.UnixEpoch.AddDays(1), 0, double.Epsilon, -double.Epsilon, 0, 0),
        };
        Assert.Equal(
            0,
            TaDirectionalComparison
                .Owned(tied, 1, PriorDirectionalMeasure.PositiveMovement)
                .Outputs["Value"]
                .Values[1]
        );
        Assert.Equal(
            0,
            TaDirectionalComparison
                .Owned(tied, 1, PriorDirectionalMeasure.NegativeMovement)
                .Outputs["Value"]
                .Values[1]
        );
        var tiny = new[]
        {
            tied[0],
            new Bar(DateTime.UnixEpoch.AddDays(1), 0, 2 * double.Epsilon, -double.Epsilon, 0, 0),
        };
        Assert.Equal(
            2d / 3,
            TaDirectionalComparison
                .Owned(tiny, 1, PriorDirectionalMeasure.PositiveIndicator)
                .Outputs["Value"]
                .Values[1]
        );
    }

    [Fact]
    public void NativeSubrangesAliasingFloatAndUnstableRoutesMatchTheirContracts()
    {
        var data = CompetitorData.Create(60);
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (var unstable in new[] { 0, 2 })
        {
            using var settings = new TaDirectionalComparison.Settings(measure, unstable);
            const int p = 3,
                start = 20,
                end = 45;
            var lookback = (int)TaDirectionalComparison.Lookback(p, measure, unstable);
            Assert.Equal(lookback, TaDirectionalComparison.NativeLookback(p, measure));
            var buffer = new double[data.Count];
            Assert.Equal(
                TaCore.RetCode.Success,
                TaDirectionalComparison.Call(
                    data.Highs,
                    data.Lows,
                    data.Closes,
                    start..end,
                    buffer,
                    out var range,
                    p,
                    measure
                )
            );
            Assert.Equal(measure == PriorDirectionalMeasure.Index ? 0..26 : 20..46, range);
            var expected = TaDirectionalComparison
                .NativeReference(
                    data.IndicatorBars.Skip(start - lookback)
                        .Take(end - start + lookback + 1)
                        .ToArray(),
                    p,
                    measure,
                    unstable
                )
                .Skip(lookback)
                .Select(v => v!.Value);
            Assert.Equal(expected, buffer.Take(26));
            var alias = (double[])data.Highs.Clone();
            Assert.Equal(
                TaCore.RetCode.Success,
                TaDirectionalComparison.Call(
                    alias,
                    data.Lows,
                    data.Closes,
                    System.Range.All,
                    alias,
                    out _,
                    p,
                    measure
                )
            );
            Assert.Equal(
                TaDirectionalComparison
                    .NativeReference(data.IndicatorBars, p, measure, unstable)
                    .Skip(lookback)
                    .Select(v => v!.Value),
                alias.Take(data.Count - lookback)
            );
            var high = Enumerable.Range(0, 20).Select(i => (float)i + 2).ToArray();
            var low = high.Select(v => v - 2).ToArray();
            var close = high.Select(v => v - 1).ToArray();
            var values = new float[20];
            Assert.Equal(
                TaCore.RetCode.Success,
                TaDirectionalComparison.Call(
                    high,
                    low,
                    close,
                    System.Range.All,
                    values,
                    out _,
                    p,
                    measure
                )
            );
            if (measure >= PriorDirectionalMeasure.PositiveIndicator)
                Assert.All(
                    values.Take(20 - lookback),
                    v =>
                        Assert.Equal(
                            measure == PriorDirectionalMeasure.PositiveIndicator ? 50
                                : measure == PriorDirectionalMeasure.NegativeIndicator ? 0
                                : 100,
                            v
                        )
                );
        }
    }

    [Fact]
    public void InvalidParametersNativeOverflowAndAdxrInheritedSettingAreExplicit()
    {
        var data = CompetitorData.Create(15);
        var buffer = new double[15];
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        {
            if (measure >= PriorDirectionalMeasure.Index)
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new PriorSeededDirectionalMeasure(measure, 1)
                );
            Assert.Equal(
                TaCore.RetCode.OutOfRangeParam,
                TaDirectionalComparison.Call(
                    new[] { 1d },
                    new[] { 0d },
                    new[] { .5 },
                    System.Range.All,
                    new double[1],
                    out _,
                    2,
                    measure
                )
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PriorSeededDirectionalMeasure(measure, 0)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PriorSeededDirectionalMeasure(measure, 2, -1)
            );
            Assert.Equal(-1, TaDirectionalComparison.NativeLookback(0, measure));
            Assert.Equal(
                TaCore.RetCode.BadParam,
                TaDirectionalComparison.Call(
                    data.Highs,
                    data.Lows,
                    data.Closes,
                    System.Range.All,
                    buffer,
                    out _,
                    0,
                    measure
                )
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriorSeededDirectionalMeasure((PriorDirectionalMeasure)7)
        );
        Assert.Equal(-3, Functions.AdxLookback(int.MaxValue));
        Assert.DoesNotContain("Adxr", Enum.GetNames<TaCore.UnstableFunc>());
        foreach (var unstable in new[] { 0, 2 })
        {
            using var settings = new TaDirectionalComparison.Settings(
                PriorDirectionalMeasure.Rating,
                unstable
            );
            Assert.Equal(7 + unstable, Functions.AdxrLookback(3));
            ComparisonVerifier.Check(
                TaDirectionalComparison.Pair(PriorDirectionalMeasure.Rating, unstable),
                data,
                3
            );
        }
        var max = double.MaxValue;
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.PlusDM<double>(
                new[] { -max, max },
                new[] { -max, 0d },
                System.Range.All,
                new double[2],
                out _,
                1
            )
        );
        var overflow = new double[2];
        Functions.PlusDM<double>(
            new[] { -max, max },
            new[] { -max, 0d },
            System.Range.All,
            overflow,
            out _,
            1
        );
        Assert.True(double.IsPositiveInfinity(overflow[0]));
    }

    [Fact]
    public async Task ChainingPreservesHighLowAndUsesUpstreamClose()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        {
            var indicator = new PriorSeededDirectionalMeasure(measure, 3, 2);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = TaDirectionalComparison.Reference(bars, 3, measure, 2);
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
        }
    }

    [Fact]
    public void ValuePresenceSeedAndSuppressionMutationsAreDetected()
    {
        foreach (var pair in TaDirectionalComparison.Pairs)
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
                    CompetitorData.Create(40),
                    3
                )
            );
        }
        var positive = TaDirectionalComparison.Pair(PriorDirectionalMeasure.PositiveIndicator);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                positive with
                {
                    Library = (d, p) =>
                        DirectionalComparison.Owned(
                            d.IndicatorBars,
                            p,
                            p,
                            DirectionalWindowMeasure.PositiveIndicator
                        ),
                },
                CompetitorData.Create(40),
                3
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                positive with
                {
                    Library = (d, p) =>
                        TaDirectionalComparison.Owned(
                            d.IndicatorBars,
                            p,
                            PriorDirectionalMeasure.PositiveIndicator,
                            1
                        ),
                },
                CompetitorData.Create(40),
                3
            )
        );
    }

    [Fact]
    public void InstalledDxReportsPackedOffsetsInsteadOfInputAlignment()
    {
        double[] high = [1, 2, 3, 4, 5, 6],
            low = [0, 1, 2, 3, 4, 5],
            close = [.5, 1.5, 2.5, 3.5, 4.5, 5.5];
        var values = new double[6];
        Assert.Equal(2, Functions.DxLookback(2));
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Dx<double>(high, low, close, System.Range.All, values, out var range, 2)
        );
        Assert.Equal(0..4, range);
        Assert.Equal(new double[] { 100, 100, 100, 100 }, values.Take(4));
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Dx<double>(high, low, close, 3..5, values, out range, 2)
        );
        Assert.Equal(0..3, range);
        Assert.Equal(new double[] { 100, 100, 100 }, values.Take(3));
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.PlusDI<double>(high, low, close, System.Range.All, values, out range, 1)
        );
        Assert.Equal(1..6, range);
        Assert.All(values.Take(5), v => Assert.Equal(2d / 3, v));
    }
}
