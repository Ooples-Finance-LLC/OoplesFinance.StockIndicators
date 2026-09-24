using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TriangularNumericalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task RoundedStagesAgreeAcrossAliasesRoutesAndPreviews(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(48, 245))
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod,
            MovingAvgType.SymmetricallyWeightedMovingAverage, MovingAvgType.EhlersTriangleMovingAverage,
            MovingAvgType.FibonacciWeightedMovingAverage, MovingAvgType.SquareRootWeightedMovingAverage,
            MovingAvgType.ParabolicWeightedMovingAverage, MovingAvgType.CubedWeightedMovingAverage, MovingAvgType.QuickMovingAverage, MovingAvgType.JsaMovingAverage, MovingAvgType.QuadraticMovingAverage, MovingAvgType.KaufmanAdaptiveMovingAverage })
        {
            var bars = fixture.Bars.ToArray();
            var referenceKind = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3
                : kind == MovingAvgType.WildersSmoothingMethod ? 6 : kind == MovingAvgType.FibonacciWeightedMovingAverage ? 8 : kind == MovingAvgType.SquareRootWeightedMovingAverage ? 9
                : kind == MovingAvgType.ParabolicWeightedMovingAverage ? 10 : kind == MovingAvgType.CubedWeightedMovingAverage ? 11
                : kind == MovingAvgType.QuickMovingAverage ? 12 : kind == MovingAvgType.JsaMovingAverage ? 13
                : kind == MovingAvgType.QuadraticMovingAverage ? 14 : kind == MovingAvgType.KaufmanAdaptiveMovingAverage ? 15 : 7;
            var expected = BuiltInFormulaReferences.RoundedTriangularMean(bars, length, referenceKind);
            IMovingAverage Average() => referenceKind == 1 ? new Sma() : referenceKind == 2 ? new Wma() : referenceKind == 3 ? new Ema()
                : referenceKind == 6 ? new Wwma() : referenceKind == 8 ? new FibonacciWeightedMovingAverage() : referenceKind == 9 ? new SquareRootWeightedMovingAverage()
                : referenceKind == 10 ? new ParabolicWma() : referenceKind == 11 ? new CubedWeightedMovingAverage()
                : referenceKind == 12 ? new QuickMovingAverage() : referenceKind == 13 ? new JsaMovingAverage()
                : referenceKind == 14 ? new QuadraticMovingAverage() : referenceKind == 15 ? new Kama()
                : kind == MovingAvgType.SymmetricallyWeightedMovingAverage ? new SymmetricallyWeightedMovingAverage() : new EhlersTriangleMovingAverage();
            var indicator = new TriangularMovingAverage(length, Average());
            var alias = new Tma(length, Average());
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator, alias).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            Assert.Equal(expected, run[alias].ToArray());
            var close = bars.Select(b => b.Close).ToList();
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
                bars.Select(b => b.Low).ToList(), close, bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateTriangularMovingAverage(kind, length).CustomValuesList);
            if (referenceKind == 1)
            {
                var actual = new double[bars.Length];
                MovingAverageCore.TriangularMovingAverage(close.ToArray(), actual, length);
                Assert.Equal(expected, actual);
            }
            using var state = new TriangularMovingAverageState(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("TMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, true, true).Value);
                }
            }
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedTriangularMean(b))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryDiscoveredConvexConfigurationReceivesAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }
}
