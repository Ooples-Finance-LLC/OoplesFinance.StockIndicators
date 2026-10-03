using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ChandeAverageNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName is IndicatorName.ChandeMomentumOscillatorAverage
            or IndicatorName.ChandeMomentumOscillatorAbsoluteAverage).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ZeroOriginUnitPeriodsAndRejectedInputHaveExplicitContracts()
    {
        using var state = new ChandeMomentumAverageWindow(1, 1, 1);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            Assert.Equal(-100, state.Next(-double.MaxValue, true));
            Assert.Equal(100, state.Next(double.MaxValue, false));
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var commit in new[] { false, true })
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Next(value, commit));
            Assert.Equal(100, state.Next(double.MaxValue, true));
            Assert.Equal(-100, state.Next(0, true));
            Assert.Equal(100, state.Next(double.Epsilon, true));
            Assert.Equal(0, state.Next(double.Epsilon, true));
        }
        OscillatorCore.ChandeMomentumOscillatorAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue, int.MaxValue, int.MaxValue);
        var output = new double[2];
        OscillatorCore.ChandeMomentumOscillatorAverage(new[] { double.MaxValue, double.MaxValue }, output, int.MaxValue, int.MaxValue, int.MaxValue);
        Assert.Equal(new[] { 100d, 100d }, output);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 3, 9)]
    [InlineData(9, 3, 1)]
    [InlineData(5, 10, 20)]
    public void EveryDirectRouteMatchesIndependentRoundedRatios(int first, int second, int third)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var absolute in new[] { false, true })
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedChandeAverage(bars, first, second, third, absolute);
            var output = new double[bars.Count];
            var input = bars.Select(b => b.Close).ToArray();
            if (absolute) OscillatorCore.ChandeMomentumOscillatorAbsoluteAverage(input, output, first, second, third);
            else OscillatorCore.ChandeMomentumOscillatorAverage(input, output, first, second, third);
            Assert.Equal(expected, output);
            foreach (var value in output) Assert.InRange(value, absolute ? 0 : -100, 100);
            var data = Data(bars);
            using var context = new ComputeContext();
            using var fast = absolute ? IndicatorCompute.ComputeChandeMomentumOscillatorAbsoluteAverageFast(data, context, first, second, third)
                : IndicatorCompute.ComputeChandeMomentumOscillatorAverageFast(data, context, first, second, third);
            Assert.Equal(expected, fast.ToArray());
            if (absolute) data.CalculateChandeMomentumOscillatorAbsoluteAverage(first, second, third);
            else data.CalculateChandeMomentumOscillatorAverage(first, second, third);
            var key = absolute ? "Cmoaa" : "Cmoa";
            Assert.Equal(expected, data.OutputValues[key]);
            using var lifetime = absolute ? (IDisposable)new ChandeMomentumOscillatorAbsoluteAverageState(first, second, third)
                : new ChandeMomentumOscillatorAverageState(first, second, third);
            var state = (IStreamingIndicatorState)lifetime;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var bar = NativeBar(b);
                    Assert.Equal(expected[i], state.Update(bar, false, true).Outputs![key]);
                    Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                }
            }
        }
    }

    [Fact]
    public async Task V2FactoriesRetainFixedPeriodsAndSelectedInputs()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/alternating-scale")).Bars;
        var source = new Sma(2);
        foreach (var unusedLength in new[] { 1, 90 })
        foreach (var absolute in new[] { false, true })
        {
            IndicatorBase indicator = absolute ? new ChandeMomentumOscillatorAbsoluteAverage(unusedLength) : new ChandeMomentumOscillatorAverage(unusedLength);
            indicator.Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
            var selected = run[source].ToArray();
            var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedChandeAverage(projected, absolute: absolute);
            Assert.Equal(expected, run[indicator].ToArray());
            foreach (var chained in new[] { false, true })
            {
                var data = Data(bars);
                if (chained) data.CustomValuesList = selected.ToList();
                else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var fast = absolute ? IndicatorCompute.ComputeChandeMomentumOscillatorAbsoluteAverageFast(data, context)
                    : IndicatorCompute.ComputeChandeMomentumOscillatorAverageFast(data, context);
                Assert.Equal(expected, fast.ToArray());
            }
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var i = 0; i < projected.Length; i++)
                    Assert.Equal(expected[i], state.Update(NativeBar(projected[i]), true, true).Value);
            }
        }
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar NativeBar(Bar b) => new("BANK", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
}
