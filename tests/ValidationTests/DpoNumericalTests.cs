using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DpoNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Dpo) || c.IndicatorType == typeof(DetrendedPriceOscillator))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(38, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("dpo-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesTheIndependentReference(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var maType = options is DetrendedPriceOscillatorSpecOptions dpo ? dpo.MaType : MovingAvgType.SimpleMovingAverage;
        var reference = Assert.Single(BuiltInFormulaReferences.For((IIndicator)builtIn)).OverflowReference!;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = reference(bars);
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            using (var context = new ComputeContext())
            {
                using var result = IndicatorCompute.ComputeDetrendedPriceOscillatorFast(Data(), context, length, maType);
                Assert.Equal(expected, result.ToArray());
            }
            var data = Data().CalculateDetrendedPriceOscillator(maType, length);
            Assert.Equal(expected, data.OutputValues["Dpo"]);
            if (maType == MovingAvgType.SimpleMovingAverage)
            {
                var actual = new double[bars.Count];
                OscillatorCore.DetrendedPriceOscillator(bars.Select(b => b.Close).ToArray(), actual, length);
                Assert.Equal(expected, actual);
            }
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
                        var native = new OhlcvBar("DPO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Dpo"]);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(1061)]
    public void LagAndStartupAreExplicit(int length)
    {
        var values = Enumerable.Range(1, 1065).Select(i => (double)i).ToArray();
        var actual = new double[values.Length];
        OscillatorCore.DetrendedPriceOscillator(values, actual, length);
        var lag = Math.Clamp((int)Math.Ceiling(length / 2d + 1), 2, 530);
        for (var i = 0; i < values.Length; i++)
        {
            var average = i + 1 < length ? 0 : (values[i] + values[i - length + 1]) / 2;
            Assert.Equal((i >= lag ? values[i - lag] : 0) - average, actual[i]);
        }
    }

    [Fact]
    public async Task FiniteCancellationSurvivesAndTrueOverflowIsRejected()
    {
        Bar[] BarsFor(double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var constant = BarsFor(Enumerable.Repeat(double.MaxValue, 6).ToArray());
        var indicator = new Dpo(3);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(constant)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 0d, -double.MaxValue, 0d, 0d, 0d }, run[indicator].ToArray());
        var overflow = BarsFor(new[] { double.MaxValue, 0d, -double.MaxValue });
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(overflow))
            .ConfigureIndicators(new Dpo(1)).BuildAsync());
    }

    [Fact]
    public async Task BothAliasesAndLegacyBuilderHonorSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator[] indicators = { new Dpo(3).Of(source), new DetrendedPriceOscillator(3).Of(source) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators.Prepend(source).ToArray()).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDpo(projected, 3, 1);
        foreach (var indicator in indicators) Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeDetrendedPriceOscillatorFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
