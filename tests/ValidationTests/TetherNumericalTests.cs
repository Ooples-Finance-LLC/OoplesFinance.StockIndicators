using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TetherNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TFSTetherLine) || c.IndicatorType == typeof(TFSTetherLineIndicator))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task EveryRoutePublishesTheExactWindowMidpoint(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(48, 245))
        {
            var bars = fixture.Bars.ToArray();
            var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = Midpoints(high, low, length);
            var actual = new double[bars.Length];
            OscillatorCore.TFSTetherLineIndicator(high, low, actual, length);
            Assert.Equal(expected, actual);
            OscillatorCore.TFSTetherLineIndicator(close, high, low, actual, length);
            Assert.Equal(expected, actual);
            var data = new StockData(bars.Select(b => b.Open), high, low, close, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateTFSTetherLineIndicator(length);
            Assert.Equal(expected, data.OutputValues["Tether"]);
            foreach (IIndicator indicator in new IIndicator[] { new TFSTetherLine(length), new TFSTetherLineIndicator(length) })
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
                var builtIn = (IBuiltInIndicator)indicator;
                var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
                foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
                {
                    Assert.NotNull(state);
                    using var lifetime = state as IDisposable;
                    for (var replay = 0; replay < 2; replay++)
                    {
                        state.Reset();
                        for (var i = 0; i < bars.Length; i++)
                        foreach (var commit in new[] { false, true })
                        {
                            var b = bars[i];
                            var native = new OhlcvBar("TETHER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Tether"]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task BothAliasesRetainBarExtremesWhenOnlyCloseIsReplaced()
    {
        var bars = new[] { 100d, 120, 110, 150, 90 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var source = new Sma(2);
        IIndicator[] indicators = { new TFSTetherLine(3).Of(source), new TFSTetherLineIndicator(3).Of(source) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators.Prepend(source).ToArray()).BuildAsync();
        var expected = Midpoints(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), 3);
        foreach (var indicator in indicators) Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
    }

    [Fact]
    public void LegacyChainedAliasesUseTheSyntheticRange()
    {
        var prices = new[] { 100d, 120, 110, 150, 90 };
        var dates = Enumerable.Range(0, prices.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToArray();
        var selected = prices.Select(v => 2 * v).ToArray();
        var highs = selected.Select((v, i) => Math.Max(v, selected[Math.Max(0, i - 1)])).ToArray();
        var lows = selected.Select((v, i) => Math.Min(v, selected[Math.Max(0, i - 1)])).ToArray();
        var expected = Midpoints(highs, lows, 3);
        StockData Data() => new StockData(prices, prices.Select(v => v + 1), prices.Select(v => v - 1), prices,
            Enumerable.Repeat(1d, prices.Length), dates).UseInput(InputSeries.Of(bar => 2 * bar.Close));
        var legacy = Data().CalculateTFSTetherLineIndicator(3);
        Assert.Equal(expected, legacy.OutputValues["Tether"]);
        using var context = new ComputeContext();
        using var alias = IndicatorCompute.ComputeTFSTetherLineFast(Data(), context, 3);
        using var full = IndicatorCompute.ComputeTFSTetherLineIndicatorFast(Data(), context, 3);
        Assert.Equal(expected, alias.Span.ToArray());
        Assert.Equal(expected, full.Span.ToArray());
    }

    private static double[] Midpoints(double[] high, double[] low, int length) => high.Select((_, i) =>
        ((ReferenceFraction.FromDouble(high.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Max())
          + ReferenceFraction.FromDouble(low.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Min()))
         / new ReferenceFraction(2)).ToDouble()).ToArray();
}
