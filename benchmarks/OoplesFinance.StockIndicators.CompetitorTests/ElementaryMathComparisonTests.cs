using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ElementaryMathComparisonTests
{
    [Theory]
    [InlineData(CandleArithmeticOperation.Add)]
    [InlineData(CandleArithmeticOperation.Subtract)]
    [InlineData(CandleArithmeticOperation.Multiply)]
    [InlineData(CandleArithmeticOperation.Divide)]
    public async Task ArithmeticRationalContractsVerifyLifecycleAndExtremes(
        CandleArithmeticOperation operation
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(CandleArithmetic),
                "elementary arithmetic",
                () => new CandleArithmetic(operation)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(PriceRoundingOperation.Ceiling)]
    [InlineData(PriceRoundingOperation.Floor)]
    [InlineData(PriceRoundingOperation.SquareRoot)]
    public async Task UnaryRationalContractsVerifyLifecycleAndExtremes(
        PriceRoundingOperation operation
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(PriceRoundingTransform),
                "integer rounding or root",
                () => new PriceRoundingTransform(operation)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Task<double[][]> Run(IIndicator indicator, CompetitorData data) =>
        RunBars(indicator, data.IndicatorBars);

    private static Task<double[][]> RunRaw(IIndicator indicator, double[] left, double[] right) =>
        RunBars(
            indicator,
            left.Select(
                (value, i) =>
                    new Bar(
                        DateTime.UnixEpoch.AddDays(i),
                        right[i],
                        Math.Max(value, right[i]),
                        Math.Min(value, right[i]),
                        value,
                        1
                    )
            )
        );

    private static async Task<double[][]> RunBars(IIndicator indicator, IEnumerable<Bar> bars)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task EveryOperandFieldAndItsOrderAreExplicit()
    {
        var values = new Dictionary<CandlePriceField, double>
        {
            [CandlePriceField.Open] = 3,
            [CandlePriceField.High] = 11,
            [CandlePriceField.Low] = -2,
            [CandlePriceField.Close] = 5,
        };
        var data = CompetitorData.FromOhlcv([3], [11], [-2], [5], [1]);
        foreach (var left in values)
        foreach (var right in values)
        {
            var result = await Run(
                new CandleArithmetic(CandleArithmeticOperation.Subtract, left.Key, right.Key),
                data
            );
            Assert.Equal(left.Value - right.Value, result[0][0]);
            Assert.Equal(1, result[1][0]);
        }
    }

    [Theory]
    [InlineData("Add", 6, 12, 20)]
    [InlineData("Sub", 2, 6, 12)]
    [InlineData("Mult", 8, 27, 64)]
    [InlineData("Div", 2, 3, 4)]
    [InlineData("Ceil", 4, 9, 16)]
    [InlineData("Floor", 4, 9, 16)]
    [InlineData("Sqrt", 2, 3, 4)]
    public void FloatDoubleInPlaceAndSubrangeRoutesHaveHandCalculatedResults(
        string name,
        double first,
        double second,
        double third
    )
    {
        double[] left = [4, 9, 16];
        double[] right = [2, 3, 4];
        double[] expected = [first, second, third];
        var data = ElementaryMathComparison.FromOperands(left, right);
        var pair = ComparisonPairs.Get("TaLib.Functions." + name);
        ComparisonVerifier.Check(pair, data, 20);
        Assert.Equal(expected, pair.Ooples(data, 20).Outputs["Value"].Values);
        Assert.Equal(expected, pair.Competitor(data, 20).Outputs["Value"].Values);
        var output = new float[3];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            ElementaryMathComparison.Call(
                name,
                left.Select(v => (float)v).ToArray(),
                right.Select(v => (float)v).ToArray(),
                System.Range.All,
                output,
                out var range
            )
        );
        Assert.Equal((0, 3), range.GetOffsetAndLength(3));
        Assert.Equal(expected.Select(v => (float)v), output);
        foreach (var useLeft in new[] { false, true })
        {
            var a = (double[])left.Clone();
            var b = (double[])right.Clone();
            var destination = useLeft ? a : b;
            Assert.Equal(
                TALib.Core.RetCode.Success,
                ElementaryMathComparison.Call(name, a, b, System.Range.All, destination, out range)
            );
            Assert.Equal(expected, destination);
        }
        var partial = new double[2];
        // TA-Lib's requested end index is inclusive; its returned range is exclusive.
        Assert.Equal(
            TALib.Core.RetCode.Success,
            ElementaryMathComparison.Call(
                name,
                left,
                right,
                new System.Range(1, 2),
                partial,
                out range
            )
        );
        Assert.Equal((1, 2), range.GetOffsetAndLength(3));
        Assert.Equal(expected.Skip(1), partial);
    }

    [Fact]
    public async Task UndefinedDivisionAndRootsRecoverWithoutHidingNativeNonfiniteValues()
    {
        var division = ElementaryMathComparison.FromOperands([1, 0, -1, 4], [0, -0d, 0, 2]);
        var divide = await Run(new CandleArithmetic(CandleArithmeticOperation.Divide), division);
        Assert.Equal(new[] { 0d, 0, 0, 2 }, divide[0]);
        Assert.Equal(new[] { 0d, 0, 0, 1 }, divide[1]);
        var native = ComparisonPairs.Get("TaLib.Functions.Div").Competitor(division, 20).Outputs[
            "Value"
        ];
        Assert.True(double.IsPositiveInfinity(native.Values[0]));
        Assert.True(double.IsNaN(native.Values[1]));
        Assert.True(double.IsNegativeInfinity(native.Values[2]));
        Assert.All(native.Present!, present => Assert.True(present));
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(ComparisonPairs.Get("TaLib.Functions.Div"), division, 20)
        );
        var negative = ElementaryMathComparison.FromOperands(
            [-1, -double.Epsilon, -0d, 4],
            [1, 1, 1, 1]
        );
        var roots = await Run(
            new PriceRoundingTransform(PriceRoundingOperation.SquareRoot),
            negative
        );
        Assert.Equal(new[] { 0d, 0, 0, 2 }, roots[0]);
        Assert.Equal(new[] { 0d, 0, 1, 1 }, roots[1]);
        var nativeRoots = ComparisonPairs
            .Get("TaLib.Functions.Sqrt")
            .Competitor(negative, 20)
            .Outputs["Value"];
        Assert.True(double.IsNaN(nativeRoots.Values[0]));
        Assert.True(double.IsNaN(nativeRoots.Values[1]));
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(ComparisonPairs.Get("TaLib.Functions.Sqrt"), negative, 20)
        );
    }

    [Fact]
    public async Task SubnormalsAndLargeFiniteOperandsRetainSingleOperationRounding()
    {
        var product = await RunRaw(
            new CandleArithmetic(CandleArithmeticOperation.Multiply),
            [double.Epsilon, double.Epsilon, double.MaxValue],
            [.5, 1.5, 0]
        );
        Assert.Equal(new[] { 0d, 2 * double.Epsilon, 0 }, product[0]);
        var ratios = await RunRaw(
            new CandleArithmetic(CandleArithmeticOperation.Divide),
            [double.Epsilon, double.MaxValue],
            [double.Epsilon, double.MaxValue]
        );
        Assert.Equal(new[] { 1d, 1 }, ratios[0]);
        var root = await RunRaw(
            new PriceRoundingTransform(PriceRoundingOperation.SquareRoot),
            [double.Epsilon, double.MaxValue],
            [1, 1]
        );
        Assert.Equal(Math.ScaleB(1d, -537), root[0][0]);
        var nativeRoots = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            ElementaryMathComparison.Call(
                "Sqrt",
                [double.Epsilon, double.MaxValue],
                Array.Empty<double>(),
                System.Range.All,
                nativeRoots,
                out _
            )
        );
        Assert.Equal(root[0], nativeRoots);
        Assert.Equal(
            DispersionReferenceArithmetic.Sqrt(
                MoneyFlowReferenceArithmetic.Units(double.MaxValue),
                MoneyFlowReferenceArithmetic.Grid
            ),
            root[0][1]
        );
    }

    [Theory]
    [InlineData(CandleArithmeticOperation.Add, "Add", double.MaxValue)]
    [InlineData(CandleArithmeticOperation.Subtract, "Sub", -double.MaxValue)]
    [InlineData(CandleArithmeticOperation.Multiply, "Mult", 2)]
    [InlineData(CandleArithmeticOperation.Divide, "Div", double.Epsilon)]
    public async Task FinalOverflowIsRejectedInsteadOfPublished(
        CandleArithmeticOperation operation,
        string name,
        double right
    )
    {
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            RunRaw(new CandleArithmetic(operation), [1, double.MaxValue], [1, right])
        );
        var native = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            ElementaryMathComparison.Call(
                name,
                [1d, double.MaxValue],
                [1d, right],
                System.Range.All,
                native,
                out _
            )
        );
        Assert.True(double.IsPositiveInfinity(native[1]));
    }

    [Fact]
    public async Task SignedIntegerBoundariesAndChainedCloseArePreserved()
    {
        var data = ElementaryMathComparison.FromOperands(
            [-2.5, -double.Epsilon, 0, double.Epsilon, 2.5],
            [1, 1, 1, 1, 1]
        );
        var ceiling = new PriceRoundingTransform(PriceRoundingOperation.Ceiling);
        ceiling.Of(new Sma(1));
        Assert.Equal(new[] { -2d, 0, 0, 1, 3 }, (await Run(ceiling, data))[0]);
        Assert.Equal(
            new[] { -3d, -1, 0, 0, 2 },
            (await Run(new PriceRoundingTransform(PriceRoundingOperation.Floor), data))[0]
        );
    }

    [Fact]
    public void NativeRangeAndLengthBoundariesAreExplicit()
    {
        foreach (var pair in ElementaryMathComparison.Pairs)
        {
            var name = pair.Id.Split('.')[^1];
            foreach (var left in new[] { Array.Empty<double>(), new[] { 1d } })
            {
                double[] sentinel = [99];
                Assert.Equal(
                    TALib.Core.RetCode.OutOfRangeParam,
                    ElementaryMathComparison.Call(
                        name,
                        left,
                        left,
                        System.Range.All,
                        sentinel,
                        out _
                    )
                );
                Assert.Equal(99, sentinel[0]);
            }
            foreach (
                var badRange in new[]
                {
                    new System.Range(2, 1),
                    new System.Range(0, 3),
                    new System.Range(^5, ^0),
                }
            )
                Assert.Equal(
                    TALib.Core.RetCode.OutOfRangeParam,
                    ElementaryMathComparison.Call(
                        name,
                        [4d, 9, 16],
                        [2d, 3, 4],
                        badRange,
                        new double[3],
                        out _
                    )
                );
            Assert.Throws<IndexOutOfRangeException>(() =>
                ElementaryMathComparison.Call(
                    name,
                    [4d, 9, 16],
                    [2d, 3, 4],
                    System.Range.All,
                    new double[1],
                    out _
                )
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CandleArithmetic((CandleArithmeticOperation)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CandleArithmetic(CandleArithmeticOperation.Add, (CandlePriceField)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CandleArithmetic(CandleArithmeticOperation.Add, right: (CandlePriceField)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriceRoundingTransform((PriceRoundingOperation)99)
        );
    }

    [Fact]
    public void IndependentFullOutputsAndPresenceDetectCorruption()
    {
        foreach (var pair in ElementaryMathComparison.Pairs)
        {
            var data = ElementaryMathComparison.BoundaryFixture(
                pair.Id.EndsWith("Sqrt", StringComparison.Ordinal)
            );
            ComparisonVerifier.Check(pair, data, 20);
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData input, int period)
                {
                    var series = native
                        ? pair.Competitor(input, period)
                        : pair.Ooples(input, period);
                    var output = series.Outputs["Value"];
                    if (presence)
                        output.Present![^1] = !output.Present[^1];
                    else
                        output.Values[^1] += 1;
                    return series;
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
