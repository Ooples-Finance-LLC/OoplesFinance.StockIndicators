using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VidyaNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.VariableIndexDynamicAverage)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ConvexBlendPreservesExtremesSubnormalsAndExactCancellation()
    {
        Assert.Equal(double.MaxValue, VidyaBlend.Compute(double.MaxValue, double.MaxValue, 0.1));
        Assert.Equal(double.Epsilon, VidyaBlend.Compute(double.Epsilon, double.Epsilon, 0.5));
        Assert.Equal(0, VidyaBlend.Compute(-double.MaxValue, double.MaxValue, 0.5));
        var output = new double[4];
        MovingAverageCore.Vidya(new[] { 1d, 2d, 4d, 3d }, output, 1);
        Assert.Equal(new[] { 1d, 2d, 4d, 3d }, output);
        MovingAverageCore.Vidya(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        var shortHistory = new double[2];
        MovingAverageCore.Vidya(new[] { double.MaxValue, double.MaxValue }, shortHistory, int.MaxValue);
        Assert.Equal(new[] { double.MaxValue, double.MaxValue }, shortHistory);
    }

    [Fact]
    public void RejectedInputDoesNotChangeNativeMomentumOrPriceState()
    {
        using var engine = new VariableIndexDynamicAverageEngine(3);
        for (var replay = 0; replay < 2; replay++)
        {
            engine.Reset();
            Assert.Equal(double.MaxValue, engine.Next(double.MaxValue, true));
            engine.Next(-double.MaxValue, false);
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var commit in new[] { false, true })
                Assert.Throws<ArgumentOutOfRangeException>(() => engine.Next(value, commit));
            Assert.Equal(double.MaxValue, engine.Next(double.MaxValue, false));
            Assert.Equal(double.MaxValue, engine.Next(double.MaxValue, true));
            Assert.Equal(0, engine.Next(-double.MaxValue, true));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AliasesFactoriesAndSelectedInputsMatchIndependentTrajectory(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars;
            var source = new Sma(2);
            var compact = new Vidya(length);
            var expanded = new VariableIndexDynamicAverage(length);
            compact.Of(source);
            expanded.Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(source, compact, expanded).BuildAsync();
            var selected = run[source].ToArray();
            var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedVidya(projected, length);
            Assert.Equal(expected, run[compact].ToArray());
            Assert.Equal(expected, run[expanded].ToArray());
            foreach (var chained in new[] { false, true })
            {
                var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                    bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
                if (chained) data.CustomValuesList = selected.ToList();
                else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var first = IndicatorCompute.ComputeVidyaFast(data, context, length);
                using var second = IndicatorCompute.ComputeVariableIndexDynamicAverageFast(data, context, length);
                Assert.Equal(expected, first.ToArray());
                Assert.Equal(expected, second.ToArray());
            }
            foreach (var options in new IIndicatorSpecOptions[] { new VidyaSpecOptions(length), new VariableIndexDynamicAverageSpecOptions(length) })
            {
                var spec = new IndicatorSpec(IndicatorName.VariableIndexDynamicAverage, options);
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
                            var bar = new OhlcvBar("VIDYA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(bar, false, true).Outputs!["Vidya"]);
                            Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                        }
                    }
                }
            }
        }
    }
}
