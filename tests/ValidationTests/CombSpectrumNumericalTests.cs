using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CombSpectrumNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersCombFilterSpectralEstimate)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassEveryNumericalClass(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void ReferenceRejectsOutputFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    private static Bar B(double p) => new(DateTime.UnixEpoch, p, p, p, p, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("COMB", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(7, 2, .3)]
    [InlineData(4, 2, 1.1)]
    [InlineData(2, 7, .3)]
    [InlineData(int.MaxValue, int.MaxValue, .3)]
    [InlineData(4, 2, double.MaxValue)]
    public void DelayedPowerMatchesDirectHistoryAcrossRoutes(int upper, int lower, double bandwidth)
    {
        var bars = new[] { 2d, -4, 9, 3, -7, 0, 19, -3, 0, 5, 2, 7 }.Select(B).ToArray();
        var expected = BuiltInFormulaReferences.CombSpectrumValues(bars, upper, lower, bandwidth);
        var data = Data(bars); data.CalculateEhlersCombFilterSpectralEstimate(upper, lower, bandwidth);
        Assert.Equal(expected.Values, data.OutputValues["Ecfse"]); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var native in new[] { true, false })
        {
            var spec = new IndicatorSpec(IndicatorName.EhlersCombFilterSpectralEstimate, new EhlersCombFilterSpectralEstimateSpecOptions(upper, lower, bandwidth));
            var state = native ? StatefulIndicatorFactory.Create(spec)! : StreamingIndicatorFactory.CreateState(spec)!;
            Assert.NotNull(state); using var lifetime = state as IDisposable;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(B(100)), false, false);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected.Values[i], state.Update(Native(bars[i]), final, true).Outputs!["Ecfse"]);
                }
            }
        }
    }
    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    [InlineData(1e-160)]
    public void PowersRemainDefinedBeyondBothBinary64ExponentLimits(double scale)
    {
        var bars = new[] { scale, -scale, scale, -scale, 0, scale, 0, -scale }.Select(B).ToArray();
        var expected = BuiltInFormulaReferences.CombSpectrumValues(bars, 5, 1, .3);
        var data = Data(bars).CalculateEhlersCombFilterSpectralEstimate(5, 1);
        Assert.Equal(expected.Values, data.OutputValues["Ecfse"]); Assert.Equal(expected.Signals, data.SignalsList);
        Assert.All(expected.Values, v => Assert.True(v == 0 || v >= 1 && v <= 5));
        if (scale != double.Epsilon) Assert.Contains(expected.Values, v => v > 0);
    }
    [Fact]
    public void ExactHalfPowerIsIncludedAndNormalizationDoesNotUnderflow()
    {
        var tiny = F.Of(double.Epsilon); var huge = F.Of(double.MaxValue); huge *= huge;
        foreach (var scale in new[] { tiny * tiny, (F)1, huge })
        {
            Assert.Equal(7d / 3, CombSpectrumWindow.Summarize(new[] { (2, 2 * scale), (3, scale) }));
            Assert.Equal(2, CombSpectrumWindow.Summarize(new[] { (2, 2 * scale), (3, (1 - tiny) * scale) }));
        }
    }
    [Fact]
    public void SinglePeriodAndEmptySpectrumHaveIndependentHands()
    {
        var prices = new[] { 2d, -4, 9, 2, -3, 7 };
        var single = new CombSpectrumWindow(3, 3, 0);
        Assert.Equal(new[] { 0d, 3, 3, 3, 3, 3 }, prices.Select(p => single.Next(p, true).Value));
        var empty = new CombSpectrumWindow(2, 3, .3); Assert.All(prices.Select(p => empty.Next(p, true).Value), v => Assert.Equal(0, v));
        var flat = new CombSpectrumWindow(7, 1, .3); Assert.All(Enumerable.Range(0, 15).Select(_ => flat.Next(0, true).Value), v => Assert.Equal(0, v));
    }
    [Fact]
    public void HugeNarrowRangeAllocatesForObservedHistory()
    {
        var warmup = new CombSpectrumWindow(1, 1, .3); warmup.Next(1, true);
        var before = GC.GetAllocatedBytesForCurrentThread(); var window = new CombSpectrumWindow(int.MaxValue, int.MaxValue, .3);
        Assert.Equal(0, window.Next(2, true).Value);
        Assert.Equal((double)int.MaxValue, window.Next(3, true).Value);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 2_000_000);
    }
    [Fact]
    public void SelectedInputsAndRejectedBarsPreserveState()
    {
        var selected = new[] { 2d, -4, 9, 3, -7, 0, 19, -3 };
        var expected = BuiltInFormulaReferences.CombSpectrumValues(selected.Select(B).ToArray(), 4, 2, .3);
        var data = Data(Enumerable.Repeat(B(100), selected.Length).ToArray()); data.SetCustomValues(selected.ToList());
        data.CalculateEhlersCombFilterSpectralEstimate(4, 2); Assert.Equal(expected.Values, data.ChainedValues); Assert.Equal(expected.Signals, data.SignalsList);
        Assert.Equal(Enumerable.Repeat(100d, selected.Length), data.ClosePrices);
        using var state = new EhlersCombFilterSpectralEstimateState(4, 2); using var clean = new EhlersCombFilterSpectralEstimateState(4, 2);
        state.Update(Native(B(7)), true, false); clean.Update(Native(B(7)), true, false);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var slot in Enumerable.Range(0, 5))
            {
                var p = new[] { 1d, 2, 0, 1, 3 }; p[slot] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, p[0], p[1], p[2], p[3], p[4])), true, true));
            }
        foreach (var p in selected) Assert.Equal(clean.Update(Native(B(p)), true, false).Value, state.Update(Native(B(p)), true, false).Value);
        foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => new CombSpectrumWindow(4, 2, invalid));
    }
}
