using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BollingerNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => new[] { typeof(BollingerBands), typeof(BollingerBandsMiddle), typeof(BollingerBandsPercentB), typeof(BollingerBandsWidth) }.Contains(c.IndicatorType))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentFit(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        var kind = options is BollingerBandsSpecOptions bb ? bb.MaType : MovingAvgType.SimpleMovingAverage;
        var multiplier = options is BollingerBandsSpecOptions bands ? bands.StdDevMult : options is BollingerBandsPercentBSpecOptions percentOptions ? percentOptions.Multiplier : 2;
        var keys = builtIn.BatchName == IndicatorName.BollingerBandsPercentB ? new[] { "PctB" }
            : builtIn.BatchName == IndicatorName.BollingerBandsWidth ? new[] { "BbWidth" } : new[] { "UpperBand", "MiddleBand", "LowerBand" };
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedBollinger(bars, length, testCase.ToString().Contains("/bollinger-composition/") ? int.Parse(testCase.ToString().Split('/').Last()) : kind == MovingAvgType.WeightedMovingAverage ? 2 : 1, multiplier);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
            VolatilityCore.BollingerBands(prices, upper, middle, lower, length, multiplier, kind);
            Assert.Equal(expected["UpperBand"], upper); Assert.Equal(expected["MiddleBand"], middle); Assert.Equal(expected["LowerBand"], lower);
            var legacy = builtIn.BatchName == IndicatorName.BollingerBandsPercentB ? Data().CalculateBollingerBandsPercentB(multiplier, kind, length)
                : builtIn.BatchName == IndicatorName.BollingerBandsWidth ? Data().CalculateBollingerBandsWidth(multiplier, kind, length)
                : Data().CalculateBollingerBands(kind, length, multiplier);
            foreach (var key in keys) Assert.Equal(expected[key], legacy.OutputValues[key]);
            using (var context = new ComputeContext())
            {
                using var top = IndicatorCompute.ComputeBollingerUpperFast(Data(), context, length, multiplier, kind);
                using var bottom = IndicatorCompute.ComputeBollingerLowerFast(Data(), context, length, multiplier, kind);
                using var width = IndicatorCompute.ComputeBollingerBandsWidthFast(Data(), context, length, multiplier, kind);
                Assert.Equal(expected["UpperBand"], top.ToArray()); Assert.Equal(expected["LowerBand"], bottom.ToArray()); Assert.Equal(expected["BbWidth"], width.ToArray());
                if (kind == MovingAvgType.SimpleMovingAverage)
                {
                    using var percent = IndicatorCompute.ComputeBollingerBandsPercentBFast(Data(), context, length, multiplier);
                    Assert.Equal(expected["PctB"], percent.ToArray());
                    VolatilityCore.BollingerBandsWidth(prices, middle, length, multiplier);
                    Assert.Equal(expected["BbWidth"], middle);
                }
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
                        var native = new OhlcvBar("FIT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var outputs = state.Update(native, commit, true).Outputs!;
                            foreach (var key in keys) Assert.Equal(expected[key][i], outputs[key]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputReachesBandsAndRatios()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        foreach (IIndicator indicator in new IIndicator[] { new BollingerBands(3).Of(source), new BollingerBandsMiddle(3).Of(source),
            new BollingerBandsWidth(3).Of(source), new BollingerBandsPercentB(3).Of(source) })
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
            var selected = run[source].ToArray();
            var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedBollinger(projected, 3, 1, 2);
            var builtIn = (IBuiltInIndicator)indicator;
            var keys = indicator.Outputs.Count == 1 ? new[] { (builtIn.BatchOutputKey ?? (builtIn.BatchName == IndicatorName.BollingerBandsWidth ? "BbWidth" : builtIn.BatchName == IndicatorName.BollingerBandsPercentB ? "PctB" : "MiddleBand")) } : new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
            foreach (var chained in new[] { false, true })
            {
                var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                    bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
                if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var width = IndicatorCompute.ComputeBollingerBandsWidthFast(data, context, 3);
                using var percent = IndicatorCompute.ComputeBollingerBandsPercentBFast(data, context, 3);
                Assert.Equal(expected["BbWidth"], width.ToArray()); Assert.Equal(expected["PctB"], percent.ToArray());
            }
        }
    }

    [Fact]
    public void RatiosPreserveSmallSpreadsAndAvoidOverflowingIntermediateBands()
    {
        Assert.Equal(50, BollingerArithmetic.Percent(1e16, 1e16, 0.25, 2));
        Assert.Equal(1e-16, BollingerArithmetic.Width(1e16, 0.25, 2));
        Assert.Equal(4, BollingerArithmetic.Width(double.MaxValue, double.MaxValue, 2));
        Assert.Equal(50, BollingerArithmetic.Percent(double.MaxValue, double.MaxValue, double.MaxValue, 2));
        Assert.Equal(double.MaxValue, BollingerArithmetic.Band(-double.MaxValue, double.MaxValue, 2));
    }
}
