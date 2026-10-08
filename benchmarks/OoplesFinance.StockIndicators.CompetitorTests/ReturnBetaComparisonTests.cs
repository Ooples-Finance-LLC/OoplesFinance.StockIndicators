using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ReturnBetaComparisonTests
{
    [Theory]
    [InlineData(ReturnBetaSelection.Standard)]
    [InlineData(ReturnBetaSelection.Up)]
    [InlineData(ReturnBetaSelection.Down)]
    [InlineData(ReturnBetaSelection.All)]
    public async Task FullStatisticsHaveIndependentLifecycleContracts(ReturnBetaSelection selection)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowBetaStatistics),
                "return beta",
                () => new WindowBetaStatistics(4, selection)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ScalarLifecycleIncludesFlatAndOversizedArithmetic(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowReturnBeta),
                "return slope",
                () => new WindowReturnBeta(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownSlopeOrientationDirectionalRatioAndConvexity()
    {
        var scalar = ReturnBetaComparison
            .Create(false)
            .Ooples(CompetitorData.FromBodies([1, 3, 15], [1, 2, 6]), 2)
            .Outputs["Beta"];
        Assert.Equal(new[] { false, false, true }, scalar.Present);
        Assert.Equal(2, scalar.Values[2]);
        var data = CompetitorData.FromBodies([16, 32, 96, 48, 36], [16, 48, 240, 120, 90]);
        var result = ReturnBetaComparison.Create(true).Ooples(data, 4);
        Assert.Equal(2, result.Outputs["BetaUp"].Values[4]);
        Assert.Equal(1, result.Outputs["BetaDown"].Values[4]);
        Assert.Equal(2, result.Outputs["Ratio"].Values[4]);
        Assert.Equal(1, result.Outputs["Convexity"].Values[4]);
        Assert.Equal(new[] { 0d, 2, 4, -.5, -.25 }, result.Outputs["ReturnsEval"].Values);
        Assert.Equal(new[] { 0d, 1, 2, -.5, -.25 }, result.Outputs["ReturnsMrkt"].Values);
        var zeroDown = ReturnBetaComparison
            .Create(true)
            .Ooples(CompetitorData.FromBodies([16, 32, 96, 48, 36], [16, 48, 240, 240, 240]), 4);
        Assert.Equal(0, zeroDown.Outputs["BetaDown"].Values[4]);
        Assert.False(zeroDown.Outputs["Ratio"].Present![4]);
        Assert.Equal(4, zeroDown.Outputs["Convexity"].Values[4]);
    }

    [Fact]
    public void EverySelectionZeroPriceAndFlatSubsetMatchesIndependentReferences()
    {
        var zeros = CompetitorData.FromBodies(
            [0, 3, 0, -2, -3, 0, 7, 2],
            [2, 0, 4, 0, -2, -3, 0, 1]
        );
        foreach (var selection in Enum.GetValues<ReturnBetaSelection>())
        foreach (var period in new[] { 1, 2, 3, 7 })
        {
            var pair = ReturnBetaComparison.Create(true, selection);
            ComparisonVerifier.Check(pair, ReturnBetaComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, zeros, period);
        }
        foreach (var period in new[] { 1, 2, 3, 7 })
            ComparisonVerifier.Check(ReturnBetaComparison.Create(false), zeros, period);
        var flat = CompetitorData.FromBodies([3, 3, 3, 3], [7, 7, 7, 7]);
        Assert.False(ReturnBetaComparison.Create(true).Ooples(flat, 2).Outputs["Beta"].Present![3]);
        Assert.Equal(
            0,
            ReturnBetaComparison.Create(false).Ooples(flat, 2).Outputs["Beta"].Values[3]
        );
    }

    private static Bar[] Raw(double[] market, double[] evaluation) =>
        market
            .Select(
                (v, i) =>
                    new Bar(
                        DateTime.UnixEpoch.AddDays(i),
                        v,
                        Math.Max(v, evaluation[i]),
                        Math.Min(v, evaluation[i]),
                        evaluation[i],
                        0
                    )
            )
            .ToArray();

    [Fact]
    public void OversizedUnpublishedReturnsAndNativeIntermediateOverflow()
    {
        var narrowMarket = new[] { 1d, 1, Math.BitIncrement(1) };
        var wideEvaluation = new[] { 1d, 1, double.MaxValue };
        Assert.True(
            double.IsInfinity(
                ReturnBetaComparison.Reference(
                    narrowMarket,
                    wideEvaluation,
                    2,
                    false,
                    ReturnBetaSelection.Standard
                )[0][2]!.Value
            )
        );
        Assert.Throws<IndicatorOutputException>(() =>
            ReturnBetaComparison.Owned(Raw(wideEvaluation, narrowMarket), 2, false)
        );
        foreach (
            var prices in new[]
            {
                new[] { double.Epsilon, double.MaxValue, -double.MaxValue },
                new[] { double.MaxValue, -double.MaxValue, 0d },
            }
        )
        {
            var bars = Raw(prices, prices);
            var scalar = ReturnBetaComparison.Owned(bars, 2, false);
            Assert.Equal(1, scalar.Outputs["Beta"].Values[2]);
            var packed = new double[3];
            Assert.Equal(
                TALib.Core.RetCode.Success,
                Functions.Beta<double>(prices, prices, System.Range.All, packed, out _, 2)
            );
            Assert.False(double.IsFinite(packed[0]));
        }
        var huge = new[] { double.Epsilon, double.MaxValue, -double.MaxValue };
        Assert.Throws<IndicatorOutputException>(() =>
            ReturnBetaComparison.Owned(Raw(huge, huge), 2, true)
        );
        var rows = huge.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v)).ToArray();
        Assert.True(
            double.IsPositiveInfinity(rows.GetBeta(rows, 2).ToArray()[1].ReturnsEval!.Value)
        );
        var tiny = new[]
        {
            double.Epsilon,
            2 * double.Epsilon,
            3 * double.Epsilon,
            2 * double.Epsilon,
        };
        var output = ReturnBetaComparison.Owned(Raw(tiny, tiny), 3, true);
        Assert.Equal(1, output.Outputs["Beta"].Values[3]);
        var lazy = ReturnBetaComparison.Owned(Raw([1, 2, 3], [1, 3, 5]), int.MaxValue, true);
        Assert.All(lazy.Outputs["Beta"].Present!, v => Assert.False(v));
        Assert.All(lazy.Outputs["ReturnsEval"].Present!, v => Assert.True(v));
    }

    [Fact]
    public void SkenderRoutesDateChecksDefaultModeAndInvalidSelectionHole()
    {
        var data = ReturnBetaComparison.Fixture();
        var eval = data.Dates.Zip(data.Closes, (d, v) => (d, v)).ToArray();
        var market = data.Dates.Zip(data.Opens, (d, v) => (d, v)).ToArray();
        var marketQuotes = data
            .Quotes.Select(q => new Quote
            {
                Date = q.Date,
                Open = q.Open,
                High = q.High,
                Low = q.Low,
                Close = q.Open,
                Volume = q.Volume,
            })
            .ToArray();
        IReusableResult[] re = eval.Select(p => (IReusableResult)new SmaResult(p.d) { Sma = p.v })
            .ToArray();
        IReusableResult[] rm = market
            .Select(p => (IReusableResult)new SmaResult(p.d) { Sma = p.v })
            .ToArray();
        foreach (var type in Enum.GetValues<BetaType>())
        {
            var expected = ReturnBetaComparison.FromSkender(eval.GetBeta(market, 4, type));
            foreach (
                var rows in new[]
                {
                    eval.Reverse().GetBeta(market.Reverse(), 4, type),
                    data.Quotes.GetBeta(marketQuotes, 4, type),
                    re.GetBeta(rm, 4, type),
                }
            )
                ComparisonVerifier.Compare(
                    expected,
                    ReturnBetaComparison.FromSkender(rows),
                    "beta route"
                );
        }
        ComparisonVerifier.Compare(
            ReturnBetaComparison.FromSkender(eval.GetBeta(market, 4, BetaType.Standard)),
            ReturnBetaComparison.FromSkender(eval.GetBeta(market, 4)),
            "default beta mode"
        );
        Assert.Throws<InvalidQuotesException>(() => eval.GetBeta(market.Skip(1), 4).ToArray());
        var bad = (ValueTuple<DateTime, double>[])market.Clone();
        bad[2].Item1 = bad[2].Item1.AddHours(1);
        Assert.Throws<InvalidQuotesException>(() => eval.GetBeta(bad, 4).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => eval.GetBeta(market, 0).ToArray());
        var invalid = eval.GetBeta(market, 4, (BetaType)99).ToArray();
        Assert.All(
            invalid,
            r =>
            {
                Assert.Null(r.Beta);
                Assert.Null(r.BetaUp);
                Assert.Null(r.BetaDown);
                Assert.NotNull(r.ReturnsEval);
            }
        );
    }

    [Fact]
    public void TaLibRangesAliasingLookbackAndOneElementFailure()
    {
        var data = ReturnBetaComparison.Fixture();
        var packed = new double[data.Count];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Beta<double>(data.Closes, data.Opens, 5..12, packed, out var range, 3)
        );
        Assert.Equal(5..13, range);
        var expected = ReturnBetaComparison.NativeReference(
            data.Closes.Skip(2).Take(11).ToArray(),
            data.Opens.Skip(2).Take(11).ToArray(),
            3,
            false,
            ReturnBetaSelection.Standard
        )[0];
        Assert.Equal(expected.Skip(3).Select(v => v!.Value), packed.Take(8));
        var aliased = (double[])data.Closes.Clone();
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Beta<double>(aliased, data.Opens, System.Range.All, aliased, out _, 3)
        );
        Assert.Equal(
            ReturnBetaComparison.Create(false).Competitor(data, 3).Outputs["Beta"].Values.Skip(3),
            aliased.Take(data.Count - 3)
        );
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            Functions.Beta<double>(data.Closes, data.Opens, System.Range.All, packed, out _, 0)
        );
        Assert.Equal(
            TALib.Core.RetCode.OutOfRangeParam,
            Functions.Beta<double>(
                new[] { 1d },
                new[] { 1d },
                System.Range.All,
                new double[1],
                out _,
                1
            )
        );
        Assert.Equal(-1, Functions.BetaLookback(0));
        Assert.Equal(3, Functions.BetaLookback(3));
        Assert.False(
            ReturnBetaComparison
                .Create(false)
                .Ooples(CompetitorData.FromBodies([1], [1]), 1)
                .Outputs["Beta"]
                .Present![0]
        );
    }

    [Fact]
    public async Task FieldSelectionAndChainedCloseAreVerified()
    {
        var data = ReturnBetaComparison.Fixture();
        var selected = new WindowBetaStatistics(
            4,
            ReturnBetaSelection.All,
            CandlePriceField.High,
            CandlePriceField.Low
        );
        using (
            var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(selected)
                .BuildAsync()
        )
        {
            var expected = ReturnBetaComparison.Reference(
                data.Highs,
                data.Lows,
                4,
                true,
                ReturnBetaSelection.All
            );
            for (var i = 0; i < 7; i++)
                Assert.Equal(expected[i].Select(v => v ?? 0), run[selected.Outputs[i]].ToArray());
        }
        foreach (var full in new[] { false, true })
        {
            MultiOutputIndicatorBase indicator = full
                ? new WindowBetaStatistics(4, ReturnBetaSelection.All)
                : new WindowReturnBeta(4);
            indicator.Of(new FixedPeriodWma(2));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var close = FixedWeightedComparison.Stage(data.Closes, 2, false);
            var expected = ReturnBetaComparison.Reference(
                full ? data.Opens : close,
                full ? close : data.Opens,
                4,
                full,
                ReturnBetaSelection.All
            );
            for (var i = 0; i < expected.Length; i++)
                Assert.Equal(expected[i].Select(v => v ?? 0), run[indicator.Outputs[i]].ToArray());
        }
    }

    [Fact]
    public void InvalidParametersAndAllOutputPresenceMutationsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowReturnBeta(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowBetaStatistics(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowBetaStatistics(3, (ReturnBetaSelection)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowReturnBeta(3, (CandlePriceField)99)
        );
        foreach (var pair in ReturnBetaComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                var at = Array.FindLastIndex(output.Present!, v => v);
                Assert.True(at >= 0, "Fixture must exercise " + name);
                if (presence)
                    output.Present![at] = false;
                else
                    output.Values[at] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    ReturnBetaComparison.Fixture(),
                    20
                )
            );
        }
    }
}
