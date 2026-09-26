using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeBalanceNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactSignedVolumesAndProductsSurviveCancellationAndEviction()
    {
        var prices = new[] { 2d, 5, 3, 8, 3, 1, 5, 0, -1, -3, 2, 2, 2 };
        var ordinary = BarsOf(prices).Select((b, i) => new Bar(b.Time, i % 2 == 0 ? 0 : 9, 12, -4, b.Close, i % 3 == 0 ? -3 : 2)).ToArray();
        var extreme = BarsOf(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, double.MaxValue, -double.MaxValue, 0, 0 })
            .Select((b, i) => new Bar(b.Time, i % 2 == 0 ? -1 : 1, b.High, b.Low, b.Close, i % 3 == 0 ? -double.MaxValue : double.MaxValue)).ToArray();
        var tiny = BarsOf(new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 })
            .Select((b, i) => new Bar(b.Time, 0, 3 * double.Epsilon, -double.Epsilon, b.Close, i % 2 == 0 ? double.Epsilon : 2)).ToArray();
        foreach (var bars in new[] { ordinary, extreme, tiny })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var indicator in new IBuiltInIndicator[] { new UpsideDownsideVolume(period), new TFSVolumeOscillator(period), new VolumeAccumulationOscillator(period) })
        {
            var expectedPair = BuiltInFormulaReferences.VolumeBalanceOutputs(bars, indicator).Single(); var expected = expectedPair.Value;
            var options = indicator.CreateOptions(); var spec = new IndicatorSpec(indicator.BatchName, options);
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
            using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var projected = bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray();
            var selectedExpected = BuiltInFormulaReferences.VolumeBalanceOutputs(projected, indicator)[expectedPair.Key];
            var selected = Data(bars); selected.CustomValuesList = bars.Select(_ => 42d).ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Assert.Equal(selectedExpected, selectedArm.Value.ToArray());
            Assert.Equal(selectedExpected, BuilderArmBinding.Compute(selected, spec, target).ToArray());
            var core = new double[bars.Length]; var close = bars.Select(b => b.Close).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
            switch (indicator.BatchName)
            {
                case IndicatorName.UpsideDownsideVolume:
                    OoplesFinance.StockIndicators.Core.OscillatorCore.UpsideDownsideVolume(close, volume, core, period); break;
                case IndicatorName.TFSVolumeOscillator:
                    OoplesFinance.StockIndicators.Core.OscillatorCore.TFSVolumeOscillator(bars.Select(b => b.Open).ToArray(), close, volume, core, period); break;
                default:
                    OoplesFinance.StockIndicators.Core.OscillatorCore.VolumeAccumulationOscillator(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), close, volume, core, period); break;
            }
            Assert.Equal(expected, core);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec)! })
            {
                Assert.NotNull(state); using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                        foreach (var final in new[] { false, false, true })
                            Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                    }
                }
            }
        }
        var one = new[] { new Bar(DateTime.UnixEpoch, 0, 5, -1, 3, 6) };
        Assert.Equal(2, BuiltInFormulaReferences.VolumeBalanceOutputs(one, new TFSVolumeOscillator(3))["Tfsvo"][0]);
        Assert.Equal(6, BuiltInFormulaReferences.VolumeBalanceOutputs(one, new VolumeAccumulationOscillator(3))["Vao"][0]);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 3))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant switch { 0 => new UpsideDownsideVolumeState(3), 1 => new TFSVolumeOscillatorState(3), _ => new VolumeAccumulationOscillatorState(3) };
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "UpsideDownsideVolume", "TFSVolumeOscillator", "VolumeAccumulationOscillator" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.VolumeBalanceOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
