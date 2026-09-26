using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AlmaCompositionNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Name.StartsWith("alma-composition/", StringComparison.Ordinal)
            || c.Name.Contains("-composition/", StringComparison.Ordinal) && c.Name.EndsWith("/20", StringComparison.Ordinal))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
    {
        var cases = Cases.Select(row => (IndicatorValidationCase)row[0]).ToArray();
        Assert.Equal(84, cases.Length);
        foreach (var type in new[] { typeof(Tma), typeof(TriangularMovingAverage), typeof(SlowSmoothedMovingAverage),
            typeof(MiddleHighLowMovingAverage), typeof(SequentiallyFilteredMovingAverage) })
            Assert.Equal(3, cases.Count(c => c.IndicatorType == type));
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task CompositionsReceiveEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public void NativeCompositionsAndLegacyMatchIndependentStages(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars;
            var kind = MovingAvgType.ArnaudLegouxMovingAverage;
            var referenceKind = 20;
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            var expected = new[]
            {
                BuiltInFormulaReferences.RoundedTriangularMean(bars, length, referenceKind),
                BuiltInFormulaReferences.RoundedSlowMean(bars, length, referenceKind),
                BuiltInFormulaReferences.RoundedMiddleMean(bars, length, length, referenceKind),
                BuiltInFormulaReferences.RoundedSequentialMean(bars, length, referenceKind)
            };
            var legacy = new[]
            {
                Data().CalculateTriangularMovingAverage(kind, length).CustomValuesList,
                Data().CalculateSlowSmoothedMovingAverage(kind, length).CustomValuesList,
                Data().CalculateMiddleHighLowMovingAverage(kind, length, length).CustomValuesList,
                Data().CalculateSequentiallyFilteredMovingAverage(kind, length).CustomValuesList
            };
            var states = new IStreamingIndicatorState[]
            {
                new TriangularMovingAverageState(kind, length), new SlowSmoothedMovingAverageState(kind, length),
                new MiddleHighLowMovingAverageState(kind, length, length), new SequentiallyFilteredMovingAverageState(kind, length)
            };
            for (var route = 0; route < states.Length; route++)
            {
                Assert.Equal(expected[route], legacy[route]);
                var state = states[route];
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var bar = new OhlcvBar("STAGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[route][i], state.Update(bar, false, true).Value);
                        Assert.Equal(expected[route][i], state.Update(bar, true, true).Value);
                    }
                }
            }
        }
    }
}
