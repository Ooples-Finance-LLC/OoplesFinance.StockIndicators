using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DemarkPressureNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactPressurePreservesProductsSelectedInputsAndSignedCancellation()
    {
        Bar B(int i, double open, double high, double low, double close, double volume) => new(DateTime.UnixEpoch.AddMinutes(i), open, high, low, close, volume);
        var ordinary = Enumerable.Range(0, 20).Select(i => B(i, 20 + i % 4, 30 + i % 5, 12 - i % 3, 15 + i * 7 % 14, i % 3 == 0 ? -2 : i % 4 + 1)).ToArray();
        var extremes = Enumerable.Range(0, 20).Select(i => B(i, 0, double.MaxValue, -double.MaxValue, i % 2 == 0 ? double.MaxValue : -double.MaxValue, i % 3 == 0 ? -double.MaxValue : double.MaxValue)).ToArray();
        var tiny = Enumerable.Range(0, 20).Select(i => B(i, 0, 1, 0, i % 2 == 0 ? double.Epsilon : -double.Epsilon, double.Epsilon)).ToArray();
        var signed = new[] { B(0, 0, 2, -2, 1, -1), B(1, 0, 2, -2, -1, 1), B(2, -5, 1, -7, -3, 2), B(3, -4, 1, -5, -2, -1) };
        foreach (var bars in new[] { ordinary, extremes, tiny, signed })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var indicator in new IBuiltInIndicator[] { new DemarkPressureRatioV1(period), new DemarkPressureRatioV2(period) })
        {
            var expected = BuiltInFormulaReferences.DemarkPressureOutputs(bars, indicator)["Dpr"];
            var options = indicator.CreateOptions(); var spec = new IndicatorSpec(indicator.BatchName, options);
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
            using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var selectedBars = bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray();
            var selected = Data(bars); selected.CustomValuesList = bars.Select(_ => 42d).ToList();
            var selectedExpected = BuiltInFormulaReferences.DemarkPressureOutputs(selectedBars, indicator)["Dpr"];
            using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Assert.Equal(selectedExpected, selectedArm.Value.ToArray());
            Assert.Equal(selectedExpected, BuilderArmBinding.Compute(selected, spec, target).ToArray());
            var core = new double[bars.Length]; var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
            var open = bars.Select(b => b.Open).ToArray(); var close = bars.Select(b => b.Close).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
            if (indicator.BatchName == IndicatorName.DemarkPressureRatioV1) OoplesFinance.StockIndicators.Core.OscillatorCore.DemarkPressureRatioV1(high, low, open, close, volume, core, period);
            else OoplesFinance.StockIndicators.Core.OscillatorCore.DemarkPressureRatioV2(high, low, open, close, volume, core, period);
            Assert.Equal(expected, core);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec)! })
            {
                Assert.NotNull(state); using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        state.Update(Native(B(0, 0, 4, -2, 3, 7)), false, true);
                        foreach (var final in new[] { false, false, true })
                        {
                            var actual = state.Update(Native(bars[i]), final, true);
                            Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Dpr"]);
                            Assert.InRange(actual.Value, 0, 100);
                        }
                    }
                }
            }
        }
        foreach (var second in new[] { false, true })
        {
            using var subnormal = new DemarkPressureWindow(3, second);
            Assert.Equal(100, subnormal.Next(0, 1, 0, double.Epsilon, double.Epsilon, true));
            using var cancellation = new DemarkPressureWindow(2, second);
            cancellation.Next(0, 2, -2, 1, -1, true);
            Assert.Equal(second ? 50 : 0, cancellation.Next(0, 2, -2, -1, 1, true));
        }
    }

    [Fact]
    public void GapBranchesAreStrictlyAboveFifteenPercent()
    {
        double Up(double open)
        {
            using var window = new DemarkPressureWindow(1, false);
            window.Next(100, 100, 100, 100, 0, true);
            return window.Next(open, 120, 90, 110, 1, true);
        }
        double Down(double open)
        {
            using var window = new DemarkPressureWindow(1, false);
            window.Next(115, 115, 115, 115, 0, true);
            return window.Next(open, 120, 90, 110, 1, true);
        }
        Assert.Equal(0, Up(Math.BitDecrement(115))); Assert.Equal(0, Up(115)); Assert.True(Up(Math.BitIncrement(115)) > 80);
        Assert.Equal(100, Down(Math.BitIncrement(100))); Assert.Equal(100, Down(100)); Assert.True(Down(Math.BitDecrement(100)) < 30);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 2))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant switch { 0 => new DemarkPressureRatioV1State(4), _ => new DemarkPressureRatioV2State(4) };
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "DemarkPressureRatioV1", "DemarkPressureRatioV2" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route);

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
