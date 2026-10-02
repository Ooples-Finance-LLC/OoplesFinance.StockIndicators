using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PeriodicChannelNumericalTests
{
    [Fact]
    public void LocalRationalArithmeticMatchesIndependentUncancelledFractions()
    {
        var values = new[] { 0d, 1, -1, 3, -7, double.Epsilon, -double.Epsilon, double.MaxValue, Math.BitIncrement(1d) };
        foreach (var a in values) foreach (var b in values) foreach (var divisor in new[] { 1d, 3, 7, double.Epsilon, double.MaxValue })
        {
            var left = PeriodicChannelWindow.Number.Of(a).Divide(PeriodicChannelWindow.Number.Of(divisor));
            var right = PeriodicChannelWindow.Number.Of(b).Divide(3);
            var x = ReferenceFraction.FromDouble(a) / ReferenceFraction.FromDouble(divisor);
            var y = ReferenceFraction.FromDouble(b) / new ReferenceFraction(3);
            void Equal(ReferenceFraction expected, PeriodicChannelWindow.Number actual)
            {
                var (numerator, denominator) = expected.Components;
                Assert.Equal(numerator * actual.Denominator, actual.Numerator * denominator);
                Assert.Equal(System.Numerics.BigInteger.One, System.Numerics.BigInteger.GreatestCommonDivisor(System.Numerics.BigInteger.Abs(actual.Numerator), actual.Denominator));
                Assert.Equal(expected.ToDouble(), actual.Publish());
            }
            Equal(x + y, left + right); Equal(x - y, left - right); Equal(x * y, left * right);
            if (y.Sign != 0) Equal(x / y, left.Divide(right));
            var bands = PeriodicChannelWindow.Number.PublishBands(left, right);
            var expectedBands = new[] { x, y, x + y, x + new ReferenceFraction(2) * y, x + new ReferenceFraction(3) * y,
                x - y, x - new ReferenceFraction(2) * y, x - new ReferenceFraction(3) * y };
            Assert.Equal(expectedBands.Select(v => v.ToDouble()), bands);
        }
    }
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PERIODIC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(PeriodicChannel)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCenteredMoments(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.PeriodicOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)
    {
        Assert.Equal(8, c.Factory().Outputs.Count);
        Assert.Equal(new[] { "K", "Os", "Ap", "Bp", "Cp", "Al", "Bl", "Cl" }, GeneratedIndicatorOutputs.KeysFor(IndicatorName.PeriodicChannel));
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
        var indicator = (PeriodicChannel)c.Factory();
        var bars = Bars(new[] { 0d, 0, 1, 2, 1, 0, double.Epsilon, -3, 2 });
        var expected = BuiltInFormulaReferences.PeriodicValues(bars, indicator.Length1, indicator.Length2).Outputs;
        var actual = Data(bars).CalculatePeriodicChannel(indicator.Length1, indicator.Length2).OutputValues;
        foreach (var key in expected.Keys)
        {
            var values = actual[key].ToArray();
            var rule = IndicatorValidationRule.Reference(0, _ => expected[key], IndicatorErrorBudget.Exact);
            rule.Check(new IndicatorValidationContext("batch-slot", bars, new[] { values }, 0));
            values[^1] += 1;
            Assert.Throws<InvalidOperationException>(() => rule.Check(new IndicatorValidationContext("batch-slot-fault", bars, new[] { values }, 0)));
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static Dictionary<string, double[]> Check(double[] prices, int period = 5, int lookback = 3)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.PeriodicValues(bars, period, lookback);
        var batch = Data(bars).CalculatePeriodicChannel(period, lookback);
        foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]);
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var state = new PeriodicChannelState(period, lookback); using var window = new PeriodicChannelWindow(period, lookback);
        var sign = new PeriodicCorrelationSign(lookback);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Bars(new[] { 19d })[0]), true, false); window.Next(19, true); sign.Next(19, true);
            state.Reset(); window.Reset(); sign.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Update(Native(Bars(new[] { -71d })[0]), false, false); window.Next(-71, false); sign.Next(-71, false);
                foreach (var final in new[] { false, false, true })
                {
                    Assert.Equal(expected.Directions[i], sign.Next(prices[i], final));
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(prices[i], final);
                    Assert.Equal(expected.Outputs["K"][i], point.Value); Assert.Equal(expected.Signals[i], direct.Trade);
                    foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                    Assert.Equal(expected.Outputs.Values.Select(v => v[i]), direct.Values);
                }
            }
        }
        return expected.Outputs;
    }

    [Fact]
    public void OppositeRadicalsCancelExactlyAndSubnormalPerturbationsChooseBothDirections()
    {
        foreach (var perturbation in new[] { 0d, double.Epsilon, -double.Epsilon })
        {
            var prices = new[] { 0d, 0, 1, 2, 1, 0, perturbation };
            // sqrt(3)/2 + 1 - 1 - sqrt(3)/2 = 0. The perturbed last
            // square is (3/4)*(1-e)^2/(1-e+e^2); comparison gives sign(e).
            var reference = BuiltInFormulaReferences.PeriodicValues(Bars(prices), 5, 3);
            Assert.Equal(Math.Sign(perturbation), reference.Directions[^1]);
            var sign = new PeriodicCorrelationSign(3);
            foreach (var price in prices.Take(6)) sign.Next(price, true);
            Assert.Equal(Math.Sign(perturbation), sign.Next(perturbation, false));
            Assert.Equal(0, sign.Next(0, false));
            Assert.Equal(Math.Sign(perturbation), sign.Next(perturbation, true));
            Check(prices);
        }
    }

    [Fact]
    public void IndependentBarIndexAndBandHands()
    {
        var output = Check(new[] { 2d, 2, 2 }, lookback: 1);
        Assert.Equal(new[] { 0d, 4, 8 }, output["K"]);
        Assert.Equal(new[] { 0d, 4, 5 }, output["Os"]);
        Assert.Equal(new[] { 0d, 16, 23 }, output["Cp"]);
        Assert.Equal(new[] { 0d, -8, -7 }, output["Cl"]);
    }

    [Fact]
    public void ExactMomentsSurviveOverflowAndSubnormalScaling()
    {
        var pattern = new[] { 0d, 0, 1, 2, 1, 0, 0, -1, 1, 0, 2, -2 };
        foreach (var lookback in new[] { 2, 3, 5, 9 })
        {
            var signs = new PeriodicCorrelationSign(lookback); var expected = pattern.Select(p => signs.Next(p, true)).ToArray();
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1022) })
            {
                signs.Reset(); Assert.Equal(expected, pattern.Select(p => signs.Next(p * scale, true)));
                Check(pattern.Select(p => p * scale).ToArray(), lookback: lookback);
            }
        }
        Check(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0d, -double.MaxValue });
    }

    [Fact]
    public void ExpiryFlatWindowsAndExtremePeriodsRemainLazy()
    {
        var prices = new[] { 1d, 1, Math.BitIncrement(1d), 1, Math.BitDecrement(1d), 1, 1, 7, -2, 3 };
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue })
        { Check(Array.Empty<double>(), period, period); Check(prices, period, period); }
        Assert.All(Check(new double[10])["K"], v => Assert.Equal(0, v));
    }

    [Fact]
    public void RejectedCandlesDoNotAdvanceExactState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(new[] { 1d, invalid })).CalculatePeriodicChannel());
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new PeriodicChannelState(5, 3); using var control = new PeriodicChannelState(5, 3);
                foreach (var bar in Bars(new[] { 0d, 0, 1, 2, 1, 0 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
                var input = new[] { 1d, 2, 0, 1, 1 }; input[field] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, true));
                foreach (var bar in Bars(new[] { double.Epsilon, 0, -3, 2 }))
                {
                    var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true);
                    foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]);
                }
            }
        }
    }
}
