using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PriceVolumeNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void IndependentLaggedLegsPreserveWideChangesAndStartup()
    {
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue })
        foreach (var period in new[] { 1, 3, 17 })
        {
            var bars = Enumerable.Range(0, 64).Select(i =>
            {
                var price = (i % 4 == 0 ? -1 : i % 4 == 1 ? 1 : 0) * scale;
                var volume = (i % 7 == 0 ? -1 : i % 7 == 1 ? 1 : 0) * scale;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, volume);
            }).ToArray();
            var indicator = (IBuiltInIndicator)new PriceVolumeOscillator(period);
            var expected = BuiltInFormulaReferences.PriceVolumeOutputs(bars, indicator); var options = indicator.CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                var spec = new IndicatorSpec(indicator.BatchName, options, pair.Key);
                Assert.Equal(pair.Value, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
                using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(pair.Value, arm.Value.ToArray());
                var selected = Data(bars); selected.CustomValuesList = bars.Select((_, i) => (double)(i % 5)).ToList();
                var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected.CustomValuesList[i], b.Volume)).ToArray();
                var selectedExpected = BuiltInFormulaReferences.PriceVolumeOutputs(projected, indicator)[pair.Key];
                using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Assert.Equal(selectedExpected, selectedArm.Value.ToArray());
                Assert.Equal(selectedExpected, BuilderArmBinding.Compute(selected, spec, target).ToArray());
            }
            foreach (var volumeLength in new[] { 1, 3, 14 })
            {
                var varied = BuiltInFormulaReferences.PriceVolumeOutputs(bars, indicator, volumeLength);
                var batch = Data(bars).CalculatePriceVolumeOscillator(period, volumeLength);
                foreach (var pair in varied) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
                var priceCore = new double[bars.Length]; var volumeCore = new double[bars.Length];
                OoplesFinance.StockIndicators.Core.OscillatorCore.PriceVolumeOscillator(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), priceCore, volumeCore, period, volumeLength);
                Assert.Equal(varied["Po"], priceCore); Assert.Equal(varied["Vo"], volumeCore);
                using var volumeArm = IndicatorCompute.ComputePriceVolumeOscillatorFast(Data(bars), context, volumeLength, true);
                Assert.Equal(varied["Vo"], volumeArm.ToArray());
                using var state = new PriceVolumeOscillatorState(period, volumeLength);
                CheckState(state, bars, varied);
            }
            var primary = new IndicatorSpec(indicator.BatchName, options);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(primary), StreamingIndicatorFactory.CreateState(primary)! })
            { Assert.NotNull(state); using var lifetime = state as IDisposable; CheckState(state, bars, expected); }
            Assert.All(expected["Po"].Take(period), v => Assert.Equal(0, v));
            Assert.All(expected.Values.SelectMany(v => v), v => Assert.InRange(v, -1, 1));
        }
    }
    private static void CheckState(IStreamingIndicatorState state, Bar[] bars, IReadOnlyDictionary<string, double[]> expected)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(bars[i].Time, 100, 100, 100, 100, 100)), false, true);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                }
            }
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 1))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => new PriceVolumeOscillatorState(3, 2);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "PriceVolumeOscillator" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.PriceVolumeOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
