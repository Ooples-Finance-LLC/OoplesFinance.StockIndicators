using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ElderRayNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ElderRayIndex) || c.IndicatorType == typeof(ElderRayBullPower) || c.IndicatorType == typeof(ElderRayBearPower))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(38, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("elder-ray-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void BothNativeOutputsAndLegacyMatchIndependentReferences(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var maType = options is ElderRayIndexSpecOptions elder ? elder.MaType : MovingAvgType.ExponentialMovingAverage;
        var kind = testCase.Name.StartsWith("elder-ray-composition/") ? int.Parse(testCase.Name.Split('/')[2])
            : testCase.Name == "weighted-average" ? 2 : 3;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var bull = BuiltInFormulaReferences.RoundedElderRay(bars, length, kind, true);
            var bear = BuiltInFormulaReferences.RoundedElderRay(bars, length, kind, false);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateElderRayIndex(maType, length);
            Assert.Equal(bull, data.OutputValues["BullPower"]);
            Assert.Equal(bear, data.OutputValues["BearPower"]);
            using (var context = new ComputeContext())
            {
                using var bullBuffer = IndicatorCompute.ComputeElderRayBullPowerFast(data, context, length, maType);
                using var bearBuffer = IndicatorCompute.ComputeElderRayBearPowerFast(data, context, length, maType);
                Assert.Equal(bull, bullBuffer.ToArray());
                Assert.Equal(bear, bearBuffer.ToArray());
            }
            if (kind == 3)
            {
                var close = bars.Select(b => b.Close).ToArray();
                var high = bars.Select(b => b.High).ToArray();
                var low = bars.Select(b => b.Low).ToArray();
                var actual = new double[bars.Count];
                VolumeCore.ElderRayBullPower(high, close, actual, length); Assert.Equal(bull, actual);
                VolumeCore.ElderRayBearPower(low, close, actual, length); Assert.Equal(bear, actual);
                OscillatorCore.ElderRayBullPower(high, close, actual, length); Assert.Equal(bull, actual);
                OscillatorCore.ElderRayBearPower(low, close, actual, length); Assert.Equal(bear, actual);
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
                        var native = new OhlcvBar("ELDER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var actual = state.Update(native, commit, true).Outputs!;
                            Assert.Equal(bull[i], actual["BullPower"]);
                            Assert.Equal(bear[i], actual["BearPower"]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task OnlyPublishedOverflowIsRejected()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, -double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1) };
        var bear = new ElderRayBearPower(1);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(bear).BuildAsync();
        Assert.Equal(0, Assert.Single(run[bear].ToArray()));
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new ElderRayBullPower(1)).BuildAsync());
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new ElderRayIndex(1)).BuildAsync());
    }

    [Fact]
    public async Task TypedSelectionPreservesHighLowAndFeedsTheAverage()
    {
        var bars = new[] { 100d, 120, 110, 150, 90 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var source = new Sma(2);
        IIndicator[] indicators = { new ElderRayIndex(3).Of(source), new ElderRayBullPower(3).Of(source), new ElderRayBearPower(3).Of(source) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators.Prepend(source).ToArray()).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        foreach (var indicator in indicators)
            Assert.Equal(BuiltInFormulaReferences.RoundedElderRay(projected, 3, 3, indicator is not ElderRayBearPower), run[indicator.Outputs[0]].ToArray());
    }

    [Fact]
    public void LegacySelectedInputUsesSyntheticRangeForBothPowers()
    {
        var prices = new[] { 100d, 120, 110, 150, 90 };
        var dates = Enumerable.Range(0, prices.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToArray();
        var selected = prices.Select(v => 2 * v).ToArray();
        var projected = selected.Select((v, i) => new Bar(dates[i], v, Math.Max(v, selected[Math.Max(0, i - 1)]),
            Math.Min(v, selected[Math.Max(0, i - 1)]), v, 1)).ToArray();
        foreach (var chained in new[] { false, true })
        {
            StockData Data()
            {
                var data = new StockData(prices, prices.Select(v => v + 1), prices.Select(v => v - 1), prices,
                    Enumerable.Repeat(1d, prices.Length), dates);
                if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
                return data;
            }
            var legacy = Data().CalculateElderRayIndex(length: 3);
            using var context = new ComputeContext();
            using var bull = IndicatorCompute.ComputeElderRayBullPowerFast(Data(), context, 3);
            using var bear = IndicatorCompute.ComputeElderRayBearPowerFast(Data(), context, 3);
            Assert.Equal(BuiltInFormulaReferences.RoundedElderRay(projected, 3, 3, true), bull.ToArray());
            Assert.Equal(BuiltInFormulaReferences.RoundedElderRay(projected, 3, 3, false), bear.ToArray());
            Assert.Equal(legacy.OutputValues["BullPower"], bull.ToArray());
            Assert.Equal(legacy.OutputValues["BearPower"], bear.ToArray());
        }
    }
}
