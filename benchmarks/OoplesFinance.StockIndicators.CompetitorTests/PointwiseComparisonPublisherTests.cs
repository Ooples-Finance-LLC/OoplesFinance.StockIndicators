using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PointwiseComparisonPublisherTests
{
    public static IEnumerable<object[]> Pairs => ElementaryMathComparison.Pairs
        .Concat(CircularComparison.Pairs).Concat(TranscendentalComparison.Pairs)
        .Select(pair => new object[] { pair.Id });

    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryAdapterPreservesOriginalFullProjectionAfterDisposal(string id)
    {
        var pair = ComparisonPairs.All.Single(p => p.Id == id);
        var data = CompetitorData.FromCloses(Enumerable.Range(0, 257)
            .Select(i => .125 + (i % 64) / 128d).ToArray());
        string name = id.Split('.')[^1];
        IIndicator indicator = name switch
        {
            "Add" => new CandleArithmetic(CandleArithmeticOperation.Add),
            "Sub" => new CandleArithmetic(CandleArithmeticOperation.Subtract),
            "Mult" => new CandleArithmetic(CandleArithmeticOperation.Multiply),
            "Div" => new CandleArithmetic(CandleArithmeticOperation.Divide),
            "Ceil" => new PriceRoundingTransform(PriceRoundingOperation.Ceiling),
            "Floor" => new PriceRoundingTransform(PriceRoundingOperation.Floor),
            "Sqrt" => new PriceRoundingTransform(PriceRoundingOperation.SquareRoot),
            "Sin" => new PriceCircularTransform(PriceCircularOperation.Sine),
            "Cos" => new PriceCircularTransform(PriceCircularOperation.Cosine),
            "Tan" => new PriceCircularTransform(PriceCircularOperation.Tangent),
            "Asin" => new PriceCircularTransform(PriceCircularOperation.ArcSine),
            "Acos" => new PriceCircularTransform(PriceCircularOperation.ArcCosine),
            "Atan" => new PriceCircularTransform(PriceCircularOperation.ArcTangent),
            "Ln" => new PriceTranscendentalTransform(PriceTranscendentalOperation.NaturalLogarithm),
            "Log10" => new PriceTranscendentalTransform(PriceTranscendentalOperation.CommonLogarithm),
            "Exp" => new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential),
            "Sinh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicSine),
            "Cosh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicCosine),
            "Tanh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicTangent),
            _ => throw new InvalidOperationException(id)
        };
        ComparisonSeries expected;
        using (var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult())
        {
            // Retain the original multi-array publisher as the regression oracle.
            var values = run[indicator.Outputs[0]].ToArray();
            var flags = run[indicator.Outputs[1]].ToArray();
            expected = VolumePriceComparison.Mask(values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray());
        }
        var actual = pair.Ooples(data, 20);
        AssertOutput(expected, actual);
        var retained = actual.Outputs["Value"].Values.ToArray();
        actual.Outputs["Value"].Values[0] = 42;
        Assert.Equal(retained[0], pair.Ooples(data, 20).Outputs["Value"].Values[0]);
    }

    [Fact]
    public void ProjectionPreservesBitsPresenceAndIndependentOwnership()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            double.PositiveInfinity, double.NegativeInfinity, double.NaN,
            BitConverter.Int64BitsToDouble(unchecked((long)0xfff8000000001234UL))];
        foreach (double flag in new[] { 0d, -0d, 1d, -1d, double.NaN, double.PositiveInfinity,
            double.NegativeInfinity, double.Epsilon, -double.Epsilon })
        {
            var input = values.ToArray();
            var flags = Enumerable.Repeat(flag, input.Length).ToArray();
            var expected = VolumePriceComparison.Mask(input.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray());
            var actual = PointwiseComparisonPublisher.Publish(input, flags);
            Array.Fill(input, 42); Array.Fill(flags, 42);
            AssertOutput(expected, actual);
        }
    }

    [Fact]
    public void EmptyProjectionPreservesOutputShape() =>
        AssertOutput(VolumePriceComparison.Mask([]), PointwiseComparisonPublisher.Publish([], []));

    [Fact]
    public void MismatchedPresenceIsRejected() =>
        Assert.Throws<ArgumentException>(() => PointwiseComparisonPublisher.Publish([1d], []));

    private static void AssertOutput(ComparisonSeries expected, ComparisonSeries actual)
    {
        Assert.Equal(expected.Outputs.Keys, actual.Outputs.Keys);
        var first = expected.Outputs["Value"]; var second = actual.Outputs["Value"];
        Assert.Equal(first.FirstValid, second.FirstValid);
        Assert.Equal(first.Present, second.Present);
        Assert.Equal(first.Values.Select(BitConverter.DoubleToInt64Bits),
            second.Values.Select(BitConverter.DoubleToInt64Bits));
    }
}
