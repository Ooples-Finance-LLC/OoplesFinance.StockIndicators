using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KarobeinSignalTests
{
    public static IEnumerable<object[]> Cases =>
        from length in new[] { 1, 2, 3, 14 }
        from prices in new double[][]
        {
            [1, 2, 1, 1, 0, 2, 2, 1],
            [2, 2, 2, 2, 2, 2, 2, 2],
            [-8, 2, -5, 9, 0, 4],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 1, 1],
            [double.Epsilon, double.MaxValue, 1, double.MaxValue / 2, double.Epsilon, 2]
        }
        select new object[] { length, prices };

    [Theory, MemberData(nameof(Cases))]
    public void ExactSignalsSurvivePreviewsAndReset(int length, double[] prices)
    {
        var expected = Reference(prices, length);
        using var window = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, length);
        for (var replay = 0; replay < 2; replay++)
        {
            var committed = new List<double>();
            for (var i = 0; i < prices.Length; i++)
            {
                var speculative = Reference(committed.Append(3d).ToArray(), length)[^1];
                Assert.Equal(speculative, window.Next(3, false));
                foreach (var final in new[] { false, false, true })
                    Assert.Equal(expected[i], window.Next(prices[i], final));
                committed.Add(prices[i]);
            }
            window.Reset();
        }
        var data = new StockData(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length),
            Enumerable.Range(0, prices.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
        data.CalculateKarobeinOscillator(MovingAvgType.ExponentialMovingAverage, length);
        Assert.Equal(expected.Select(p => p.Line), data.OutputValues["Ko"]);
        Assert.Equal(expected.Select(p => p.Trade), data.SignalsList);
    }

    [Theory]
    [InlineData(.75, .25, 0, Signal.StrongBuy)]
    [InlineData(.75, .5, 0, Signal.Buy)]
    [InlineData(0, .5, .75, Signal.StrongSell)]
    [InlineData(0, .25, .75, Signal.Sell)]
    public void SeparatedBoundsCertifyTheExpectedSignal(double line, double previous, double older, Signal expected)
    {
        Assert.True(KarobeinWindow.TryCertifiedTrade(line, previous, older, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExactAndRoundedTiesKeepTheRationalFallback()
    {
        Assert.False(KarobeinWindow.TryCertifiedTrade(.5, .5, .5, out _));
        Assert.False(KarobeinWindow.TryCertifiedTrade(.75, .5, .25, out _));
        Assert.False(KarobeinWindow.TryCertifiedTrade(0, 0, 0, out _));
        // Distinct exact values can publish the same binary64 value.
        var half = new ReferenceFraction(1) / new ReferenceFraction(2);
        var tiny = new ReferenceFraction(1) / new ReferenceFraction(System.Numerics.BigInteger.One << 1075);
        Assert.Equal(half.ToDouble(), (half + tiny).ToDouble());
        Assert.False(KarobeinWindow.TryCertifiedTrade((half + tiny).ToDouble(), half.ToDouble(), (half - tiny).ToDouble(), out _));
    }

    [Fact]
    public void EveryCertifiedRationalTripleHasTheExactSignal()
    {
        var random = new Random(82371); var certified = 0;
        ReferenceFraction Sample()
        {
            var denominator = random.NextInt64(2, 1L << 52);
            return new ReferenceFraction(random.NextInt64(denominator + 1)) / new ReferenceFraction(denominator);
        }
        for (var i = 0; i < 1000; i++)
        {
            var line = Sample(); var previous = Sample(); var older = Sample();
            var slope = line - previous; var acceleration = slope - (previous - older);
            var expected = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
            if (!KarobeinWindow.TryCertifiedTrade(line.ToDouble(), previous.ToDouble(), older.ToDouble(), out var actual)) continue;
            certified++; Assert.Equal(expected, actual);
        }
        Assert.InRange(certified, 900, 1000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchMeansMatchExactReferenceAcrossLongerHistory(bool callbacks)
    {
        var prices = Enumerable.Range(0, 96).Select(i => 10d + (i * 13 % 101) / 8d).ToArray();
        var expected = Reference(prices, 14);
        var data = new StockData(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length),
            Enumerable.Range(0, prices.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
        var actual = KarobeinWindow.Calculate(data, MovingAvgType.ExponentialMovingAverage, 14, callbacks);
        Assert.Equal(expected.Select(p => p.Line), actual.Values);
        Assert.Equal(expected.Select(p => p.Trade), actual.Signals);
    }

    [Fact]
    public void SubstitutedMeansKeepEveryFoldBranchAndExactPole()
    {
        foreach (var ratio in new[] { -2d, -1, 0, 1, 2 })
        foreach (var fall in new[] { -2d, -1, 0, 1, 2 })
        foreach (var rise in new[] { -2d, -1, 0, 1, 2 })
        {
            var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
            ReferenceFraction Clip(ReferenceFraction v) => v.Sign < 0 ? zero : v.CompareTo(one) > 0 ? one : v;
            double Expected()
            {
                var r = ReferenceFraction.FromDouble(ratio);
                if (r.Sign == 0) return 0;
                var first = r + ReferenceFraction.FromDouble(rise);
                if (first.Sign == 0) throw new ArgumentException("rising-ratio");
                var c = Clip(r / first);
                var second = r + c * ReferenceFraction.FromDouble(fall);
                if (second.Sign == 0) throw new ArgumentException("falling-ratio");
                return Clip(new ReferenceFraction(2) * r / second - one).ToDouble();
            }
            var expected = 0d; var expectedError = Record.Exception(() => expected = Expected());
            var calls = 0; double[][] means = [[1, ratio], [0, fall], [0, rise]];
            IReadOnlyList<double> Callback(IReadOnlyList<double> values, int period) => means[calls++];
            var prices = new[] { 1d, 2d };
            var data = new StockData(prices, prices, prices, prices, new[] { 1d, 1d },
                new[] { DateTime.UnixEpoch, DateTime.UnixEpoch.AddMinutes(1) });
            (double[] Values, Signal[] Signals) actual = default;
            using (OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.Arm(
                new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { Callback, Callback, Callback }))
            {
                var actualError = Record.Exception(() => actual = KarobeinWindow.Calculate(data, MovingAvgType.ExponentialMovingAverage, 2, true));
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                if (expectedError is not null) Assert.Contains(expectedError.Message, actualError!.Message);
            }
            Assert.Equal(3, calls);
            if (expectedError is not null) continue;
            Assert.Equal(new[] { 0d, expected }, actual.Values);
            Assert.Equal(new[] { Signal.None, expected > 0 ? Signal.StrongBuy : Signal.None }, actual.Signals);
        }
    }

    private static (double Line, Signal Trade)[] Reference(double[] prices, int length)
    {
        ReferenceFraction R(long n) => new(n);
        ReferenceFraction Clip(ReferenceFraction v) => v.Sign < 0 ? R(0) : v.CompareTo(R(1)) > 0 ? R(1) : v;
        ReferenceFraction[] Mean(ReferenceFraction[] values)
        {
            var result = new ReferenceFraction[values.Length];
            var previous = R(0); var seed = R(0); var alpha = R(2) / R(length + 1L);
            for (var i = 0; i < values.Length; i++)
            {
                if (i < length) { seed += values[i]; previous = seed / R(i + 1L); }
                else previous += alpha * (values[i] - previous);
                result[i] = previous;
            }
            return result;
        }
        var mean = Mean(prices.Select(ReferenceFraction.FromDouble).ToArray());
        var ratios = mean.Select((v, i) => i == 0 || mean[i - 1].Sign == 0 ? R(0) : v / mean[i - 1]).ToArray();
        var falls = Mean(ratios.Select((v, i) => i > 0 && mean[i].CompareTo(mean[i - 1]) < 0 ? v : R(0)).ToArray());
        var rises = Mean(ratios.Select((v, i) => i > 0 && mean[i].CompareTo(mean[i - 1]) > 0 ? v : R(0)).ToArray());
        var output = new (double Line, Signal Trade)[prices.Length];
        var previousLine = R(0); var previousSlope = R(0);
        for (var i = 0; i < prices.Length; i++)
        {
            var line = R(0);
            if (ratios[i].Sign != 0)
            {
                var correction = Clip(ratios[i] / (ratios[i] + rises[i])) * falls[i];
                line = Clip((ratios[i] - correction) / (ratios[i] + correction));
            }
            var slope = line - previousLine; var acceleration = slope - previousSlope;
            var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
            output[i] = (line.ToDouble(), trade);
            previousLine = line; previousSlope = slope;
        }
        return output;
    }
}
