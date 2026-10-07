using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AlmaNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Alma)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void GaussianSymmetryPreservesCancellationAndFullWindowConstants()
    {
        using var mean = new AlmaWindowMean(3, .5, 3);
        mean.Next(double.MaxValue, true);
        mean.Next(0, true);
        Assert.Equal(0, mean.Next(-double.MaxValue, true));
        mean.Reset();
        for (var i = 0; i < 3; i++) mean.Next(double.MaxValue, true);
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, false));
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, true));
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            foreach (var commit in new[] { false, true })
                Assert.Throws<ArgumentOutOfRangeException>(() => mean.Next(invalid, commit));
            Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, false));
        }
        using var uniform = new AlmaWindowMean(3, .85, 0);
        Assert.Equal(1, uniform.Next(3, true));
        Assert.Equal(2, uniform.Next(3, true));
        Assert.Equal(3, uniform.Next(3, true));
        MovingAverageCore.ArnaudLegouxMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
    }

    [Theory]
    [InlineData(1, .85, 6)]
    [InlineData(3, .85, 6)]
    [InlineData(14, .85, 6)]
    [InlineData(3, .5, 3)]
    [InlineData(9, 0, 6)]
    [InlineData(9, 1, 6)]
    [InlineData(3, .85, 0)]
    [InlineData(3, .85, -3)]
    public async Task AllRoutesMatchIndependentRationalConvolution(int period, double offset, int sigma)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.ToArray();
            var expected = BuiltInFormulaReferences.RoundedAlmaMean(bars, period, offset, sigma);
            var actual = new double[bars.Length];
            MovingAverageCore.ArnaudLegouxMovingAverage(bars.Select(b => b.Close).ToArray(), actual, period, offset, sigma);
            Assert.Equal(expected, actual);
            var data = Data(bars);
            data.CalculateArnaudLegouxMovingAverage(period, offset, sigma);
            Assert.Equal(expected, data.OutputValues["Alma"]);
            using var native = new ArnaudLegouxMovingAverageState(period, offset, sigma);
            CheckState(native, bars, expected);
            if (offset != .85 || sigma != 6) continue;
            var indicator = new Alma(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var spec = new IndicatorSpec(IndicatorName.ArnaudLegouxMovingAverage, new AlmaSpecOptions(period));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                CheckState(state, bars, expected);
            }
        }
    }

    [Fact]
    public async Task SelectedAndChainedInputsReachEveryBuilderRoute()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).First(f => f.Bars.Count == 40).Bars.ToArray();
        var source = new Sma(2);
        var indicator = new Alma(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedAlmaMean(projected, 3);
        Assert.Equal(expected, run[indicator].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = Data(bars);
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeAlmaFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }

    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));

    private static void CheckState(IStreamingIndicatorState state, Bar[] bars, double[] expected)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var native = new OhlcvBar("ALMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                foreach (var commit in new[] { false, true })
                    Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Alma"]);
            }
        }
    }
}
