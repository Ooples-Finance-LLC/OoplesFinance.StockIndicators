using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AbsoluteChandeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.ChandeMomentumOscillatorAbsolute)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void StartupGateAndFirstEvictionPreserveTheDisplacementContract()
    {
        var output = new double[5];
        OscillatorCore.ChandeMomentumOscillatorAbsolute(new[] { 10d, 11, 12, 11, 10 }, output, 2);
        Assert.Equal(new[] { 0d, 0, 100, 0, 100 }, output);
        OscillatorCore.ChandeMomentumOscillatorAbsolute(new[] { -double.MaxValue, double.MaxValue, 0, double.MaxValue, double.MaxValue }, output, 1);
        Assert.Equal(new[] { 0d, 100, 100, 100, 0 }, output);
        OscillatorCore.ChandeMomentumOscillatorAbsolute(new[] { -double.Epsilon, double.Epsilon, 0, double.Epsilon, 0 }, output, 2);
        Assert.Equal(new[] { 0d, 0, 100d / 3, 0, 0 }, output);
        OscillatorCore.ChandeMomentumOscillatorAbsolute(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        var shortHistory = new double[2];
        OscillatorCore.ChandeMomentumOscillatorAbsolute(new[] { -double.MaxValue, double.MaxValue }, shortHistory, int.MaxValue);
        Assert.Equal(new[] { 0d, 0 }, shortHistory);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    public async Task AllRoutesMatchDisplacementReferenceAndRespectSelectedInput(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars;
            var source = new Sma(2);
            var indicator = new ChandeMomentumOscillatorAbsolute(length);
            indicator.Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(source, indicator).BuildAsync();
            var selected = run[source].ToArray();
            var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedAbsoluteChande(projected, length);
            Assert.Equal(expected, run[indicator].ToArray());
            var actual = new double[projected.Length];
            OscillatorCore.ChandeMomentumOscillatorAbsolute(selected, actual, length);
            Assert.Equal(expected, actual);
            foreach (var chained in new[] { false, true })
            {
                var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                    bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
                if (chained) data.CustomValuesList = selected.ToList();
                else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var fast = IndicatorCompute.ComputeChandeMomentumOscillatorAbsoluteFast(data, context, length);
                Assert.Equal(expected, fast.ToArray());
                data.CalculateChandeMomentumOscillatorAbsolute(length);
                Assert.Equal(expected, data.OutputValues["Cmoa"]);
            }
            var spec = new IndicatorSpec(IndicatorName.ChandeMomentumOscillatorAbsolute, new ChandeMomentumOscillatorAbsoluteSpecOptions(length));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < projected.Length; i++)
                    {
                        var b = projected[i];
                        var bar = new OhlcvBar("ABS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[i], state.Update(bar, false, true).Outputs!["Cmoa"]);
                        Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                        Assert.InRange(expected[i], 0, 100);
                    }
                }
            }
        }
    }
}
