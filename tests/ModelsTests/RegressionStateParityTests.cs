using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RegressionStateParityTests
{
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)]
    public void CompactMomentsMatchPreviousMinimumUnitArithmeticAcrossResets(int period)
    {
        var random = new Random(5191);
        var inputs = new[]
        {
            Enumerable.Range(0, 90).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 90).Select(_ => random.NextDouble() * 200 - 100).ToArray(),
            new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0d, -0d, 1d, -1d },
            new[] { 0d, double.Epsilon, 4 * double.Epsilon, 0d, 0d, 0d, 4 * double.Epsilon },
            Enumerable.Repeat(double.MaxValue, 25).ToArray(),
            Enumerable.Range(0, 90).Select(i => i < 25 ? Math.ScaleB(1 + i / 128d, 700)
                : i < 50 ? Math.ScaleB(1 + i / 128d, -700) : i % 7 / 8d).ToArray()
        };
        using var compact = new WindowRegressionStatistics.State(period);
        using var legacy = new LegacyState(period);
        var actual = new double[10];
        var expected = new double[10];
        foreach (var values in inputs)
        {
            compact.Reset(); legacy.Reset();
            for (int i = 0; i < values.Length; i++)
            {
                var bar = new Bar(default, 0, 0, 0, values[i], 0);
                compact.Update(bar, actual); legacy.Update(bar, expected);
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
                EqualBits(legacy.GlobalIntercept, compact.GlobalIntercept);
                EqualBits(legacy.LineAt(0), compact.LineAt(0));
                EqualBits(legacy.LineAt(Math.Min(period, i + 1) - 1), compact.LineAt(Math.Min(period, i + 1) - 1));
            }
        }
    }

    [Fact]
    public void WarmedOrdinaryStateUpdatesAndFinalLineAllocateNothing()
    {
        using var state = new WindowRegressionStatistics.State(20);
        var bars = Enumerable.Range(0, 1000).Select(i =>
            new Bar(default, 0, 0, 0, 100 + (i * 13 % 101) / 8d, 0)).ToArray();
        var outputs = new double[10];
        double Replay()
        {
            foreach (var bar in bars) state.Update(bar, outputs);
            double value = state.GlobalIntercept;
            for (int i = 0; i < 20; i++) value += state.LineAt(i);
            return value;
        }
        for (int i = 0; i < 10; i++) { state.Reset(); Replay(); }
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        double result = Replay();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(double.IsFinite(result));
    }

    private static void EqualBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    // Frozen pre-optimization state: minimum-subnormal-unit BigInteger moments
    // and a separate exact fit window provide a bitwise differential reference.
    private sealed class LegacyState(int period) : IMultiOutputState, IDisposable
    {
        private readonly ExactLinearFitWindow _fit = new(period, observedHistory: true);
        private readonly Queue<double> _history = new();
        private BigInteger _sum,
            _squares,
            _weighted;
        private ExactLinearFitWindow.Fit _latest;
        internal double GlobalIntercept => _latest.OneBasedGlobalIntercept;

        internal double LineAt(int position) => _latest.WindowPosition(position);

        public void Reset()
        {
            _fit.Reset();
            _history.Clear();
            _sum = _squares = _weighted = 0;
            _latest = default;
        }

        public void Dispose()
        {
            _fit.Dispose();
            _history.Clear();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            output[5] = 1;
            var value = ExactVarianceWindow.Units(bar.Close);
            if (_history.Count == period)
            {
                var expired = ExactVarianceWindow.Units(_history.Dequeue());
                _weighted -= _sum - expired;
                _sum -= expired;
                _squares -= expired * expired;
            }
            _weighted += _history.Count * value;
            _sum += value;
            _squares += value * value;
            _history.Enqueue(bar.Close);
            var fit = _fit.Next(bar.Close, true);
            _latest = fit;
            if (_history.Count < 2)
                return;
            var n = new BigInteger(_history.Count);
            var variance = n * _squares - _sum * _sum;
            output[0] = fit.Slope;
            output[1] = fit.OneBasedWindowIntercept;
            output[2] = ExactPopulationDeviation.RootRatio(variance, n * n);
            output[4] = fit.Last;
            output[6] = output[7] = output[9] = 1;
            if (variance.IsZero)
                return;
            var covariance = 2 * _weighted - (n - 1) * _sum;
            output[3] = ExactMeanAccumulator.UnitRatio(
                (3 * covariance * covariance) << 1074,
                (n * n - 1) * variance
            );
            output[8] = 1;
        }
    }
}
