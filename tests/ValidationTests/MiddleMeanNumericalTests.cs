using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MiddleMeanNumericalTests
{
    [Fact]
    public void SharedDiscoveryIncludesEveryPromotedMiddleComposition()
    {
        var cases = Cases.Select(row => (IndicatorValidationCase)row[0]).Where(c => c.Name.StartsWith("middle-composition/")).ToArray();
        Assert.Equal(52, cases.Length);
        Assert.Equal(52, cases.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void ExactSimpleStagePreservesCustomerOverrides()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/large")).Bars;
        var midpoint = BuiltInFormulaReferences.RoundedMiddleMean(bars, 1, 7, 3);
        var expected = midpoint.Select(value => value / 2).ToArray();
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            using var scope = ComponentAverage.Arm((values, length) =>
            {
                Assert.Equal(3, length);
                Assert.Equal(midpoint, values);
                return values.Select(value => value / 2).ToArray();
            });
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeMiddleHighLowMovingAverageFast(data, context, 3, 7, MovingAvgType.SimpleMovingAverage);
            Assert.Equal(expected, result.ToArray());
            Assert.Equal(1, ComponentAverage.Requests);
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedMiddleMean(b))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task DiscoveredConfigurationsReceiveEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 14)]
    [InlineData(14, 3)]
    [InlineData(14, 10)]
    public async Task MidpointThenAverageMatchesEveryRoute(int smoothing, int window)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var referenceKind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })
        {
            var bars = fixture.Bars;
            IMovingAverage average = referenceKind switch
            {
                1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                _ => throw new InvalidOperationException()
            };
            var kind = ((IBuiltInMovingAverage)average).AvgType;
            var expected = BuiltInFormulaReferences.RoundedMiddleMean(bars, smoothing, window, referenceKind);
            var indicator = new MiddleHighLowMovingAverage(smoothing, window, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var close = bars.Select(b => b.Close).ToArray();
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
                bars.Select(b => b.Low).ToList(), close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, Data().CalculateMiddleHighLowMovingAverage(kind, smoothing, window).CustomValuesList);
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeMiddleHighLowMovingAverageFast(Data(), context, smoothing, window, kind);
            Assert.Equal(expected, buffer.ToArray());
            if (referenceKind == 3)
            {
                var actual = new double[bars.Count];
                MovingAverageCore.MiddleHighLowMovingAverage(close, actual, smoothing, window);
                Assert.Equal(expected, actual);
            }
            using var state = new MiddleHighLowMovingAverageState(kind, smoothing, window);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var bar = new OhlcvBar("MHL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(bar, false, true).Outputs!["Mhlma"]);
                    Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                }
            }
        }
    }

    [Fact]
    public async Task RegistryAndPublicChainsUseTheSelectedScalarSeries()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/mixed-ohlc-extremes")).Bars;
        var source = new Sma(2);
        var middle = new MiddleHighLowMovingAverage(3, 10);
        middle.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, middle).BuildAsync();
        var values = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, values[i], values[i], values[i], values[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMiddleMean(projected, 3, 10, 3);
        Assert.Equal(expected, run[middle].ToArray());
        var actual = new double[bars.Count];
        var core = new MhlmaCore();
        Assert.False(core.RequiresOhlc);
        core.Compute(values, actual, 3);
        Assert.Equal(expected, actual);
        core.ComputeOhlc(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), values, actual, 3);
        Assert.Equal(expected, actual);
        MovingAverageCore.MiddleHighLowMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue, int.MaxValue);
    }
}
