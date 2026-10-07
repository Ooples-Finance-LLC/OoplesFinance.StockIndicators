using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TranscendentalComparisonTests
{
    [Theory]
    [InlineData(PriceTranscendentalOperation.NaturalLogarithm)]
    [InlineData(PriceTranscendentalOperation.CommonLogarithm)]
    [InlineData(PriceTranscendentalOperation.Exponential)]
    [InlineData(PriceTranscendentalOperation.HyperbolicSine)]
    [InlineData(PriceTranscendentalOperation.HyperbolicCosine)]
    [InlineData(PriceTranscendentalOperation.HyperbolicTangent)]
    public async Task IndependentContractsVerifyLifecycleAndExtremes(
        PriceTranscendentalOperation operation
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(PriceTranscendentalTransform),
                operation.ToString(),
                () => new PriceTranscendentalTransform(operation)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static async Task<double[][]> Run(
        PriceTranscendentalOperation operation,
        params double[] values
    )
    {
        var indicator = new PriceTranscendentalTransform(operation);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    values.Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
                )
            )
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task UndefinedLogarithmsRecoverWhileNativeNonfiniteResultsRemainVisible()
    {
        foreach (
            var operation in new[]
            {
                PriceTranscendentalOperation.NaturalLogarithm,
                PriceTranscendentalOperation.CommonLogarithm,
            }
        )
        {
            var values = await Run(operation, -1, 0, 1, 10);
            Assert.Equal(new[] { 0d, 0, 1, 1 }, values[1]);
            Assert.Equal(new[] { 0d, 0, 0 }, values[0].Take(3));
            var pair = TranscendentalComparison.Pairs[(int)operation];
            var native = pair.Competitor(CompetitorData.FromCloses([-1, 0, 1, 10]), 20).Outputs[
                "Value"
            ];
            Assert.True(native.Present![0]);
            Assert.True(native.Present[1]);
            Assert.True(double.IsNaN(native.Values[0]));
            Assert.Equal(double.NegativeInfinity, native.Values[1]);
        }
    }

    [Fact]
    public async Task UnderflowAndTinySignalsHaveExplicitResults()
    {
        Assert.Equal(
            new[] { 0d, double.Epsilon, 2 * double.Epsilon },
            (await Run(PriceTranscendentalOperation.Exponential, -746, -745, -744))[0]
        );
        foreach (
            var operation in new[]
            {
                PriceTranscendentalOperation.HyperbolicSine,
                PriceTranscendentalOperation.HyperbolicTangent,
            }
        )
            Assert.Equal(
                new[] { -double.Epsilon, 0, double.Epsilon },
                (await Run(operation, -double.Epsilon, 0, double.Epsilon))[0]
            );
        Assert.Equal(
            1,
            (await Run(PriceTranscendentalOperation.HyperbolicCosine, double.Epsilon))[0][0]
        );
        Assert.Equal(2, (await Run(PriceTranscendentalOperation.CommonLogarithm, 100))[0][0]);
        foreach (var name in new[] { "Sinh", "Tanh" })
        {
            Assert.Equal(
                double.Epsilon,
                TranscendentalComparison.ReferenceValue(name, double.Epsilon)
            );
            Assert.False(TranscendentalComparison.Budget.Accepts(double.Epsilon, 0));
        }
        Assert.False(TranscendentalComparison.Budget.Accepts(0, double.Epsilon));
    }

    [Theory]
    [InlineData(PriceTranscendentalOperation.Exponential, 710)]
    [InlineData(PriceTranscendentalOperation.HyperbolicSine, 711)]
    [InlineData(PriceTranscendentalOperation.HyperbolicSine, -711)]
    [InlineData(PriceTranscendentalOperation.HyperbolicCosine, 711)]
    public async Task FinalOverflowIsRejected(PriceTranscendentalOperation operation, double input)
    {
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Run(operation, 1, input));
        var output = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            TranscendentalComparison.Call(
                TranscendentalComparison.Names[(int)operation],
                [1d, input],
                System.Range.All,
                output,
                out _
            )
        );
        Assert.True(double.IsInfinity(output[1]));
    }

    [Fact]
    public async Task ExtremeFiniteLogarithmsAndHyperbolicValuesRemainRepresentable()
    {
        foreach (
            var operation in new[]
            {
                PriceTranscendentalOperation.NaturalLogarithm,
                PriceTranscendentalOperation.CommonLogarithm,
            }
        )
        {
            var actual = (await Run(operation, double.Epsilon, double.MaxValue))[0];
            for (var i = 0; i < 2; i++)
                Assert.True(
                    TranscendentalComparison.Budget.Accepts(
                        TranscendentalComparison
                            .ReferenceValue(
                                TranscendentalComparison.Names[(int)operation],
                                i == 0 ? double.Epsilon : double.MaxValue
                            )!
                            .Value,
                        actual[i]
                    )
                );
        }
        foreach (
            var operation in new[]
            {
                PriceTranscendentalOperation.HyperbolicSine,
                PriceTranscendentalOperation.HyperbolicCosine,
            }
        )
        {
            var actual = (await Run(operation, -710, 710))[0];
            for (var i = 0; i < 2; i++)
                Assert.True(
                    TranscendentalComparison.Budget.Accepts(
                        TranscendentalComparison
                            .ReferenceValue(
                                TranscendentalComparison.Names[(int)operation],
                                i == 0 ? -710 : 710
                            )!
                            .Value,
                        actual[i]
                    )
                );
        }
    }

    [Fact]
    public async Task ChainedOutputUsesTheSelectedSource()
    {
        var logarithm = new PriceTranscendentalTransform(
            PriceTranscendentalOperation.NaturalLogarithm
        );
        var exponential = new PriceTranscendentalTransform(
            PriceTranscendentalOperation.Exponential
        );
        exponential.Of(logarithm);
        double[] prices = [.25, .5, 1, 2, 10];
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    prices.Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
                )
            )
            .ConfigureIndicators(exponential)
            .BuildAsync();
        var actual = run[exponential.Value].ToArray();
        for (var i = 0; i < prices.Length; i++)
            Assert.True(TranscendentalComparison.Budget.Accepts(prices[i], actual[i]));
    }

    [Fact]
    public void NativeFloatDoubleRangesAliasesAndFailuresAreExplicit()
    {
        foreach (var name in TranscendentalComparison.Names)
        {
            double[] input = [.5, 1, 2, 3];
            var actual = new double[4];
            Assert.Equal(
                TALib.Core.RetCode.Success,
                TranscendentalComparison.Call(
                    name,
                    input,
                    new System.Range(1, 2),
                    actual,
                    out var range
                )
            );
            Assert.Equal(new System.Range(1, 3), range);
            for (var i = 0; i < 2; i++)
                Assert.True(
                    TranscendentalComparison.Budget.Accepts(
                        TranscendentalComparison.ReferenceValue(name, input[i + 1])!.Value,
                        actual[i]
                    )
                );
            Assert.Equal(
                TALib.Core.RetCode.Success,
                TranscendentalComparison.Call(name, input, System.Range.All, input, out _)
            );
            double[] original = [.5, 1, 2, 3];
            for (var i = 0; i < original.Length; i++)
                Assert.True(
                    TranscendentalComparison.Budget.Accepts(
                        TranscendentalComparison.ReferenceValue(name, original[i])!.Value,
                        input[i]
                    )
                );
            float[] single = [.5f, 1, 2, 3];
            Assert.Equal(
                TALib.Core.RetCode.Success,
                TranscendentalComparison.Call(name, single, System.Range.All, single, out _)
            );
            for (var i = 0; i < 4; i++)
                Assert.True(Math.Abs(single[i] - input[i]) <= 2e-7 * Math.Abs(input[i]));
            foreach (var empty in new[] { Array.Empty<double>(), new[] { 1d } })
                Assert.Equal(
                    TALib.Core.RetCode.OutOfRangeParam,
                    TranscendentalComparison.Call(
                        name,
                        empty,
                        System.Range.All,
                        new double[1],
                        out _
                    )
                );
            Assert.Equal(
                TALib.Core.RetCode.OutOfRangeParam,
                TranscendentalComparison.Call(name, input, new System.Range(3, 1), actual, out _)
            );
            Assert.Throws<IndexOutOfRangeException>(() =>
                TranscendentalComparison.Call(name, input, System.Range.All, new double[1], out _)
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriceTranscendentalTransform((PriceTranscendentalOperation)99)
        );
    }

    [Fact]
    public void CompleteOutputComparisonRejectsValueAndPresenceCorruption()
    {
        foreach (var pair in TranscendentalComparison.Pairs)
        {
            var data = TranscendentalComparison.BoundaryFixture(pair.Id.Split('.')[^1]);
            ComparisonVerifier.Check(pair, data, 20);
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData input, int period)
                {
                    var result = native
                        ? pair.Competitor(input, period)
                        : pair.Ooples(input, period);
                    var output = result.Outputs["Value"];
                    if (presence)
                        output.Present![0] = !output.Present[0];
                    else
                        output.Values[0] = output.Values[0] == 0 ? double.Epsilon : 0; // NOSONAR: Exact zero is deliberately corrupted.
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
}
