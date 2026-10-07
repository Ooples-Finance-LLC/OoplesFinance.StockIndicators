using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CircularComparisonTests
{
    [Theory]
    [InlineData(PriceCircularOperation.Sine)]
    [InlineData(PriceCircularOperation.Cosine)]
    [InlineData(PriceCircularOperation.Tangent)]
    [InlineData(PriceCircularOperation.ArcSine)]
    [InlineData(PriceCircularOperation.ArcCosine)]
    [InlineData(PriceCircularOperation.ArcTangent)]
    public async Task IndependentContractsVerifyLifecycleAndExtremes(
        PriceCircularOperation operation
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(PriceCircularTransform),
                operation.ToString(),
                () => new PriceCircularTransform(operation)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static async Task<double[][]> Run(
        PriceCircularOperation operation,
        params double[] values
    )
    {
        var indicator = new PriceCircularTransform(operation);
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
    public async Task InverseDomainIsAbsentAndRecoversWithoutMaskingNativeNaNs()
    {
        foreach (
            var op in new[] { PriceCircularOperation.ArcSine, PriceCircularOperation.ArcCosine }
        )
        {
            double[] input = [Math.BitDecrement(-1), -1, 0, 1, Math.BitIncrement(1), .5];
            var actual = await Run(op, input);
            Assert.Equal(new[] { 0d, 1, 1, 1, 0, 1 }, actual[1]);
            var pair = CircularComparison.Pairs[(int)op];
            var data = CompetitorData.FromCloses(input);
            var native = pair.Competitor(data, 20).Outputs["Value"];
            Assert.True(native.Present![0]);
            Assert.True(double.IsNaN(native.Values[0]));
            Assert.True(double.IsNaN(native.Values[4]));
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair, data, 20)
            );
        }
    }

    [Fact]
    public async Task FullBinary64RangeAndPoleNeighborsAgreeWithIndependentReduction()
    {
        var values = new List<double>
        {
            -double.MaxValue,
            double.MaxValue,
            -1e28,
            1e28,
            1e20,
            -double.Epsilon,
            0,
            double.Epsilon,
            -1,
            1,
        };
        foreach (var center in new[] { -Math.PI, -Math.PI / 2, Math.PI / 2, Math.PI })
            values.AddRange([Math.BitDecrement(center), center, Math.BitIncrement(center)]);
        for (var exponent = -1074; exponent <= 1023; exponent += 17)
        {
            var x = Math.ScaleB(1, exponent);
            values.Add(x);
            values.Add(-x);
            values.Add(Math.BitIncrement(x));
        }
        foreach (var op in Enum.GetValues<PriceCircularOperation>())
        {
            var input = values
                .Where(x =>
                    op is not (PriceCircularOperation.ArcSine or PriceCircularOperation.ArcCosine)
                    || Math.Abs(x) <= 1
                )
                .ToArray();
            var actual = (await Run(op, input))[0];
            var name = CircularComparison.Names[(int)op];
            var native = new double[input.Length];
            Assert.Equal(
                TALib.Core.RetCode.Success,
                CircularComparison.Call(name, input, System.Range.All, native, out _)
            );
            for (var i = 0; i < input.Length; i++)
            {
                var expected = CircularComparison.ReferenceValue(name, input[i])!.Value;
                Assert.True(
                    CircularComparison.Budget.Accepts(expected, actual[i]),
                    $"{name}({input[i]:R}): {expected:R} vs {actual[i]:R}"
                );
                Assert.True(CircularComparison.Budget.Accepts(expected, native[i]));
            }
        }
    }

    [Fact]
    public async Task TinySignedValuesAndChainingRetainTheirMeaning()
    {
        foreach (
            var op in new[]
            {
                PriceCircularOperation.Sine,
                PriceCircularOperation.Tangent,
                PriceCircularOperation.ArcSine,
                PriceCircularOperation.ArcTangent,
            }
        )
            Assert.Equal(
                new[] { -double.Epsilon, 0, double.Epsilon },
                (await Run(op, -double.Epsilon, 0, double.Epsilon))[0]
            );
        Assert.False(CircularComparison.Budget.Accepts(double.Epsilon, 0));
        var sine = new PriceCircularTransform(PriceCircularOperation.Sine);
        var inverse = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        inverse.Of(sine);
        double[] input = [-.5, 0, .5];
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    input.Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
                )
            )
            .ConfigureIndicators(inverse)
            .BuildAsync();
        var actual = run[inverse.Value].ToArray();
        for (var i = 0; i < input.Length; i++)
            Assert.True(CircularComparison.Budget.Accepts(input[i], actual[i]));
    }

    [Fact]
    public void NativeFloatDoubleRangesAliasesAndFailuresAreExplicit()
    {
        foreach (var name in CircularComparison.Names)
        {
            double[] input = [-.75, -.25, .25, .75];
            var actual = new double[4];
            Assert.Equal(
                TALib.Core.RetCode.Success,
                CircularComparison.Call(name, input, new System.Range(1, 2), actual, out var range)
            );
            Assert.Equal(new System.Range(1, 3), range);
            for (var i = 0; i < 2; i++)
                Assert.True(
                    CircularComparison.Budget.Accepts(
                        CircularComparison.ReferenceValue(name, input[i + 1])!.Value,
                        actual[i]
                    )
                );
            Assert.Equal(
                TALib.Core.RetCode.Success,
                CircularComparison.Call(name, input, System.Range.All, input, out _)
            );
            double[] original = [-.75, -.25, .25, .75];
            for (var i = 0; i < original.Length; i++)
                Assert.True(
                    CircularComparison.Budget.Accepts(
                        CircularComparison.ReferenceValue(name, original[i])!.Value,
                        input[i]
                    )
                );
            float[] single = [-.75f, -.25f, .25f, .75f];
            Assert.Equal(
                TALib.Core.RetCode.Success,
                CircularComparison.Call(name, single, System.Range.All, single, out _)
            );
            for (var i = 0; i < 4; i++)
                Assert.True(Math.Abs(single[i] - input[i]) <= 2e-7 * Math.Abs(input[i]));
            foreach (var empty in new[] { Array.Empty<double>(), new[] { 1d } })
                Assert.Equal(
                    TALib.Core.RetCode.OutOfRangeParam,
                    CircularComparison.Call(name, empty, System.Range.All, new double[1], out _)
                );
            Assert.Equal(
                TALib.Core.RetCode.OutOfRangeParam,
                CircularComparison.Call(name, input, new System.Range(3, 1), actual, out _)
            );
            Assert.Throws<IndexOutOfRangeException>(() =>
                CircularComparison.Call(name, input, System.Range.All, new double[1], out _)
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriceCircularTransform((PriceCircularOperation)99)
        );
    }

    [Fact]
    public void CompleteOutputComparisonRejectsValueAndPresenceCorruption()
    {
        foreach (var pair in CircularComparison.Pairs)
        {
            var data = CircularComparison.BoundaryFixture(pair.Id.Split('.')[^1]);
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
