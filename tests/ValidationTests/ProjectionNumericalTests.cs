using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ProjectionNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PROJ", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ProjectionBands) || c.IndicatorType == typeof(ProjectionOscillator)
            || c.IndicatorType == typeof(ProjectionBandwidth)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCenteredFits(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.ProjectionOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputRetainsCandleBounds(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    [Fact]
    public void FastCallbackUsesIsolatedSelectedInput()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(2 + i % 5, i)).ToArray();
        var selected = bars.Select(b => b.Close / 2).ToList();
        var data = Data(bars).WithValues(selected);
        var outputs = data.OutputValues; var signals = data.SignalsList; var name = data.IndicatorName;
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, _) => values.Select(_ => 3d).ToArray();
        using var scope = ComponentAverage.Arm(Enumerable.Repeat(callback, 20).ToArray());
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeProjectionFamilyFast(data, context, IndicatorName.ProjectionOscillator,
            3, MovingAvgType.WeightedMovingAverage, 2, "Signal");
        Assert.Equal(Enumerable.Repeat(3d, bars.Length), result.ToArray());
        Assert.True(ComponentAverage.Substitutions > 0);
        Assert.Same(selected, data.CustomValuesList); Assert.Same(outputs, data.OutputValues);
        Assert.Same(signals, data.SignalsList); Assert.Equal(name, data.IndicatorName);
    }

    [Fact]
    public void HiddenBandOverflowRetainsFiniteRatiosAndMiddle()
    {
        var bars = new[] { B(-double.MaxValue), B(double.MaxValue, 1), B(0, 2) };
        using var bands = new ProjectionBandsCalculator(2);
        foreach (var b in bars.Take(2)) bands.Update(b.High, b.Low, true);
        var last = bands.Update(0, 0, true);
        Assert.True(double.IsPositiveInfinity(last.Upper)); Assert.Equal(double.MaxValue, last.Middle);
        Assert.Equal(0, last.Lower); Assert.Equal(200, ProjectionBandsSnapshot.Publish(last.Bandwidth));
        Assert.Equal(200, Data(bars).CalculateProjectionBandwidth(length: 2).OutputValues["Pbw"][2]);
        Assert.Equal(0, Data(bars).CalculateProjectionOscillator(length: 2).OutputValues["Pbo"][2]);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(int.MaxValue)]
    public void ObservedHistoryPreservesLagPairingPreviewResetAndHugePeriods(int length)
    {
        var bars = new[] { B(3), B(1, 1), B(7, 2), B(-2, 3), B(4, 4) };
        var expected = BuiltInFormulaReferences.ProjectionOutputs(bars, IndicatorName.ProjectionBands, length, 2, 4);
        using var state = new ProjectionBandsState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(29)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(-87)), false, false);
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(B(double.NaN)), true, false));
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]);
                }
            }
        }
        Assert.Equal(expected["MiddleBand"], Data(bars).CalculateProjectionBands(length).OutputValues["MiddleBand"]);
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)] [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)] [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void LazyAverageMatchesExistingRingAndIndependentProjectionSignal(MovingAvgType kind)
    {
        using var lazy = new RocBankAverage(kind, 3, 3, observedHistory: true);
        using var ring = new RocBankAverage(kind, 3, 3);
        foreach (var value in new[] { 1d, -3d, 7d, 2d, -11d, 5d })
            foreach (var final in new[] { false, false, true })
                Assert.Equal(ring.Next(new RocBankValue(value), final).Publish(), lazy.Next(new RocBankValue(value), final).Publish());
        lazy.Reset(); ring.Reset();
        Assert.Equal(ring.Next(new RocBankValue(13), true).Publish(), lazy.Next(new RocBankValue(13), true).Publish());
        var bars = new[] { B(1), B(3, 1), B(2, 2), B(7, 3), B(-4, 4) };
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2
            : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.ProjectionOutputs(bars, IndicatorName.ProjectionOscillator, 3, code, 2);
        var actual = Data(bars).CalculateProjectionOscillator(kind, 3, 2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], actual.OutputValues[key]);
    }

    [Fact]
    public void SignalsUseExactSlopeAndPreserveVolatilityGates()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 900) })
        foreach (var family in new[] { IndicatorName.ProjectionBands, IndicatorName.ProjectionOscillator, IndicatorName.ProjectionBandwidth })
        {
            var bars = new[] { 3, -1, 7, 2, -4, 9, 1, 5 }.Select((p, i) => B(p * scale, i)).ToArray();
            var reference = BuiltInFormulaReferences.ProjectionOutputs(bars, family, 2, 2, 2);
            var data = Data(bars);
            if (family == IndicatorName.ProjectionBands) data.CalculateProjectionBands(2);
            else if (family == IndicatorName.ProjectionOscillator) data.CalculateProjectionOscillator(length: 2, smoothLength: 2);
            else data.CalculateProjectionBandwidth(length: 2);
            ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
            var previousSlope = R(0);
            for (var i = 0; i < bars.Length; i++)
            {
                var value = R(bars[i].Close); var previous = i == 0 ? R(0) : R(bars[i - 1].Close);
                var mean = family == IndicatorName.ProjectionBands ? R(reference["MiddleBand"][i])
                    : ((new ReferenceFraction(2) * value + previous) / new ReferenceFraction(3)).RoundExtendedBinary64();
                var slope = value - mean; var expected = Signal.None;
                var active = family == IndicatorName.ProjectionBands || (family == IndicatorName.ProjectionOscillator
                    ? reference["Signal"][i] >= (i == 0 ? 0 : reference["Signal"][i - 1])
                    : reference["Pbw"][i] >= reference["Signal"][i]);
                if (active)
                {
                    if (slope.Sign > 0) expected = slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : Signal.Buy;
                    else if (slope.Sign < 0) expected = slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : Signal.Sell;
                    else if (family == IndicatorName.ProjectionBands)
                    {
                        if (previous.CompareTo(R(i == 0 ? 0 : reference["LowerBand"][i - 1])) < 0 && value.CompareTo(R(reference["LowerBand"][i])) > 0) expected = Signal.Buy;
                        else if (previous.CompareTo(R(i == 0 ? 0 : reference["UpperBand"][i - 1])) > 0 && value.CompareTo(R(reference["UpperBand"][i])) < 0) expected = Signal.Sell;
                    }
                }
                Assert.Equal(expected, data.SignalsList[i]); previousSlope = slope;
            }
        }
    }

    [Fact]
    public void CorePreservesAliasedInputsAndRejectsInvalidBeforeWrites()
    {
        var bars = new[] { B(1), B(3, 1), B(2, 2), B(-4, 3) };
        var expected = BuiltInFormulaReferences.ProjectionOutputs(bars, IndicatorName.ProjectionOscillator, 2, 2, 4)["Pbo"];
        foreach (var aliased in new[] { 0, 1, 2 })
        {
            var high = bars.Select(b => b.High).ToArray(); var low = high.ToArray(); var close = high.ToArray();
            var output = aliased == 0 ? high : aliased == 1 ? low : close;
            OscillatorCore.ProjectionOscillator(high, low, close, output, 2); Assert.Equal(expected, output);
        }
        var untouched = new[] { 17d, 19d };
        Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.ProjectionOscillator(new[] { 1d, double.NaN }, new[] { 1d, 2d }, new[] { 1d, 2d }, untouched, 2));
        Assert.Equal(new[] { 17d, 19d }, untouched);
    }
}
