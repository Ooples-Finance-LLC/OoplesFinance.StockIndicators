using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SlowMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedSlowMean(b))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task DiscoveredConfigurationsReceiveAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(1600)]
    [InlineData(int.MaxValue)]
    public void SplitWindowsAndTheirCapsMatchIndependentRoundedStages(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 245))
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod,
            MovingAvgType.SymmetricallyWeightedMovingAverage, MovingAvgType.EhlersTriangleMovingAverage,
            MovingAvgType.FibonacciWeightedMovingAverage, MovingAvgType.SquareRootWeightedMovingAverage,
            MovingAvgType.ParabolicWeightedMovingAverage, MovingAvgType.CubedWeightedMovingAverage, MovingAvgType.QuickMovingAverage, MovingAvgType.JsaMovingAverage, MovingAvgType.QuadraticMovingAverage, MovingAvgType.KaufmanAdaptiveMovingAverage })
        {
            var bars = fixture.Bars.ToArray();
            var referenceKind = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2
                : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6
                : kind == MovingAvgType.FibonacciWeightedMovingAverage ? 8 : kind == MovingAvgType.SquareRootWeightedMovingAverage ? 9
                : kind == MovingAvgType.ParabolicWeightedMovingAverage ? 10 : kind == MovingAvgType.CubedWeightedMovingAverage ? 11
                : kind == MovingAvgType.QuickMovingAverage ? 12 : kind == MovingAvgType.JsaMovingAverage ? 13
                : kind == MovingAvgType.QuadraticMovingAverage ? 14 : kind == MovingAvgType.KaufmanAdaptiveMovingAverage ? 15 : 7;
            var expected = BuiltInFormulaReferences.RoundedSlowMean(bars, length, referenceKind);
            var close = bars.Select(b => b.Close).ToList();
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
                bars.Select(b => b.Low).ToList(), close, bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            var actual = new double[bars.Length];
            MovingAverageCore.SlowSmoothedMovingAverage(close.ToArray(), actual, length, kind);
            Assert.Equal(expected, actual);
            if (referenceKind == 2)
            {
                MovingAverageCore.SlowSmoothedMovingAverage(close.ToArray(), actual, length);
                Assert.Equal(expected, actual);
            }
            Assert.Equal(expected, Data().CalculateSlowSmoothedMovingAverage(kind, length).CustomValuesList);
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeSlowSmoothedMovingAverageFast(Data(), context, length, kind);
            Assert.Equal(expected, buffer.ToArray());
            using var state = new SlowSmoothedMovingAverageState(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("SLOW", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, true, true).Value);
                }
            }
        }
    }
}
