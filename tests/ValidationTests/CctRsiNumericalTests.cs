using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CctRsiNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));

    [Fact]
    public void DifferentNumeratorAndDenominatorWindowsMustNotClampTheRatio()
    {
        Assert.Equal(400, OoplesFinance.StockIndicators.Helpers.CctRsiRatio.Percent(80, 0, 70, 90));
        Assert.Equal(0, OoplesFinance.StockIndicators.Helpers.CctRsiRatio.Percent(80, 0, 70, 70));
        var prices = new[] { 5d, 4, 3, 2, 1, 2, 3, 4, 5 };
        var expected = BuiltInFormulaReferences.CctRsiOutputs(BarsOf(prices), new CCTStochRSI(2, 7, 2, 4, 3, 2, 3, MovingAvgType.ExponentialMovingAverage))["Type1"];
        Assert.Contains(expected, v => v > 100);
        var actual = new double[prices.Length]; OscillatorCore.CCTStochRsi(prices, actual, 3, 7, 2); Assert.Equal(expected, actual);
        OscillatorCore.CCTStochRsi(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.CCTStochRsi(prices, Array.Empty<double>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task OneBarWindowsPublishZeroForAllEightOutputsAtEveryScale(int exponent)
    {
        var v = Math.ScaleB(1d, exponent); var prices = new[] { -v, v, -v, 0d, v, v };
        var indicator = new CCTStochRSI(1, 1, 1, 1, 1, 1, 1, MovingAvgType.ExponentialMovingAverage);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(prices))).ConfigureIndicators(indicator).BuildAsync();
        foreach (var output in indicator.Outputs) Assert.All(result[output].ToArray(), value => Assert.Equal(0d, value));
    }

    [Fact]
    public void RawAllOutputsUseSelectedPricesAndNativeSignalRetainsItsOwnPeriod()
    {
        var prices = new[] { 5d, 4, 3, 2, 1, 2, 3, 4, 5 }; var bars = BarsOf(prices);
        var indicator = new CCTStochRSI(2, 7, 2, 4, 3, 2, 3, MovingAvgType.ExponentialMovingAverage);
        var expected = BuiltInFormulaReferences.CctRsiOutputs(bars, indicator);
        var batch = Data(prices).CalculateCCTStochRSI(MovingAvgType.ExponentialMovingAverage, 2, 7, 2, 4, 3, 2, 3);
        foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
        var data = Data(Enumerable.Repeat(9d, prices.Length).ToArray()); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        foreach (var pair in expected)
        {
            using var raw = IndicatorCompute.ComputeCCTStochRelativeStrengthIndexFast(data, context, 7, 2, 3,
                MovingAvgType.ExponentialMovingAverage, pair.Key, 2, 4, 2, 3);
            Assert.Equal(pair.Value, raw.Span.ToArray());
        }
        var customSignal = BuiltInFormulaReferences.CctRsiOutputs(bars, indicator, signalLength: 4)["Signal"];
        using var state = new CCTStochRelativeStrengthIndexState(MovingAvgType.ExponentialMovingAverage, 2, 7, 2, 4, 3, 2, 3, 4);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Assert.Equal(customSignal[i], state.Update(Native(bars[i]), false, true).Outputs!["Signal"]);
                Assert.Equal(customSignal[i], state.Update(Native(bars[i]), true, true).Outputs!["Signal"]);
            }
        }
    }

    [Fact]
    public void NativeCctRsiRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new CCTStochRelativeStrengthIndexState(length1: 2, length2: 3, length3: 4, length4: 5, length5: 6);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = Create(); var control = Create();
            using var stateLifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var v in new[] { 3d, 1, 4 })
            {
                var seed = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, v, 1));
                state.Update(seed, true, true); control.Update(seed, true, true);
            }
            var fields = new[] { 2d, 4, 1, 2, 1 }; fields[field] = invalid;
            var bad = new OhlcvBar("GAINLOSS", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 2, 1));
            var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    private static OhlcvBar Native(Bar b) => new("FOLDED", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "CCTStochRSI", "CCTStochRelativeStrengthIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.CctRsiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);

    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);

    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
