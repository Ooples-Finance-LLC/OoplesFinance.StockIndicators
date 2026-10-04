using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PrimeNumberNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(PrimeNumberOscillator) || c.IndicatorType == typeof(PrimeNumberBands)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    private static Bar B(double price) => new(DateTime.UnixEpoch, price, price, price, price, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("PN", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentBoundedSearch(IndicatorValidationCase c, string route)
    {
        var indicator = (IBuiltInIndicator)c.Factory(); var options = indicator.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var bars = new[] { -8d, 0, double.Epsilon, 1.5, 2.5, 4, 11, 12, 14, 17, 18, 20, 24, 8, 100, PrimeNumberSearch.Maximum, PrimeNumberSearch.Minimum }.Select(B).ToArray();
        var expected = BuiltInFormulaReferences.PrimeOutputs(bars, length, indicator.BatchName == IndicatorName.PrimeNumberBands);
        if (route == "batch")
        {
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
            foreach (var pair in expected) Assert.Equal(pair.Value, BuilderArmBinding.Compute(Data(bars), new IndicatorSpec(indicator.BatchName, options, pair.Key), target).ToArray());
            if (indicator.BatchName == IndicatorName.PrimeNumberOscillator)
            {
                using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePrimeNumberOscillatorFast(Data(bars), context, length);
                Assert.Equal(expected["Pno"], fast.ToArray());
            }
            return;
        }
        var spec = new IndicatorSpec(indicator.BatchName, options);
        var state = route == "native" ? StatefulIndicatorFactory.Create(spec) : StreamingIndicatorFactory.CreateState(spec);
        Assert.NotNull(state); using var lifetime = state as IDisposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(101)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    foreach (var pair in expected) Assert.Equal(pair.Value[i], result.Outputs![pair.Key]);
                }
            }
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveRanges(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void PublishedOutputsRejectFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Fact]
    public void PrimalityMatchesDivisorDefinitionAndKnownLargeCertificates()
    {
        for (long n = -10; n < 20000; n++) Assert.Equal(BuiltInFormulaReferences.PrimeDefinition(n), PrimeNumberSearch.IsPrime(n));
        foreach (var prime in new[] { 2305843009213693951L, 9223372036854775783L, 9223372036854774739L, 9223372036854774797L })
        { Assert.True(PrimeNumberSearch.IsPrime(prime)); Assert.True(BuiltInFormulaReferences.PrimeDefinition(prime)); }
        foreach (var composite in new[] { 3215031751L, 341550071728321L, 3825123056546413051L, long.MaxValue })
        { Assert.False(PrimeNumberSearch.IsPrime(composite)); Assert.False(BuiltInFormulaReferences.PrimeDefinition(composite)); }
    }
    [Fact]
    public void LargePrimeDistancesDoNotRoundAway()
    {
        Assert.Equal((9223372036854774797L, 9223372036854774739L), PrimeNumberSearch.Find(PrimeNumberSearch.Maximum, int.MaxValue));
        Assert.Equal(new[] { 13d, 13 }, Data(new[] { B(PrimeNumberSearch.Maximum), B(2) }).CalculatePrimeNumberOscillator().OutputValues["Pno"]);
        Assert.Equal(new[] { 13d }, BuiltInFormulaReferences.PrimeOffsetsReference(new[] { PrimeNumberSearch.Maximum }, 5));
        Assert.Equal(13d, new PrimeOffsetWindow(5).Next(PrimeNumberSearch.Maximum, true));
    }
    [Fact]
    public void MissingSearchAndExactTiesRetainSpecifiedHistory()
    {
        var window = new PrimeOffsetWindow(5);
        Assert.Equal(-8d, window.Next(8, true)); // no prime in the rounded five-percent interval
        Assert.Equal(-8d, window.Next(11, true)); // an exact prime retains the previous offset
        Assert.Equal(-1d, window.Next(12, true)); // equal distances select the lower prime
        Assert.Equal(11d, window.Next(0, true)); // missing primes reuse both remembered candidates
        Assert.Equal(-12d, window.Next(25, true)); // a missing interval above remembered primes uses the upper carry
        window.Reset(); Assert.Equal(0d, window.Next(0, true));
        Assert.Equal((2L, 2L), PrimeNumberSearch.Find(1.5, 5));
    }
    [Fact]
    public void BoundedDomainRejectsBeforeAdvancingEitherBand()
    {
        foreach (var bands in new[] { false, true })
        {
            IStreamingIndicatorState a = bands ? new PrimeNumberBandsState(2) : new PrimeNumberOscillatorState(2);
            IStreamingIndicatorState b = bands ? new PrimeNumberBandsState(2) : new PrimeNumberOscillatorState(2);
            a.Update(Native(B(8)), true, true); b.Update(Native(B(8)), true, true);
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, 9223372036854775808d, Math.BitDecrement(PrimeNumberSearch.Minimum) })
            {
                var bad = new Bar(DateTime.UnixEpoch, 11, 11, invalid, 11, 1);
                IIndicator indicator = bands ? new PrimeNumberBands(2) : new PrimeNumberOscillator(2);
                Assert.NotNull(IndicatorInputDomain.For(indicator).Violation(bad));
                foreach (var final in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => a.Update(Native(bad), final, true));
                var data = Data(new[] { bad });
                Assert.Throws<ArgumentOutOfRangeException>(() => { if (bands) data.CalculatePrimeNumberBands(2); else data.CalculatePrimeNumberOscillator(2); });
            }
            foreach (var value in new[] { 11d, 12, 8, 0 })
            {
                var expected = b.Update(Native(B(value)), true, true); var actual = a.Update(Native(B(value)), true, true);
                Assert.Equal(expected.Outputs, actual.Outputs);
            }
        }
    }
    [Fact]
    public void DirectSelectedInputsKeepOriginalCandlesAndSignedSignals()
    {
        var data = Data(Enumerable.Repeat(B(100), 4).ToArray()); var selected = new[] { 8d, 11, 12, 0 };
        data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputePrimeNumberOscillatorFast(data, context, 5);
        Assert.Equal(new[] { -8d, -8, -1, 11 }, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculatePrimeNumberOscillator();
        Assert.Equal(new[] { -8d, -8, -1, 11 }, data.ChainedValues);
        Assert.Equal(new[] { Signal.StrongSell, Signal.Sell, Signal.Sell, Signal.StrongBuy }, data.SignalsList);
        Assert.Equal(Enumerable.Repeat(100d, 4), data.ClosePrices);
    }
    [Fact]
    public void HugePeriodsUseObservedHistoryAndBandMinimumRemainsTwo()
    {
        var bars = new[] { B(8), B(12), B(11), B(14) };
        foreach (var length in new[] { int.MaxValue, 1, 0, int.MinValue })
        {
            var expected = BuiltInFormulaReferences.PrimeOutputs(bars, Math.Max(1, length), true);
            var data = Data(bars).CalculatePrimeNumberBands(length);
            Assert.Equal(expected["UpperBand"], data.OutputValues["UpperBand"]); Assert.Equal(expected["LowerBand"], data.OutputValues["LowerBand"]);
        }
    }
}

