using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanAdaptiveBandNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(KaufmanAdaptiveBands)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    private static Bar[] Bars(params double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KAB", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCenteredVariance(IndicatorValidationCase c, string route)
    {
        var o = (KaufmanAdaptiveBandsSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.KaufmanAdaptiveIntegerValues(bars, o.Length, (int)o.StdDevFactor), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveOriginalCandles(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EveryNumericalClassIsEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    [Fact]
    public void EnrolledReferenceRejectsEvenASubnormalOutputFault()
    {
        var indicator = ((IndicatorValidationCase)Cases.First()[0]).Factory();
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        var bars = Bars(1);
        foreach (var rule in rules)
        {
            var outputs = rules.Select(_ => (IReadOnlyList<double>)new[] { 0d }).ToArray();
            outputs[rule.ReferenceOutputSlot!.Value] = new[] { double.Epsilon };
            Assert.Throws<InvalidOperationException>(() => rule.Check(new IndicatorValidationContext("exact/hand", bars, outputs, 0)));
        }
    }

    [Fact]
    public void AlgebraicCancellationResolvesTheSubnormalMeanMidpoint()
    {
        var e = double.Epsilon;
        var bars = Bars(-3 * e, -5 * e, e, -e);
        var batch = Data(bars).CalculateKaufmanAdaptiveBands(2, .5);
        // Both gains are sqrt(1/2). The final mean is exactly -epsilon/2;
        // variance is (sqrt(2)-3/4)*epsilon^2. Round-to-even gives these bands.
        Assert.Equal(0d, batch.OutputValues["MiddleBand"][3]);
        Assert.Equal(0d, batch.OutputValues["UpperBand"][3]);
        Assert.Equal(-e, batch.OutputValues["LowerBand"][3]);
        using var state = new KaufmanAdaptiveBandsState(2, .5);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(17)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], result.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void IntervalProductsContainTheirIndependentRationalSquares()
    {
        var algebra = new KaufmanBandAlgebra(.5);
        foreach (var denominator in new[] { 2, 3, 5, 7, 13 })
        foreach (var bits in new[] { 16, 32, 96 })
        foreach (var scale in new[] { 1, 2, 3, 11, 1024 })
        {
            var basis = (F)1 / denominator; var root = algebra.Power(basis) * algebra.Constant(scale);
            var square = basis * scale * scale;
            foreach (var sign in new[] { -1, 1 })
            {
                var bounds = (root * root * algebra.Constant(sign)).Evaluate(bits);
                Assert.True(bounds.Lower.CompareTo(sign * square) <= 0);
                Assert.True(bounds.Upper.CompareTo(sign * square) >= 0);
            }
        }
    }

    [Fact]
    public void BandRoundingResolvesAlgebraicHalfwayTies()
    {
        var algebra = new KaufmanBandAlgebra(.5); var root = algebra.Power((F)1 / 2);
        var half = algebra.Constant(F.Of(double.Epsilon) / 2); var variance = root * root;
        // Both roots cancel exactly. At half epsilon, zero is the even neighbor;
        // at 1.5 epsilon, two epsilon is the even neighbor.
        Assert.Equal(0d, (half - root).Band(variance, 1));
        Assert.Equal(2 * double.Epsilon, (root + half * algebra.Constant(3)).Band(variance, -1));
    }

    [Fact]
    public void PowerClassesCancelWithoutFactoringTheRationalBases()
    {
        var algebra = new KaufmanBandAlgebra(.5);
        var a = algebra.Power((F)1 / 2); var b = algebra.Power((F)1 / 8);
        Assert.Equal(0, (a - algebra.Constant(2) * b).Sign());
        Assert.Equal(.5, (a * a).Publish());
        var fourth = new KaufmanBandAlgebra(.25);
        var root = fourth.Power((F)1 / 4);
        Assert.Equal(0, (root * root - fourth.Constant((F)1 / 2)).Sign());
        Assert.Equal(0d, root.Band(root * root, -1));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IntegerPowersMatchCenteredVarianceAcrossScales(int exponent)
    {
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 8 })
        {
            var bars = Bars(new[] { -3d, -5, 1, -1, 2, 0, -2, 3 }.Select(x => x * scale).ToArray());
            var expected = BuiltInFormulaReferences.KaufmanAdaptiveIntegerValues(bars, 2, exponent);
            var batch = Data(bars).CalculateKaufmanAdaptiveBands(2, exponent);
            using var state = new KaufmanAdaptiveBandsState(2, exponent);
            for (var i = 0; i < bars.Length; i++)
            {
                var result = state.Update(Native(bars[i]), true, true);
                foreach (var key in expected.Keys)
                { Assert.Equal(expected[key][i], batch.OutputValues[key][i]); Assert.Equal(expected[key][i], result.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void LazyPeriodsAndUnusedCandleFieldsPreserveThePriceContract()
    {
        var bars = Bars(1, -1, 2); var data = Data(bars); data.SetCustomValues(new List<double> { 7, 8, 9 });
        Assert.Equal(new[] { 7d, 8, 9 }, data.CalculateKaufmanAdaptiveBands(int.MaxValue, 0).OutputValues["MiddleBand"]);
        Assert.Equal(new[] { 1d, -1, 2 }, data.ClosePrices);
        using var state = new KaufmanAdaptiveBandsState(int.MaxValue);
        Assert.All(bars, b => Assert.Equal(0d, state.Update(Native(b), true, false).Value));
        Assert.Empty(Data([]).CalculateKaufmanAdaptiveBands(int.MaxValue).OutputValues["MiddleBand"]);
    }
    [Fact]
    public void RejectedPricesDoNotAdvanceHistory()
    {
        using var actual = new KaufmanAdaptiveBandsState(2); using var control = new KaufmanAdaptiveBandsState(2);
        var seed = Native(Bars(1)[0]); actual.Update(seed, true, false); control.Update(seed, true, false);
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(Bars(value)[0]), final, true));
        foreach (var bar in Bars(2, -1, 3, -2))
        {
            var expected = control.Update(Native(bar), true, true); var result = actual.Update(Native(bar), true, true);
            foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], result.Outputs![key]);
        }
    }
}
