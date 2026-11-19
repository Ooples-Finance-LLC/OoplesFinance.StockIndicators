using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RenkoConstantAtrTests
{
    [Theory]
    [InlineData(1)] [InlineData(20)] [InlineData(64)] [InlineData(65)]
    public void WarmupCountsAndValidationPassesMatchOriginal(int period)
    {
        foreach (int count in new[] { period - 1, period, period + 1 })
        foreach (bool highLow in new[] { false, true })
        {
            var before = new ObservedBars(Constant(count));
            var after = new ObservedBars(Constant(count));
            var expected = OriginalAtr(before, period, highLow);
            var actual = RenkoSnapshot.CalculateAtr(after, period, highLow);
            Assert.Equal(before.Reads, after.Reads);
            AssertBricks(expected, actual);
            Assert.Equal(before.Reads, after.Reads);
            Assert.Equal(count > period ? 3 : 2, after.Passes);
        }
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(1, 1)]
    [InlineData(20, 0)] [InlineData(20, 1)] [InlineData(20, 19)]
    [InlineData(20, 20)] [InlineData(20, 21)] [InlineData(20, 200)]
    [InlineData(64, 63)] [InlineData(64, 64)] [InlineData(65, 65)]
    public void FirstChangedSpanOrGapRestoresOriginalContinuation(int period, int prefixRanges)
    {
        foreach (bool gap in new[] { false, true })
        foreach (bool highLow in new[] { false, true })
        {
            var bars = Constant(prefixRanges + 1).ToList();
            double previous = bars[^1].Close;
            bars.Add(gap ? Candle(bars.Count, previous + 5, previous + 3, previous + 4)
                : Candle(bars.Count, previous + 3, previous, previous + 3));
            for (int i = 0; i < period + 4; i++)
            {
                previous = bars[^1].Close;
                bars.Add(Candle(bars.Count, previous + 2, previous, previous + 2));
            }
            // The real previous close is far from synthetic zero. A missing
            // restoration of it changes the gap, seed and every later ATR.
            var input = bars.ToArray();
            AssertBricks(OriginalAtr(input, period, highLow), RenkoSnapshot.CalculateAtr(input, period, highLow));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void RoundedSubnormalExtremeAndUnsupportedSpansMatchOriginal(int kind)
    {
        double epsilon = double.Epsilon;
        var bars = Enumerable.Range(0, 9).Select(i => kind switch
        {
            0 => Candle(i, epsilon, 0, i == 8 ? epsilon : 0),
            1 => Candle(i, double.MaxValue, 0, i == 8 ? double.MaxValue : 0),
            2 => Candle(i, double.MaxValue, -double.MaxValue, 0),
            3 => Candle(i, 0, 0, i % 2 == 0 ? 0d : -0d),
            4 => Candle(i, 1, 2, 0),
            // Exact high-low is 1+epsilon, but TrueRange rounds it to 1
            // before smoothing. The certificate must reproduce that value.
            5 => Candle(i, 1, -epsilon, i == 8 ? 1 : 0),
            _ => Candle(i, 2 * epsilon, epsilon, epsilon)
        }).ToArray();
        foreach (int period in new[] { 1, 4 })
        foreach (bool highLow in new[] { false, true })
            CompareCallAndEnumeration(new ObservedBars(bars), new ObservedBars(bars), period, highLow);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)]
    public void CapturedAtrPassAndDeferredChartReadsMatchOriginal(int kind)
    {
        var bars = Constant(45);
        Bar Transform(int pass, int row, Bar bar)
        {
            if (pass == 2)
            {
                if (kind == 0 && row == 25)
                    return Candle(row, bar.Close + 5, bar.Close + 3, bar.Close + 4);
                if (kind == 1 && row == 25)
                    return new(bar.Time, bar.Open, double.NaN, bar.Low, bar.Close, bar.Volume);
                // ATR ignores all first-bar fields except its close.
                if (kind == 2 && row == 0)
                    return new(bar.Time, bar.Open, double.NaN, double.NaN, bar.Close, double.NaN);
                if (kind == 3 && row == 0)
                    return new(bar.Time, bar.Open, bar.High, bar.Low, double.NaN, bar.Volume);
                if (kind == 5 && row == 25)
                    return new(bar.Time, bar.Open, bar.High, bar.Low, double.NaN, bar.Volume);
            }
            if (kind == 4 && pass == 3 && row == 25)
                return new(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, double.NaN);
            return bar;
        }
        var before = new ObservedBars(bars.ToArray(), Transform);
        var after = new ObservedBars(bars.ToArray(), Transform);
        CompareCallAndEnumeration(before, after, 20, false, mutateAfterCall: true);
    }

    [Fact]
    public void InvalidPeriodStillFollowsEagerInputValidation()
    {
        foreach (bool invalidInput in new[] { false, true })
        {
            var bars = Constant(2);
            if (invalidInput) bars[0] = new(default, 0, 1, 0, 0, double.NaN);
            CompareCallAndEnumeration(new ObservedBars(bars), new ObservedBars(bars), 0, false);
        }
    }

    private static void CompareCallAndEnumeration(ObservedBars before, ObservedBars after,
        int period, bool highLow, bool mutateAfterCall = false)
    {
        IEnumerable<RenkoBrick>? expected = null, actual = null;
        var expectedError = Record.Exception(() => { expected = OriginalAtr(before, period, highLow); });
        var actualError = Record.Exception(() => { actual = RenkoSnapshot.CalculateAtr(after, period, highLow); });
        Assert.Equal(expectedError?.GetType(), actualError?.GetType());
        Assert.Equal((expectedError as ArgumentException)?.ParamName, (actualError as ArgumentException)?.ParamName);
        Assert.Equal(before.Reads, after.Reads);
        if (expectedError is not null) return;
        if (mutateAfterCall)
        {
            var changed = Candle(32, 173, 171, 172, 19);
            before.Replace(32, changed);
            after.Replace(32, changed);
        }
        AssertBricks(expected!, actual!);
        Assert.Equal(before.Reads, after.Reads);
    }

    // The pre-shortcut implementation is the oracle. It continues to use the
    // unchanged shared ATR state and the same normalized chart implementation.
    private static IEnumerable<RenkoBrick> OriginalAtr(IReadOnlyList<Bar> bars, int period, bool highLow)
    {
        foreach (var bar in bars)
            if (!double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                throw new ArgumentOutOfRangeException(nameof(bars));
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        var state = new SeededAtrWindow(period);
        foreach (var bar in bars) state.Add(bar);
        if (!state.HasAverage) return [];
        double size = state.Average.Publish();
        return size == 0 ? [] : RenkoSnapshot.Calculate(bars, size, highLow);
    }

    private static void AssertBricks(IEnumerable<RenkoBrick> expected, IEnumerable<RenkoBrick> actual)
    {
        static object Bits(RenkoBrick b) => (b.Date, BitConverter.DoubleToInt64Bits(b.Open),
            BitConverter.DoubleToInt64Bits(b.High), BitConverter.DoubleToInt64Bits(b.Low),
            BitConverter.DoubleToInt64Bits(b.Close), BitConverter.DoubleToInt64Bits(b.Volume), b.IsUp);
        Assert.Equal(expected.Select(Bits).ToArray(), actual.Select(Bits).ToArray());
    }

    private static Bar[] Constant(int count) => Enumerable.Range(0, count)
        .Select(i => Candle(i, 100 + 2 * i, 98 + 2 * i, 100 + 2 * i)).ToArray();

    private static Bar Candle(int row, double high, double low, double close, double volume = 3) =>
        new(DateTime.UnixEpoch.AddMinutes(row), close, high, low, close, volume);

    private sealed class ObservedBars(Bar[] bars, Func<int, int, Bar, Bar>? transform = null) : IReadOnlyList<Bar>
    {
        internal List<string> Reads { get; } = [];
        internal int Passes { get; private set; }
        public int Count { get { Reads.Add("Count"); return bars.Length; } }
        public Bar this[int index] { get { Reads.Add("Index:" + index); return bars[index]; } }
        internal void Replace(int row, Bar bar) => bars[row] = bar;
        public IEnumerator<Bar> GetEnumerator()
        {
            int pass = ++Passes;
            Reads.Add("Begin:" + pass);
            return Enumerate(pass).GetEnumerator();
        }
        private IEnumerable<Bar> Enumerate(int pass)
        {
            try
            {
                for (int row = 0; row < bars.Length; row++)
                {
                    Reads.Add(pass + ":" + row);
                    yield return transform is null ? bars[row] : transform(pass, row, bars[row]);
                }
            }
            finally { Reads.Add("End:" + pass); }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
