using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class HammingNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(HammingMa) || c.IndicatorType == typeof(EhlersHammingMovingAverage))
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
    public void ConstantWindowIsFiniteAndInvalidPreviewsCannotChangeState()
    {
        using var mean = new HammingWindowMean(3, 3);
        for (var i = 0; i < 3; i++) mean.Next(double.MaxValue, true);
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, false));
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HammingWindowMean(3, invalid));
            foreach (var commit in new[] { false, true })
                Assert.Throws<ArgumentOutOfRangeException>(() => mean.Next(invalid, commit));
            Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, false));
        }
        mean.Reset();
        Assert.Equal(0, mean.Next(0, true));
        MovingAverageCore.EhlersHammingMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 3)]
    [InlineData(14, 3)]
    [InlineData(3, 0)]
    [InlineData(7, .5)]
    [InlineData(3, 4)]
    [InlineData(3, double.MaxValue)]
    public async Task AllRoutesMatchIndependentRationalConvolution(int length, double pedestal)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.ToArray();
            var expected = BuiltInFormulaReferences.RoundedHammingMean(bars, length, pedestal);
            var actual = new double[bars.Length];
            MovingAverageCore.EhlersHammingMovingAverage(bars.Select(b => b.Close).ToArray(), actual, length, pedestal);
            Assert.Equal(expected, actual);
            var data = Data(bars);
            data.CalculateEhlersHammingMovingAverage(length, pedestal);
            Assert.Equal(expected, data.OutputValues["Ehma"]);
            using var context = new ComputeContext();
            using var direct = IndicatorCompute.ComputeEhlersHammingMovingAverageFast(Data(bars), context, length, pedestal);
            Assert.Equal(expected, direct.ToArray());
            using var state = new EhlersHammingMovingAverageState(length, pedestal);
            CheckState(state, bars, expected);
            using var smoother = new EhlersHammingMovingAverageSmoother(length, pedestal);
            for (var replay = 0; replay < 2; replay++)
            {
                smoother.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    Assert.Equal(expected[i], smoother.Next(bars[i].Close, false));
                    Assert.Equal(expected[i], smoother.Next(bars[i].Close, true));
                }
            }
            if (pedestal != 3) continue;
            IIndicator[] indicators = { new HammingMa(length), new EhlersHammingMovingAverage(length) };
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
            foreach (var indicator in indicators)
            {
                Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
                var builtIn = (IBuiltInIndicator)indicator;
                var spec = new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
                foreach (var factoryState in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
                {
                    Assert.NotNull(factoryState);
                    using var lifetime = factoryState as IDisposable;
                    CheckState(factoryState, bars, expected);
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputReachesTheWindow()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars.ToArray();
        var source = new Sma(2);
        var indicator = new HammingMa(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedHammingMean(projected, 3);
        Assert.Equal(expected, run[indicator].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = Data(bars);
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var output = IndicatorCompute.ComputeEhlersHammingMovingAverageFast(data, context, 3);
            Assert.Equal(expected, output.ToArray());
        }
    }

    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));

    private static void CheckState(IStreamingIndicatorState state, Bar[] bars, double[] expected)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var native = new OhlcvBar("HAMMING", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                foreach (var commit in new[] { false, true })
                    Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Ehma"]);
            }
        }
    }
}
