using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SharedAverageReferenceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(1, 9)]
    [InlineData(1, 64)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    [InlineData(2, 9)]
    [InlineData(2, 64)]
    [InlineData(3, 1)]
    [InlineData(3, 3)]
    [InlineData(3, 9)]
    [InlineData(3, 64)]
    public void MaintainedExactSumsMatchIndependentWindowRecomputation(int kind, int length)
    {
        var fixtures = IndicatorAdversarialCases.Generate(32, 244)
            .Select(fixture => fixture.Bars.Select(bar => bar.Close).ToArray())
            .Append(new[] { double.MaxValue, 1d, -double.MaxValue, double.Epsilon, 0d, 7d, -3d, 2d });
        foreach (var values in fixtures)
        {
            var expected = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                if (kind == 1 && i + 1 < length) continue;
                if (kind == 3 && i >= length)
                {
                    var previous = ReferenceFraction.FromDouble(expected[i - 1]);
                    expected[i] = (previous + new ReferenceFraction(2) *
                        (ReferenceFraction.FromDouble(values[i]) - previous) / new ReferenceFraction(length + 1L)).ToDouble();
                    continue;
                }
                var exact = new ReferenceFraction(0);
                for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                    exact += ReferenceFraction.FromDouble(values[j]) * new ReferenceFraction(kind == 2 ? length - i + j : 1);
                var denominator = kind == 2 ? (long)length * (length + 1L) / 2 : kind == 3 ? i + 1 : length;
                expected[i] = (exact / new ReferenceFraction(denominator)).ToDouble();
            }
            Assert.Equal(expected, BuiltInFormulaReferences.Average(values, length, kind));
        }
    }

    [Theory]
    [InlineData(1, 0, 0, 4)]
    [InlineData(2, 1, 8d / 3, 14d / 3)]
    [InlineData(3, 2, 3, 4)]
    [InlineData(6, 2d / 3, 16d / 9, 86d / 27)]
    public void StartupKeepsTheDeclaredMeanPolicy(int kind, double first, double second, double third)
    {
        var actual = BuiltInFormulaReferences.Average(new[] { 2d, 4d, 6d }, 3, kind);
        Assert.Equal(first, actual[0]);
        Assert.Equal(second, actual[1]);
        // Wilder rounds each recursive stage, rather than only the final fraction.
        var expectedThird = kind == 6
            ? ((ReferenceFraction.FromDouble(second) * new ReferenceFraction(2) + new ReferenceFraction(6)) / new ReferenceFraction(3)).ToDouble()
            : third;
        Assert.Equal(expectedThird, actual[2]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConstantExtremeMeanIsRepresentable(int kind)
    {
        var actual = BuiltInFormulaReferences.Average(Enumerable.Repeat(double.MaxValue, 8).ToArray(), 3, kind);
        Assert.All(actual.Skip(2), value => Assert.Equal(double.MaxValue, value));
    }

    [Fact]
    public void CancellationRetainsTheSmallResidual()
    {
        var values = new[] { double.MaxValue, 1d, -double.MaxValue };
        Assert.Equal(1d / 3, BuiltInFormulaReferences.Average(values, 3, 1)[2]);
        Assert.Equal(1d / 3, BuiltInFormulaReferences.Average(values, 3, 3)[2]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void UnitPeriodHandlesOppositeExtremes(int kind)
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.MaxValue };
        Assert.Equal(values, BuiltInFormulaReferences.Average(values, 1, kind));
    }

    [Fact]
    public void RecursiveMeansAvoidOverflowingCorrections()
    {
        var maximum = double.MaxValue;
        Assert.Equal(-maximum / 3, BuiltInFormulaReferences.Average(new[] { maximum, maximum, -maximum }, 2, 3)[2]);
        Assert.Equal(new[] { maximum / 2, -maximum / 4 },
            BuiltInFormulaReferences.Average(new[] { maximum, -maximum }, 2, 6));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void EmptyInputsAndNonfiniteUpstreamStagesKeepTheirPolicies(int kind)
    {
        Assert.Empty(BuiltInFormulaReferences.Average(Array.Empty<double>(), 3, kind));
        var result = BuiltInFormulaReferences.Average(new[] { double.PositiveInfinity }, 1, kind);
        Assert.Equal(double.PositiveInfinity, result[0]);
    }
}
