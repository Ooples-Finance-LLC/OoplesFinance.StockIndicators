using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FilteredChandeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedFilteredChande(b))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesAllPromotedComponents()
        => Assert.Equal(51, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("chande-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void RoundedThresholdDiscardsLargeChangesAndRetainsExactSmallSignals()
    {
        using var window = new ChandeMomentumWindow(2, 3);
        Assert.Equal(0, window.Next(0, true));
        Assert.Equal(0, window.Next(4, true));
        Assert.Equal(100, window.Next(5, true));
        Assert.Equal(0, window.Next(4, true));
        window.Reset();
        Assert.Equal(0, window.Next(-double.Epsilon, true));
        Assert.Equal(100, window.Next(3, true));
        Assert.Equal(17 * double.Epsilon, window.Next(0, false));
        Assert.Equal(17 * double.Epsilon, window.Next(0, true));
        window.Reset();
        Assert.Equal(0, window.Next(-double.MaxValue, true));
        Assert.Equal(0, window.Next(double.MaxValue, false));
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var commit in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => window.Next(value, commit));
        Assert.Equal(0, window.Next(double.MaxValue, true));
        Assert.Equal(0, window.Next(0, true));
        Assert.Equal(100, window.Next(3, true));
        foreach (var limit in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChandeMomentumWindow(2, limit));
        using var zero = new ChandeMomentumWindow(2, 0);
        Assert.Equal(0, zero.Next(1, true));
        Assert.Equal(0, zero.Next(2, true));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(3d)]
    [InlineData(double.MaxValue)]
    public void DirectThresholdParametersMatchTheReference(double filter)
    {
        var bars = new[] { 0d, 0.5, -0.5, 4, 3, -double.Epsilon, 3, 0, -double.MaxValue, double.MaxValue }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedFilteredChande(bars, 2, filter);
        var signal = expected.Select((v, i) => ((ReferenceFraction.FromDouble(v)
            + ReferenceFraction.FromDouble(i == 0 ? 0 : expected[i - 1])) / new ReferenceFraction(2)).ToDouble()).ToArray();
        var data = Data(bars);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeChandeMomentumOscillatorFilterFast(data, context, 2, filter);
        Assert.Equal(expected, fast.ToArray());
        data.CalculateChandeMomentumOscillatorFilter(MovingAvgType.SimpleMovingAverage, 2, filter);
        Assert.Equal(expected, data.OutputValues["Cmof"]);
        Assert.Equal(signal, data.OutputValues["Signal"]);
        using var state = new ChandeMomentumOscillatorFilterState(MovingAvgType.SimpleMovingAverage, 2, filter);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var bar = new OhlcvBar("LIMIT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            foreach (var commit in new[] { false, true })
            {
                var actual = state.Update(bar, commit, true);
                Assert.Equal(expected[i], actual.Value);
                Assert.Equal(signal[i], actual.Outputs!["Signal"]);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    public async Task BothOutputsMatchEveryRoute(int length)
    {
        var averages = new (IMovingAverage Average, int Kind)[] {
            (new Sma(),1), (new Wma(),2), (new Ema(),3), (new Wwma(),6),
            (new SymmetricallyWeightedMovingAverage(),7), (new FibonacciWeightedMovingAverage(),8),
            (new SquareRootWeightedMovingAverage(),9), (new ParabolicWma(),10), (new CubedWeightedMovingAverage(),11),
            (new QuickMovingAverage(),12), (new JsaMovingAverage(),13), (new QuadraticMovingAverage(),14),
            (new Kama(),15), (new SineWma(),16), (new NaturalMa(),17), (new EhlersHannMovingAverage(),18), (new Vidya(),19) };
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var (average, kind) in averages)
        {
            var bars = fixture.Bars;
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            var expected = BuiltInFormulaReferences.RoundedFilteredChande(bars, length);
            var signal = BuiltInFormulaReferences.RoundedFilteredChandeSignal(bars, length, kind);
            var indicator = new ChandeMomentumOscillatorFilter(length, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            Assert.Equal(signal, run[indicator.Signal].ToArray());
            var data = Data(bars);
            data.CalculateChandeMomentumOscillatorFilter(maType, length);
            Assert.Equal(expected, data.OutputValues["Cmof"]);
            Assert.Equal(signal, data.OutputValues["Signal"]);
            var spec = new IndicatorSpec(IndicatorName.ChandeMomentumOscillatorFilter, new ChandeMomentumOscillatorFilterSpecOptions(length, maType));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var bar = new OhlcvBar("FILTER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var actual = state.Update(bar, commit, true);
                            Assert.Equal(expected[i], actual.Value);
                            Assert.Equal(signal[i], actual.Outputs!["Signal"]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ChainingAndCustomerSignalUseTheSelectedTrajectory()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/alternating-scale")).Bars;
        var source = new Sma(2);
        var indicator = new ChandeMomentumOscillatorFilter(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedFilteredChande(projected, 3);
        Assert.Equal(expected, run[indicator].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = Data(bars);
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var primary = IndicatorCompute.ComputeChandeMomentumOscillatorFilterFast(data, context, 3);
            Assert.Equal(expected, primary.ToArray());
            using var scope = ComponentAverage.Arm((input, _) => input.Select(v => v / 2).ToArray());
            using var signal = IndicatorCompute.ComputeChandeMomentumOscillatorFilterSignalFast(data, context, 3, MovingAvgType.SimpleMovingAverage);
            Assert.Equal(expected.Select(v => v / 2), signal.ToArray());
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}
