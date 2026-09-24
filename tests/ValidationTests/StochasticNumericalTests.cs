using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class StochasticNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType != typeof(StochasticFastOscillator)
            && c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedStochastic(b))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(255, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("stochastic-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ExactRangePreservesOverflowSubnormalReversedAndClampedCases()
    {
        Assert.Equal(50, ClampedRangePosition.Percent(0, -double.MaxValue, double.MaxValue));
        Assert.Equal(double.Epsilon, ClampedRangePosition.Percent(double.Epsilon, 0, 100));
        Assert.Equal(50, ClampedRangePosition.Percent(0, -double.Epsilon, double.Epsilon));
        Assert.Equal(50, ClampedRangePosition.Percent(0, 1, -1));
        Assert.Equal(0, ClampedRangePosition.Percent(2, 1, -1));
        Assert.Equal(100, ClampedRangePosition.Percent(-2, 1, -1));
        Assert.Equal(0, ClampedRangePosition.Percent(double.MaxValue, 1, 1));
        Assert.Equal(100, ClampedRangePosition.Percent(double.MaxValue, 0, double.Epsilon));
        Assert.Equal(0, ClampedRangePosition.Percent(-double.MaxValue, 0, double.Epsilon));
        var output = new double[2];
        OscillatorCore.StochasticK(new[] { double.MaxValue, double.MaxValue }, new[] { -double.MaxValue, -double.MaxValue },
            new[] { 0d, 0d }, output, int.MaxValue);
        Assert.Equal(new[] { 50d, 50d }, output);
        OscillatorCore.StochasticD(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Span<double>.Empty, int.MaxValue, int.MaxValue);
    }

    [Theory]
    [InlineData(1, 3, 7)]
    [InlineData(3, 1, 3)]
    [InlineData(14, 3, 1)]
    public async Task AllOutputsMatchEveryRoute(int length, int first, int last)
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
            var expected = BuiltInFormulaReferences.RoundedStochastic(bars, length, first, last, kind);
            var indicator = new StochasticOscillator(length, first, last, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected["FastK"], run[indicator].ToArray());
            Assert.Equal(expected["FastD"], run[indicator.FastD].ToArray());
            Assert.Equal(expected["SlowD"], run[indicator.SlowD].ToArray());
            var data = Data(bars);
            data.CalculateStochasticOscillator(maType, length, first, last);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
            var core = new double[bars.Count];
            OscillatorCore.StochasticK(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
                bars.Select(b => b.Close).ToArray(), core, length);
            Assert.Equal(expected["FastK"], core);
            if (kind == 1)
            {
                OscillatorCore.StochasticD(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
                    bars.Select(b => b.Close).ToArray(), core, length, first);
                Assert.Equal(expected["FastD"], core);
            }
            using var state = new StochasticOscillatorState(maType, length, first, last);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Count; i++)
                foreach (var commit in new[] { false, true })
                {
                    var result = state.Update(NativeBar(bars[i]), commit, true);
                    Assert.Equal(expected["FastK"][i], result.Value);
                    foreach (var (key, values) in expected)
                    {
                        Assert.Equal(values[i], result.Outputs![key]);
                        Assert.InRange(values[i], 0, 100);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task FactoriesAndTypedChainingKeepTheDeclaredOhlcFields()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/alternating-scale")).Bars;
        var source = new Sma(2);
        IIndicator[] indicators = { new Stochastic(3, 7), new StochasticK(3, 7, 1), new StochasticD(3),
            new PricePosition(3, 7, 1), new StochasticOscillator(3, 7, 1) };
        foreach (var indicator in indicators)
            if (indicator is MultiOutputIndicatorBase multi) multi.Of(source);
            else ((IndicatorBase)indicator).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new IIndicator[] { source }.Concat(indicators).ToArray()).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        foreach (var indicator in indicators)
        {
            var builtIn = (IBuiltInIndicator)indicator;
            var first = indicator is StochasticD ? 3 : 7;
            var last = indicator is Stochastic or StochasticD ? 3 : 1;
            var expected = BuiltInFormulaReferences.RoundedStochastic(projected, 3, first, last, 1);
            Assert.Equal(expected[indicator is StochasticD ? "FastD" : "FastK"], run[indicator].ToArray());
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var i = 0; i < projected.Length; i++)
                {
                    var result = state.Update(NativeBar(projected[i]), true, true);
                    foreach (var (key, values) in expected) Assert.Equal(values[i], result.Outputs![key]);
                }
            }
        }
    }

    [Fact]
    public void LegacySelectedSeriesUsesItsOwnRangeWhenOutsidePriceBars()
    {
        var bars = new[] { 100d, 101d, 102d }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        foreach (var chained in new[] { false, true })
        {
            var data = Data(bars);
            if (chained) data.CustomValuesList = new() { 1, 2, 3 };
            else data.InputValues = new() { 1, 2, 3 };
            using var context = new ComputeContext();
            using var compact = IndicatorCompute.ComputeStochasticKFast(data, context, 2);
            using var full = IndicatorCompute.ComputeStochasticOscillatorFast(data, context, 2);
            using var regular = IndicatorCompute.ComputeStochasticRegularFast(data, context, 2);
            Assert.Equal(compact.ToArray(), regular.ToArray());
            Assert.Equal(new[] { 0d, 100, 100 }, compact.ToArray());
            Assert.Equal(compact.ToArray(), full.ToArray());
            data.CalculateStochasticOscillator(length: 2);
            Assert.Equal(compact.ToArray(), data.OutputValues["FastK"]);
        }
    }

    [Fact]
    public void DiscoveryIncludesDoubleStochasticCompositions()
        => Assert.Equal(51, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("double-stochastic-composition/")));

    [Theory]
    [InlineData(1, 7)]
    [InlineData(3, 1)]
    [InlineData(14, 3)]
    public async Task DoubleStochasticUsesTwoIndependentRangeWindows(int length, int smoothing)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        foreach (var (average, kind) in new (IMovingAverage, int)[] { (new Sma(), 1), (new Wma(), 2), (new Ema(), 3), (new Vidya(), 19), (new FibonacciWeightedMovingAverage(), 8) })
        {
            var bars = fixture.Bars;
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            var expected = BuiltInFormulaReferences.RoundedDoubleStochastic(bars, length, kind, smoothing);
            var data = Data(bars);
            data.CalculateDoubleStochasticOscillator(maType, length, smoothing);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
            using var state = new DoubleStochasticOscillatorState(maType, length, smoothing);
            Check(state, expected);
            var indicator = new DoubleStochasticOscillator(length, average);
            var fixedExpected = BuiltInFormulaReferences.RoundedDoubleStochastic(bars, length, kind);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(fixedExpected["Dso"], run[indicator].ToArray());
            Assert.Equal(fixedExpected["Signal"], run[indicator.Signal].ToArray());
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var factoryState in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(factoryState);
                using var lifetime = factoryState as IDisposable;
                Check(factoryState, fixedExpected);
            }
            void Check(IStreamingIndicatorState engine, IReadOnlyDictionary<string, double[]> reference)
            {
                for (var replay = 0; replay < 2; replay++)
                {
                    engine.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var result = engine.Update(NativeBar(bars[i]), commit, true);
                        foreach (var (key, values) in reference)
                        {
                            Assert.Equal(values[i], result.Outputs![key]);
                            Assert.InRange(values[i], 0, 100);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void DiscoveryIncludesDynamicMomentumCompositions()
        => Assert.Equal(51, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("dynamic-momentum-composition/")));

    [Theory]
    [InlineData(1, 7)]
    [InlineData(3, 1)]
    [InlineData(10, 20)]
    public async Task DynamicMomentumUsesIndependentPrefixExtrema(int length, int slowPeriod)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        foreach (var (average, kind) in new (IMovingAverage, int)[] { (new Sma(), 1), (new Wma(), 2), (new Ema(), 3), (new Vidya(), 19) })
        {
            var bars = fixture.Bars;
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            var expected = BuiltInFormulaReferences.RoundedDynamicMomentum(bars, length, kind, slowPeriod);
            var data = Data(bars);
            data.CalculateDynamicMomentumOscillator(maType, length, slowPeriod);
            Assert.Equal(expected, data.OutputValues["Dmo"]);
            using var state = new DynamicMomentumOscillatorState(maType, length, slowPeriod);
            Check(state, expected);
            var indicator = new DynamicMomentumOscillator(length, average);
            var fixedExpected = BuiltInFormulaReferences.RoundedDynamicMomentum(bars, length, kind);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(fixedExpected, run[indicator].ToArray());
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var factoryState in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(factoryState);
                using var lifetime = factoryState as IDisposable;
                Check(factoryState, fixedExpected);
            }
            void Check(IStreamingIndicatorState engine, double[] reference)
            {
                for (var replay = 0; replay < 2; replay++)
                {
                    engine.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var result = engine.Update(NativeBar(bars[i]), commit, true);
                        Assert.Equal(reference[i], result.Value);
                        Assert.InRange(result.Value, 0, 100);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(3, 1)]
    [InlineData(14, 3)]
    public async Task RegularAllRoutesUseTheIndependentStochasticReference(int length, int signal)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedStochastic(bars, length, signal, 3, 1);
            var indicator = new StochasticRegular(length, signal);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected["FastK"], run[indicator].ToArray());
            Assert.Equal(expected["FastD"], run[indicator.Signal].ToArray());
            var data = Data(bars);
            data.CalculateStochasticRegular(length1: length, length2: signal);
            Assert.Equal(expected["FastK"], data.OutputValues["Sco"]);
            Assert.Equal(expected["FastD"], data.OutputValues["Signal"]);
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(NativeBar(bars[i]), commit, true);
                        Assert.Equal(expected["FastK"][i], result.Outputs!["Sco"]);
                        Assert.Equal(expected["FastD"][i], result.Outputs["Signal"]);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public void DependentNativeStatesPreserveBatchOutputsAcrossPreviewAndReset(int period)
    {
        var bars = Enumerable.Range(0, 48).Select(i => {
            var close = 20 + Math.Sin(i * 1.7) * 8;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), close, close + 1 + i % 3,
                close - 2 - i % 5, close, 1);
        }).ToArray();
        var cases = new (IStreamingIndicatorState State, Action<StockData> Batch)[] {
            (new DiNapoliPreferredStochasticOscillatorState(period, 3, 7),
                d => d.CalculateDiNapoliPreferredStochasticOscillator(period, 3, 7)),
            (new DoubleStochasticOscillatorState(length: period, smoothLength: 3),
                d => d.CalculateDoubleStochasticOscillator(length: period, smoothLength: 3)),
            (new DynamicMomentumOscillatorState(length1: period, length2: 7),
                d => d.CalculateDynamicMomentumOscillator(length1: period, length2: 7)),
            (new StochasticRegularState(length1: period, length2: 3),
                d => d.CalculateStochasticRegular(length1: period, length2: 3))
        };
        foreach (var (state, batch) in cases)
        {
            using var lifetime = state as IDisposable;
            var data = Data(bars);
            batch(data);
            Assert.Contains(data.OutputValues.Values.SelectMany(v => v), value => value != 0);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var commit in new[] { false, true })
                {
                    var result = state.Update(NativeBar(bars[i]), commit, true);
                    foreach (var (key, values) in data.OutputValues)
                        Assert.True(values[i] == result.Outputs![key],
                            $"{state.Name}.{key}, period {period}, bar {i}, final {commit}: {values[i]:R} != {result.Outputs[key]:R}");
                }
            }
        }
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar NativeBar(Bar b) => new("STOCH", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
}
