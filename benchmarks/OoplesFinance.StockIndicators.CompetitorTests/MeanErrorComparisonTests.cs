using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MeanErrorComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentRationalContractCoversLifecycleAndExtremes(bool details)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    details
                        ? typeof(MovingAverageDiagnostics)
                        : typeof(WindowMeanAbsoluteDeviation),
                    "errors around published mean",
                    () =>
                        details
                            ? new MovingAverageDiagnostics(period)
                            : new WindowMeanAbsoluteDeviation(period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    private static async Task<double[][]> Run(IIndicator indicator, params double[] values)
    {
        var bars = values
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task ErrorsUsePublishedMeanInsteadOfUnroundedPopulationCenter()
    {
        var baseline = Math.ScaleB(1, 53);
        var result = await Run(new MovingAverageDiagnostics(2), baseline, baseline + 2);
        Assert.Equal(baseline, result[0][1]);
        Assert.Equal(1, result[1][1]);
        Assert.Equal(2, result[2][1]);
        Assert.All(result.Skip(4), row => Assert.Equal(new[] { 0d, 1 }, row));
        Assert.Equal(
            new[] { 0d, 1 },
            (await Run(new WindowMeanAbsoluteDeviation(2), baseline, baseline + 2))[0]
        );
    }

    [Fact]
    public async Task SignedRelativeErrorsRetainSignsAndRecoverAfterZeroExpires()
    {
        var negative = await Run(new MovingAverageDiagnostics(2), -1, -3);
        Assert.Equal(-2, negative[0][1]);
        Assert.Equal(1, negative[1][1]);
        Assert.Equal(1, negative[2][1]);
        Assert.Equal(-2d / 3, negative[3][1]);
        var zero = await Run(new MovingAverageDiagnostics(2), 1, 0, 3, 5);
        Assert.Equal(new[] { 0d, 0, 0, 1 }, zero[7]);
        Assert.Equal(new[] { 0d, 1, 1, 1 }, zero[4]);
        Assert.Equal(4d / 15, zero[3][3]);
    }

    [Fact]
    public async Task OppositeUnrepresentableRatiosCancelBeforeRounding()
    {
        var result = await Run(new MovingAverageDiagnostics(3), double.Epsilon, -double.Epsilon, 3);
        Assert.Equal(1, result[0][2]);
        Assert.Equal(4d / 3, result[1][2]);
        Assert.Equal(2, result[2][2]);
        Assert.Equal(-4d / 9, result[3][2]);
        Assert.Equal(1, result[7][2]);
        var native = new[] { double.Epsilon, -double.Epsilon, 3 }
            .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
            .GetSmaAnalysis(3)
            .Last();
        Assert.Null(native.Mape);
    }

    [Fact]
    public async Task ScalarDoesNotEvaluateUnselectedOversizedErrorsAndPeriodsAreLazy()
    {
        var scalar = await Run(
            new WindowMeanAbsoluteDeviation(2),
            -double.MaxValue,
            double.MaxValue
        );
        Assert.Equal(new[] { 0d, double.MaxValue }, scalar[0]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(new MovingAverageDiagnostics(2), -double.MaxValue, double.MaxValue)
        );
        var incomplete = await Run(
            new MovingAverageDiagnostics(3),
            -double.MaxValue,
            double.MaxValue
        );
        Assert.All(incomplete, row => Assert.All(row, v => Assert.Equal(0, v)));
        var lazy = await Run(new MovingAverageDiagnostics(int.MaxValue), 1, 3, 5);
        Assert.All(lazy, row => Assert.All(row, v => Assert.Equal(0, v)));
        Assert.Equal(
            new[] { 0d, 1, 4d / 3 },
            (await Run(new WindowMeanAbsoluteDeviation(int.MaxValue), 1, 3, 5))[0]
        );
    }

    [Fact]
    public void BothArmsCoverStartupRoundingZeroAndSignedPrices()
    {
        foreach (var pair in MeanErrorComparison.Pairs)
        foreach (var period in new[] { 2, 3, 20 })
        foreach (
            var data in new[]
            {
                MeanErrorComparison.Fixture(),
                MeanErrorComparison.RoundedMeanFixture(),
                MeanErrorComparison.CollapsedQuoteFixture(),
                CompetitorData.FromCloses([5]),
            }
        )
            ComparisonVerifier.Check(pair, data, period);
        ComparisonVerifier.Check(MeanErrorComparison.Pairs[1], MeanErrorComparison.Fixture(), 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageDiagnostics(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowMeanAbsoluteDeviation(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanErrorComparison.Fixture().Quotes.GetSmaAnalysis(0).ToArray()
        );
        var output = new double[3];
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            TALib.Functions.AvgDev<double>(
                new double[] { 1, 2, 3 },
                System.Range.All,
                output,
                out _,
                1
            )
        );
    }

    [Fact]
    public void QuoteTupleAndChainedNativeRoutesAgreeOnExactInputs()
    {
        var data = CompetitorData.FromCloses([1, 3, 4, -1, -3, -5, 0, 0, 1, 3, 4]);
        static object[] Fields(IEnumerable<SmaAnalysis> rows) =>
            rows.Select(r => (object)(r.Sma, r.Mad, r.Mse, r.Mape)).ToArray();
        var quotes = Fields(data.Quotes.GetSmaAnalysis(3));
        Assert.Equal(
            quotes,
            Fields(data.Dates.Zip(data.Closes, (date, value) => (date, value)).GetSmaAnalysis(3))
        );
        Assert.Equal(
            quotes,
            Fields(data.Quotes.GetSma(1).Cast<IReusableResult>().GetSmaAnalysis(3))
        );
    }

    [Fact]
    public void EveryPublishedValueAndPresenceFlagRejectsCorruptionInBothArms()
    {
        foreach (var pair in MeanErrorComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var name in pair.OutputNames ?? ["Value"])
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                var index = data.Count - 1;
                if (presence && output.Present is not null)
                    output.Present[index] = !output.Present[index];
                else
                    output.Values[index] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    MeanErrorComparison.Fixture(),
                    3
                )
            );
        }
    }
}
