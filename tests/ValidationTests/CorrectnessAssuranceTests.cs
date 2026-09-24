using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CorrectnessAssuranceTests
{
    [Fact]
    public void TwoPoleReferenceRetainsDecayingImpulsesBelowDecimalResolution()
    {
        var input = new double[200];
        input[0] = 1;
        var actual = BuiltInFormulaReferences.PoleTrajectory(input, 8, 2, 1, 0, 0);
        var angle = Math.Sqrt(2) * Math.PI / 8;
        var radius = Math.Exp(-angle);
        var gain = 1 - 2 * radius * Math.Cos(angle) + radius * radius;
        foreach (var i in new[] { 90, 120, 180 })
        {
            var expected = gain * Math.Pow(radius, i) * Math.Sin((i + 1) * angle) / Math.Sin(angle);
            Assert.NotEqual(0d, actual[i]);
            Assert.True(Math.Abs(actual[i] - expected) <= Math.Abs(expected) * 1e-11,
                $"Impulse {i}: expected {expected:R}, got {actual[i]:R}");
        }
    }

    [Fact]
    public void BuiltInReferencesCheckStartupEvenWhenWarmupExceedsTheFixture()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, 1) };
        // Keep a FullReference adapter case as well as the specialized exact
        // references: promoting SMA/EMA must not leave the shared adapter untested.
        foreach (var (indicator, initial) in new (IIndicator, double)[]
            { (new Sma(2), 0), (new Ema(2), 1), (new OoplesFinance.StockIndicators.Indicators.Range(100), 0) })
        {
            var rule = Assert.Single(BuiltInFormulaReferences.For(indicator));
            rule.Check(new IndicatorValidationContext("startup", bars, new[] { new[] { initial } }, 100));
            Assert.Throws<InvalidOperationException>(() => rule.Check(
                new IndicatorValidationContext("wrong-startup", bars, new[] { new[] { initial + 1 } }, 100)));
        }
    }

    [Fact]
    public void OvershootHasAnIndependentStartupTrajectory()
    {
        var coverage = IndicatorFormulaCoverage.Inspect(new(typeof(OvershootReductionMovingAverage), "residual",
            () => new OvershootReductionMovingAverage(14)));
        Assert.True(coverage.IsComplete);
        Assert.True(coverage.HasCompleteIndependentTrajectories);
        Assert.Empty(coverage.RecurrenceOnlyOutputSlots);
        Assert.Equal(new[] { 0 }, coverage.IndependentTrajectoryOutputSlots);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(8.99999999995, 0)]
    [InlineData(9.00000000005, 0)]
    [InlineData(8.99, 1)]
    [InlineData(-8.99, -1)]
    public void MarketStateTreatsTheNineDegreeAmbiguityBandAsNeutral(double angle, double expected)
        => Assert.Equal(expected, OoplesFinance.StockIndicators.Helpers.EhlersCorrelationPhase.MarketState(angle, 0));

    [Fact]
    public void QuadraticMinusArithmeticMeanRespectsEachStartupSeed()
    {
        var bars = new[] { 1d, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new QmaSmaDifference(3))).Check(new IndicatorValidationContext(
            "hand-startup", bars, new[] { new[] { 1d, Math.Sqrt(2.5), Math.Sqrt(7) - 7d / 3 } }, 0));
    }

    [Fact]
    public void BatchRejectsAlignmentBeforeTouchingState()
    {
        var dates = new[] { DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1) };
        var primary = Stock(new[] { 100d, 110 }, dates);
        var benchmark = Stock(new[] { 100d, 200 }, dates.Select(t => t.AddDays(1)).ToArray());
        var state = new CountingPair();
        var key = new OoplesFinance.StockIndicators.Streaming.SeriesKey("P", OoplesFinance.StockIndicators.Streaming.BarTimeframe.Days(1));
        var market = new OoplesFinance.StockIndicators.Streaming.SeriesKey("M", key.Timeframe);
        Assert.Throws<ArgumentException>(() => BatchCompute.ComputeAllMultiSeries(primary, benchmark, state, key, market));
        Assert.Equal(0, state.Calls);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BatchRejectsTheEntireInvalidDomainBeforeTouchingState(double invalid)
    {
        var dates = new[] { DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1) };
        var primary = Stock(new[] { 100d, invalid }, dates);
        var benchmark = Stock(new[] { 100d, 110 }, dates);
        var state = new CountingPair();
        var key = new OoplesFinance.StockIndicators.Streaming.SeriesKey("P", OoplesFinance.StockIndicators.Streaming.BarTimeframe.Days(1));
        var market = new OoplesFinance.StockIndicators.Streaming.SeriesKey("M", key.Timeframe);
        Assert.Throws<ArgumentOutOfRangeException>(() => BatchCompute.ComputeAllMultiSeries(primary, benchmark, state, key, market));
        Assert.Equal(0, state.Calls);
        Assert.Throws<ArgumentOutOfRangeException>(() => primary.CalculateRSMKIndicator(benchmark));
        Assert.Throws<ArgumentOutOfRangeException>(() => benchmark.CalculateRSMKIndicator(primary));
    }

    private sealed class CountingPair : OoplesFinance.StockIndicators.Streaming.IMultiSeriesIndicatorState, IMultiSeriesInputDomainContract
    {
        internal int Calls;
        public IndicatorInputDomain PrimaryInputDomain => IndicatorInputDomain.PositiveClose;
        public IndicatorInputDomain BenchmarkInputDomain => IndicatorInputDomain.PositiveClose;
        public IndicatorName Name => IndicatorName.None;
        public void Reset() => Calls++;
        public OoplesFinance.StockIndicators.Streaming.MultiSeriesIndicatorStateResult Update(
            OoplesFinance.StockIndicators.Streaming.MultiSeriesContext context,
            OoplesFinance.StockIndicators.Streaming.SeriesKey series, OoplesFinance.StockIndicators.Streaming.OhlcvBar bar,
            bool isFinal, bool includeOutputs)
        {
            Calls++;
            return new(true, 0, null);
        }
    }
    [Fact]
    public async Task MissingFormulaFailsByDefaultButSmokeIsExplicitlyAvailable()
    {
        var testCase = new IndicatorValidationCase(typeof(Probe), "missing-contract", () => new Probe(0));
        var report = await IndicatorValidation.ValidateAsync(testCase);
        Assert.False(report.IsValid);
        Assert.Contains(report.Failures, f => f.Rule == "FormulaReference");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
        var smoke = await IndicatorValidation.ValidateAsync(testCase, IndicatorValidationOptions.Smoke());
        Assert.True(smoke.IsValid);
        Assert.False(smoke.FormulaCoverage!.IsComplete);
    }

    [Fact]
    public async Task WarmupDoesNotHideInfinity()
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(Probe), "warmup-infinity", () => new Probe(1)));
        Assert.Contains(report.Failures, f => f.Rule == "Finite" && f.Message.Contains("bar 0"));
    }

    [Fact]
    public async Task SignedOutputBudgetRejectsErasingATinySignal()
    {
        // Extreme generated prices can exceed the absolute budget even with the
        // sign check disabled. Isolate sign decisions within that budget too.
        var ordinary = new IndicatorErrorBudget(1e-9, 1e-9);
        var signed = new IndicatorErrorBudget(1e-9, 1e-9, requireSameSign: true);
        foreach (var tiny in new[] { double.Epsilon, -double.Epsilon, 1e-14, -1e-14 })
        {
            foreach (var pair in new[] { (tiny, 0d), (0d, tiny), (tiny, -tiny) })
            {
                Assert.True(ordinary.Accepts(pair.Item1, pair.Item2));
                Assert.False(signed.Accepts(pair.Item1, pair.Item2));
            }
            Assert.True(signed.Accepts(tiny, tiny * 2));
        }
        var report = await IndicatorValidation.ValidateAsync(new(typeof(Probe), "erased-signal", () => new Probe(2)));
        Assert.Contains(report.Failures, f => f.Rule == "Reference[0]");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BatchRejectsFutureBenchmarkUnequalLengthsDuplicatesAndReverseTime(int defect)
    {
        var dates = new[] { DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1) };
        var marketDates = defect == 0 ? dates.Select(t => t.AddDays(1)).ToArray() : dates;
        if (defect == 2) dates = marketDates = new[] { DateTime.UnixEpoch, DateTime.UnixEpoch };
        if (defect == 3) dates = marketDates = dates.Reverse().ToArray();
        var primary = Stock(new[] { 100d, 110 }, dates);
        var benchmark = defect == 1 ? Stock(new[] { 100d }, marketDates.Take(1).ToArray())
            : Stock(new[] { 100d, 200 }, marketDates);
        Assert.Throws<ArgumentException>(() => Evaluate(primary, benchmark));
    }

    [Fact]
    public void AlignedBenchmarkRetainsItsFormula()
    {
        var dates = new[] { DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1) };
        var result = Evaluate(Stock(new[] { 100d, 110 }, dates), Stock(new[] { 100d, 200 }, dates));
        Assert.Equal(2, result.Length);
        Assert.Equal(100 * Math.Log(110d / 200), result[1], 10);
    }

    [Fact]
    public void ErrorBudgetDoesNotOverflowIntoAcceptingOppositeExtremes()
    {
        var budget = new IndicatorErrorBudget(1, 1e-9);
        Assert.False(budget.Accepts(double.MaxValue, -double.MaxValue));
        Assert.False(budget.Accepts(double.NaN, double.NaN));
        Assert.False(budget.Accepts(double.PositiveInfinity, double.PositiveInfinity));
        Assert.False(IndicatorErrorBudget.Exact.Accepts(double.Epsilon, 0));
        Assert.True(IndicatorErrorBudget.Exact.Accepts(double.MaxValue, double.MaxValue));
    }

    [Theory]
    [InlineData(IndicatorStartupPolicy.NaN, true)]
    [InlineData(IndicatorStartupPolicy.Finite, false)]
    public async Task StartupNaNRequiresAnExplicitPolicy(IndicatorStartupPolicy policy, bool valid)
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(UnavailableStartup), "startup", () => new UnavailableStartup(policy)));
        Assert.Equal(valid, report.IsValid);
    }

    private static StockData Stock(double[] values, DateTime[] times) => new(values.ToList(), values.ToList(),
        values.ToList(), values.ToList(), values.Select(_ => 1d).ToList(), times.ToList());

    private static double[] Evaluate(StockData primary, StockData benchmark)
    {
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(primary));
        builder.AddDataSource("market", IndicatorDataSource.FromBatch(benchmark));
        SeriesHandle handle = default;
        builder.ConfigureIndicators(c => handle = c.RSMKIndicator(c.Price("market"), 1, 1));
        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle);
        return runtime.GetSeries(handle).ToArray();
    }

    public sealed class Probe(int mode) : IndicatorBase, IIndicatorValidationContract
    {
        public override int WarmupBars => mode == 1 ? 5 : 0;
        public IEnumerable<IndicatorValidationRule> ValidationRules => mode == 0 ? Array.Empty<IndicatorValidationRule>()
            : new[] { IndicatorValidationRule.Reference(0, bars => bars.Select(b => mode == 1 ? b.Close : (b.Close - 100) * 1e-14).ToArray(),
                new IndicatorErrorBudget(1e-9, 1e-9, requireSameSign: mode == 2), includeWarmup: false) };
        protected internal override object CreateState() => new State(mode);
        private sealed class State(int mode) : IIndicatorState
        {
            private int _count;
            public void Reset() => _count = 0;
            public double Update(in Bar bar) => mode == 1 ? ++_count <= 5 ? double.PositiveInfinity : bar.Close : 0;
        }
    }

    public sealed class UnavailableStartup(IndicatorStartupPolicy policy) : IndicatorBase, IIndicatorStartupContract, IIndicatorValidationContract
    {
        public override int WarmupBars => 2;
        public IndicatorStartupPolicy StartupPolicy(int outputSlot) => policy;
        public IEnumerable<IndicatorValidationRule> ValidationRules => new[]
        { IndicatorValidationRule.Reference(0, bars => bars.Select((b, i) => i < 2 ? double.NaN : b.Close).ToArray(), 0, 0) };
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            private int _count;
            public void Reset() => _count = 0;
            public double Update(in Bar bar) => ++_count <= 2 ? double.NaN : bar.Close;
        }
    }
}
