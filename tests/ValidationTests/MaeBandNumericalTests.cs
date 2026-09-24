using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MaeBandNumericalTests
{
    [Fact]
    public void ExactCumulativeErrorsPreserveHistoryPreviewsAndFiniteOppositeBand()
    {
        var errors = new ExactCumulativeErrorBands(1);
        Assert.Equal((6d, -6d), errors.Next(6, 0, true));
        Assert.Equal((3d, -3d), errors.Next(0, 0, false));
        Assert.Equal((3d, -3d), errors.Next(0, 0, false));
        Assert.Equal((3d, -3d), errors.Next(0, 0, true));
        Assert.Equal((2d, -2d), errors.Next(0, 0, true));
        for (var count = 4; count <= 100; count++)
        {
            var expected = (new ReferenceFraction(6) / new ReferenceFraction(count)).ToDouble();
            Assert.Equal((expected, -expected), errors.Next(0, 0, true));
        }
        errors.Reset();
        var extremes = errors.Next(-double.MaxValue, double.MaxValue, false);
        Assert.Equal(double.PositiveInfinity, extremes.Upper);
        Assert.Equal(-double.MaxValue, extremes.Lower);
        Assert.Equal(0, errors.Next(0, double.MaxValue, true).Lower);
        var zeroMultiplier = new ExactCumulativeErrorBands(0);
        Assert.Equal((double.MaxValue, double.MaxValue), zeroMultiplier.Next(-double.MaxValue, double.MaxValue, true));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(double.MaxValue)]
    public void MultiplierAndCumulativeErrorMatchIndependentBands(double multiplier)
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, double.MaxValue, 1, 2, 3 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMaeBands(bars, new MeanAbsoluteErrorBandsSpecOptions(2, multiplier));
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        var legacy = data.CalculateMeanAbsoluteErrorBands(multiplier, length: 2);
        using var state = new MeanAbsoluteErrorBandsState(multiplier, length: 2);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("MAE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            var actual = state.Update(native, true, true).Outputs!;
            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key][i], legacy.OutputValues[key][i]);
                Assert.Equal(expected[key][i], actual[key]);
            }
        }
    }

    [Fact]
    public async Task SelectedInputControlsCenterAndCumulativeError()
    {
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new MeanAbsoluteErrorBands(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMaeBands(projected, new MeanAbsoluteErrorBandsSpecOptions(3));
        var keys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
        for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MeanAbsoluteErrorBands)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentMaeBandFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedMaeBands(fixture.Bars, options);
            var count = fixture.Bars.Count;
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key].Take(count), BuilderArmBinding.Compute(Data(), outputSpec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.TryComputeFast(Data(), outputSpec, context);
                Assert.NotNull(buffer);
                Assert.Equal(expected[key].Take(count), buffer.Value.ToArray());
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("GUPPY", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var outputs = state.Update(native, commit, true).Outputs!;
                            foreach (var key in expected.Keys) Assert.Equal(expected[key][i], outputs[key]);
                        }
                    }
                }
            }
        }
    }

}
