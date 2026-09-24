using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ChandeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedChande(b))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEverySignalComponent()
        => Assert.Equal(198, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("chande-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task DiscoveredConfigurationsReceiveEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void HandVectorsCoverExtremeDifferencesSubnormalsEvictionAndPreview()
    {
        var values = new[] { 1d, 2d, 4d, 3d, 1d };
        var actual = new double[values.Length];
        OscillatorCore.ChandeMomentumOscillator(values, actual, 2);
        Assert.Equal(new[] { 0d, 100d, 100d, 100d / 3, -100d }, actual);
        var tiny = new[] { -double.Epsilon, double.Epsilon, 0, double.Epsilon };
        var small = new double[4];
        OscillatorCore.ChandeMomentumOscillator(tiny, small, 2);
        Assert.Equal(new[] { 0d, 100d, 100d / 3, 0d }, small);
        OscillatorCore.ChandeMomentumOscillator(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        var shortHistory = new double[2];
        OscillatorCore.ChandeMomentumOscillator(new[] { -double.MaxValue, double.MaxValue }, shortHistory, int.MaxValue);
        Assert.Equal(new[] { 0d, 100d }, shortHistory);
        using var state = new OoplesFinance.StockIndicators.Helpers.ChandeMomentumWindow(2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            Assert.Equal(0, state.Next(-double.MaxValue, true));
            state.Next(0, false);
            Assert.Equal(100, state.Next(double.MaxValue, false));
            Assert.Equal(100, state.Next(double.MaxValue, true));
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var commit in new[] { false, true })
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Next(invalid, commit));
            state.Next(1, true);
            state.Next(2, true);
            Assert.Equal(100, state.Next(3, true));
            Assert.Equal(100, state.Next(3, true));
            Assert.Equal(0, state.Next(3, true));
        }
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    [InlineData(3, 7)]
    [InlineData(14, 3)]
    public async Task BothOutputsMatchEveryRoute(int length, int signalLength)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 })
        {
            var bars = fixture.Bars;
            IMovingAverage average = kind switch
            {
                1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(),
                _ => throw new InvalidOperationException()
            };
            var movingKind = ((IBuiltInMovingAverage)average).AvgType;
            var expected = BuiltInFormulaReferences.RoundedChande(bars, length);
            var expectedSignal = BuiltInFormulaReferences.RoundedChandeSignal(bars, length, signalLength, kind);
            var indicator = new ChandeMomentumOscillator(length, signalLength, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            Assert.Equal(expectedSignal, run[indicator.Signal].ToArray());
            var data = Data(bars);
            data.CalculateChandeMomentumOscillator(movingKind, length, signalLength);
            Assert.Equal(expected, data.OutputValues["Cmo"]);
            Assert.Equal(expectedSignal, data.OutputValues["Signal"]);
            var actual = new double[bars.Count];
            OscillatorCore.ChandeMomentumOscillator(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            using var state = new ChandeMomentumOscillatorState(movingKind, length, signalLength);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var bar = NativeBar(b);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(bar, commit, true);
                        Assert.Equal(expected[i], result.Value);
                        Assert.Equal(expectedSignal[i], result.Outputs!["Signal"]);
                        Assert.InRange(result.Value, -100, 100);
                    }
                }
            }
        }
    }

    [Fact]
    public void VidyaConsumersUseTheSameMomentumIncludingPreviewAndReset()
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var length in new[] { 1, 3, 14 })
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedVidya(bars, length);
            var actual = new double[bars.Count];
            MovingAverageCore.Vidya(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            for (var i = 0; i < actual.Length; i++)
            {
                Assert.True(double.IsFinite(actual[i]));
                Assert.InRange(actual[i], bars.Take(i + 1).Min(b => b.Close), bars.Take(i + 1).Max(b => b.Close));
            }
            var data = Data(bars);
            data.CalculateVariableIndexDynamicAverage(length: length);
            Assert.Equal(expected, data.OutputValues["Vidya"]);
            using var engine = new VariableIndexDynamicAverageEngine(length);
            using var native = new VariableIndexDynamicAverageState(length: length);
            for (var replay = 0; replay < 2; replay++)
            {
                engine.Reset();
                native.Reset();
                for (var i = 0; i < bars.Count; i++)
                foreach (var commit in new[] { false, true })
                {
                    Assert.Equal(expected[i], engine.Next(bars[i].Close, commit));
                    Assert.Equal(expected[i], native.Update(NativeBar(bars[i]), commit, true).Value);
                }
            }
        }
    }

    [Fact]
    public void FactoriesPreserveBothAliasPeriodsAndSignalLengths()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/alternating-scale")).Bars;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var options in new IIndicatorSpecOptions[] { new CmoSpecOptions(length), new ChandeMomentumOscillatorSpecOptions(length, 7),
            new ChandeMomentumOscillatorSignalSpecOptions(length, 7) })
        {
            var expected = BuiltInFormulaReferences.RoundedChande(bars, length);
            var signalLength = options is CmoSpecOptions ? 3 : 7;
            var signal = BuiltInFormulaReferences.RoundedChandeSignal(bars, length, signalLength, 3);
            var spec = new IndicatorSpec(IndicatorName.ChandeMomentumOscillator, options);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var i = 0; i < bars.Count; i++)
                {
                    var result = state.Update(NativeBar(bars[i]), true, true);
                    Assert.Equal(expected[i], result.Value);
                    Assert.Equal(signal[i], result.Outputs!["Signal"]);
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputAndCustomerSignalArePreserved()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/alternating-scale")).Bars;
        var source = new Sma(2);
        var aliases = new MultiOutputIndicatorBase[] { new Cmo(3), new ChandeMomentumOscillator(3, 7) };
        foreach (var alias in aliases) alias.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new IIndicator[] { source }.Concat(aliases).ToArray()).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedChande(projected, 3);
        foreach (var alias in aliases) Assert.Equal(expected, run[alias].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = Data(bars);
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var primary = IndicatorCompute.ComputeCmoFast(data, context, 3);
            Assert.Equal(expected, primary.ToArray());
            using var scope = ComponentAverage.Arm((input, _) => input.Select(value => value / 2).ToArray());
            using var signal = IndicatorCompute.ComputeChandeMomentumOscillatorSignalFast(data, context, 3, 7, MovingAvgType.SimpleMovingAverage);
            Assert.Equal(expected.Select(v => v / 2), signal.ToArray());
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
        bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
    private static OhlcvBar NativeBar(Bar b) => new("CMO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
}
