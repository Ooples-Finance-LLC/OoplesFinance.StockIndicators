using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WilderWordEmaReuseTests
{
    public static IEnumerable<object[]> Settings =>
        from period in new[] { 1, 2, 20, 1024, 1025, int.MaxValue }
        from averageSeed in new[] { false, true }
        select new object[] { period, averageSeed };

    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((price, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1)).ToArray();

    private static double[] Ordinary(int count) => Enumerable.Range(0, count)
        .Select(i => 100 + (i * 37 % 257) / 16d).ToArray();

    private static double[] Extreme(int count)
    {
        double[] pattern = [double.MaxValue, -double.MaxValue, double.Epsilon,
            -double.Epsilon, 0d, -0d, Math.ScaleB(1d, -1022), -Math.ScaleB(1d, -1022)];
        return Enumerable.Range(0, count).Select(i => pattern[i % pattern.Length]).ToArray();
    }

    private static double[] Mixed(int count)
    {
        double[] pattern = [1d, -1d, 0d, -0d, Math.BitIncrement(1d),
            Math.BitDecrement(-1d), Math.ScaleB(1d, -700), Math.ScaleB(-1d, 700)];
        return Enumerable.Range(0, count).Select(i => pattern[i % pattern.Length]).ToArray();
    }

    private static void AssertBits(IEnumerable<double> expected, IEnumerable<double> actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));

    [Theory, MemberData(nameof(Settings))]
    public void BothSeedsAndPeriodBoundsMatchIndependentFractions(int period, bool averageSeed)
    {
        // Unsupported maximum periods must stay lazy; 1024/1025 both reach
        // post-seed updates so the opt-in boundary is actually exercised.
        int count = period == int.MaxValue ? 65 : Math.Max(65, period + 33);
        var owner = new WilderMovingAverage(period, averageSeed);
        foreach (var prices in new[] { Ordinary(count), Extreme(count), Mixed(count) })
        {
            var bars = BarsOf(prices);
            // Reference uses ReferenceFraction throughout, independently of
            // both the reused word certificate and the original accumulator.
            var expected = owner.Reference(bars);
            var state = (IIndicatorState)owner.CreateState();
            var actual = bars.Select(bar => state.Update(in bar)).ToArray();
            AssertBits(expected, actual);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetAndSplitContinuationRetainSeedAndRoundedState(bool averageSeed)
    {
        var owner = new WilderMovingAverage(20, averageSeed);
        var state = (IIndicatorState)owner.CreateState();
        foreach (var prices in new[] { Ordinary(257), Extreme(257), Mixed(257), Ordinary(257) })
        {
            var bars = BarsOf(prices);
            var expected = owner.Reference(bars);
            foreach (int split in new[] { 0, 1, 19, 20, 21, 128, 257 })
            {
                state.Reset();
                var actual = new List<double>();
                foreach (var bar in bars.Take(split)) actual.Add(state.Update(in bar));
                foreach (var bar in bars.Skip(split)) actual.Add(state.Update(in bar));
                AssertBits(expected, actual);
            }
        }
    }

    [Fact]
    public void FirstValueSignedZeroAndPostSeedCanonicalZeroStayDistinct()
    {
        var bars = BarsOf([-0d, -0d, 0d]);
        var first = (IIndicatorState)new WilderMovingAverage(20, false).CreateState();
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(first.Update(in bars[0])));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(first.Update(in bars[1])));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(first.Update(in bars[2])));
        var mean = (IIndicatorState)new WilderMovingAverage(1).CreateState();
        foreach (var bar in bars)
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(mean.Update(in bar)));
    }

    [Fact]
    public void ReusedCertificateAcceptsOrdinaryUpdatesAndRejectsExtendedCases()
    {
        var recurrence = new TsiWordEmaRecurrence(39);
        Assert.True(recurrence.TryAdvance(100, 101, out var rounded));
        var expected = (ReferenceFraction.FromDouble(100) * new ReferenceFraction(19)
            + ReferenceFraction.FromDouble(101)) / new ReferenceFraction(20);
        AssertBits([expected.ToDouble()], [rounded]);
        Assert.True(recurrence.TryAdvance(1, -19, out rounded));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(rounded));
        Assert.False(recurrence.TryAdvance(double.Epsilon, 1, out _));
        Assert.False(recurrence.TryAdvance(1, Math.ScaleB(1d, 100), out _));
        Assert.False(recurrence.TryAdvance(1, Math.ScaleB(1d, 40), out _));
    }

    [Fact]
    public void RejectedSubnormalUpdateCanReturnToTheWordPath()
    {
        var owner = new WilderMovingAverage(2, false);
        var bars = BarsOf([100, double.Epsilon, 100, 101]);
        var expected = owner.Reference(bars);
        var recurrence = new TsiWordEmaRecurrence(3);
        Assert.False(recurrence.TryAdvance(100, double.Epsilon, out _));
        Assert.True(recurrence.TryAdvance(expected[1], 100, out var recovered));
        AssertBits([expected[2]], [recovered]);
        var state = (IIndicatorState)owner.CreateState();
        AssertBits(expected, bars.Select(bar => state.Update(in bar)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalAlligatorAndGatorStatesMatchUnchangedCapturedStates(bool gator)
    {
        var alligator = new AlligatorWithDetails(20, 8, 8, 5, 2, 3);
        MultiOutputIndicatorBase owner = gator ? new GatorWithDetails(20, 8, 8, 5, 2, 3) : alligator;
        var original = (IMultiOutputState)owner.CreateState();
        IMultiOutputState captured = new ValuesBarExecution.AlligatorState(alligator, 257);
        if (gator) captured = new GatorWithDetails.State(captured);
        foreach (var prices in new[] { Ordinary(257), Extreme(257), Mixed(257), Ordinary(257) })
        {
            original.Reset(); captured.Reset();
            var expected = new double[owner.Outputs.Count];
            var actual = new double[expected.Length];
            foreach (var bar in BarsOf(prices))
            {
                Array.Fill(expected, 17); Array.Fill(actual, 29);
                captured.Update(in bar, expected);
                original.Update(in bar, actual);
                AssertBits(expected, actual);
            }
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(1024)]
    [InlineData(1025)]
    public void SmmaEnvelopeOriginalStateMatchesIndependentCenterAndBands(int period)
    {
        var owner = new WindowAverageEnvelope(period, 2.5, EnvelopeAverage.Smma);
        var state = (IMultiOutputState)owner.CreateState();
        var values = new double[4];
        foreach (double scale in new[] { 1d, -1d, Math.ScaleB(1d, -1000) })
        {
            state.Reset();
            var bars = BarsOf(Ordinary(period + 33).Select(value => value * scale));
            var center = new WilderMovingAverage(period).Reference(bars);
            for (int i = 0; i < bars.Length; i++)
            {
                Array.Fill(values, 19);
                state.Update(in bars[i], values);
                if (i < period - 1) AssertBits([0, 0, 0, 0], values);
                else
                {
                    var exact = ReferenceFraction.FromDouble(center[i]);
                    double upper = (exact * ReferenceFraction.FromDouble(102.5) / new ReferenceFraction(100)).ToDouble();
                    double lower = (exact * ReferenceFraction.FromDouble(97.5) / new ReferenceFraction(100)).ToDouble();
                    AssertBits([center[i], upper, lower, 1], values);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedExecutionRetainsHistoryValidationAndCancellation(bool averageSeed)
    {
        var bars = BarsOf(Ordinary(257));
        var owner = new WilderMovingAverage(20, averageSeed);
        var expected = owner.Reference(bars);
        var retainedBars = bars.ToArray();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        using var run = await builder.BuildAsync();
        AssertBits(expected, run[owner.Outputs[0]].ToArray());
        Array.Clear(bars);
        AssertBits(expected, run[owner.Outputs[0]].ToArray());
        Assert.Equal(retainedBars[^1], run.Latest.Bar);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));

        bars = BarsOf(Ordinary(257));
        var last = bars[^1]; bars[^1] = new(last.Time, last.Open, last.High, last.Low, last.Close, double.NaN);
        StockIndicatorBuilder Invalid() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        var originalError = await Record.ExceptionAsync(() => Invalid().ConfigureBehavior(_ => { }).BuildAsync());
        var capturedError = await Record.ExceptionAsync(() => Invalid().BuildAsync());
        Assert.NotNull(originalError); Assert.NotNull(capturedError);
        Assert.Equal(originalError.GetType(), capturedError.GetType());
        Assert.Equal(originalError.Message, capturedError.Message);
    }
}
