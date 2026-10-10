using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactPairStatisticsTests
{
    private static IEnumerable<(double[] Left, double[] Right)> Fixtures()
    {
        yield return (Enumerable.Range(0, 41).Select(i => 10 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 41).Select(i => 8 + (i * 7 % 97) / 16d).ToArray());
        double[] extremes = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, -0d, 1, -1, 2, 3, 4, 5, 6, 7, 8];
        yield return (extremes, extremes.Reverse().ToArray());
        double[] adjacent = [1, Math.BitIncrement(1d), Math.BitDecrement(1d), 1, 1, Math.BitIncrement(1d), 1];
        yield return (adjacent, adjacent.Select(v => -v).ToArray());
        yield return (new double[19], Enumerable.Repeat(1d, 19).ToArray());
        double bound = (1L << 30) / 7;
        yield return ([0, 1, bound - 1, bound, -bound, bound + 1, 0, double.Epsilon, 2, 3, 4, 5, 6, 7, 8],
            [1, 0, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6, -6, 7, -7]);
        yield return (Enumerable.Range(0, 27).Select(i => Math.ScaleB((i % 7 - 3) * 1.23456789012345, i * 73 - 1000)).ToArray(),
            Enumerable.Range(0, 27).Select(i => Math.ScaleB((i % 5 - 2) * 1.125, 900 - i * 70)).ToArray());
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(int.MaxValue)]
    public void PairMomentsMatchIndependentWindowsAcrossGridChangesEvictionAndReset(int period)
    {
        var window = new PairStatisticsWindow(period);
        foreach (var (left, right) in Fixtures())
        {
            window.Reset();
            for (var i = 0; i < left.Length; i++)
            {
                window.Add(left[i], right[i]);
                var start = Math.Max(0, i - period + 1);
                var values = Enumerable.Range(start, i - start + 1)
                    .Select(j => (X: ReferenceFraction.FromDouble(left[j]), Y: ReferenceFraction.FromDouble(right[j]))).ToArray();
                var (a, b, c) = Moments(values);
                var n = new ReferenceFraction(values.Length);
                foreach (var flatZero in new[] { false, true })
                for (var slot = 0; slot < 5; slot++)
                {
                    double? expected = null;
                    if (values.Length == period)
                    {
                        if (slot >= 2) expected = ((slot == 2 ? c : slot == 3 ? a : b) / (n * n)).ToDouble();
                        else if (a.Sign == 0 || b.Sign == 0) expected = flatZero ? 0 : null;
                        else
                        {
                            var squared = c * c / (a * b);
                            expected = slot == 0 ? c.Sign * squared.SqrtToDouble() : squared.ToDouble();
                        }
                    }
                    Bits(expected, window.Read(slot, flatZero));
                }
            }
        }
    }

    public static IEnumerable<object[]> BetaCases =>
        from period in new[] { 1, 3, 7, int.MaxValue }
        from selection in Enum.GetValues<ReturnBetaSelection>()
        select new object[] { period, selection };

    [Theory, MemberData(nameof(BetaCases))]
    public void ReturnMomentsPreserveExtendedReturnsSubsetsAndExactPublishedRatios(int period, ReturnBetaSelection selection)
    {
        var window = new ReturnBetaWindow(period, selection);
        var unit = new ReferenceFraction(BigInteger.One << 1074);
        foreach (var (left, right) in Fixtures())
        {
            window.Reset();
            var returns = new List<(ReferenceFraction X, ReferenceFraction Y)>();
            for (var i = 0; i < left.Length; i++)
            {
                window.Add(left[i], right[i]);
                var x = Return(left[i], i == 0 ? 0 : left[i - 1]);
                var y = Return(right[i], i == 0 ? 0 : right[i - 1]);
                Assert.Equal(0, x.CompareTo(new ReferenceFraction(window.MarketReturn) / unit));
                Assert.Equal(0, y.CompareTo(new ReferenceFraction(window.EvaluationReturn) / unit));
                if (i != 0) returns.Add((x, y));
                if (returns.Count > period) returns.RemoveAt(0);
                foreach (var flatZero in new[] { false, true })
                for (var slot = 0; slot < 3; slot++)
                {
                    double? expected = null;
                    if (returns.Count == period && (selection == ReturnBetaSelection.All || (int)selection == slot))
                    {
                        var selected = returns.Where(v => slot == 0 || (slot == 1 ? v.X.Sign > 0 : v.X.Sign < 0)).ToArray();
                        var (a, _, c) = Moments(selected);
                        expected = a.Sign == 0 ? flatZero ? 0 : null : (c / a).ToDouble();
                    }
                    Bits(expected, window.Beta(slot, flatZero));
                }
            }
        }
    }

    [Fact]
    public void CertifiedPairMomentsAndCorrelationPublishWithoutPerBarAllocations()
    {
        var window = new PairStatisticsWindow(20);
        for (int i = 0; i < 100; i++)
        {
            window.Add(10 + (i * 13 % 101) / 8d, 8 + (i * 7 % 97) / 16d);
            _ = window.Read(0, true);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            window.Add(10 + (i * 13 % 101) / 8d, 8 + (i * 7 % 97) / 16d);
            sum += window.Read(0, true)!.Value;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(sum));
    }

    [Fact]
    public void CompactReturnsMatchIndependentRoundingAcrossSignsAndFullExponentRange()
    {
        var random = new Random(81143);
        var window = new ReturnBetaWindow(2, ReturnBetaSelection.All);
        var unit = new ReferenceFraction(BigInteger.One << 1074);
        double[] boundaries = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            1, -1, Math.BitIncrement(1d), Math.BitDecrement(1d), Math.ScaleB(1, -1022)];
        var pairs = from previous in boundaries from current in boundaries select (previous, current);
        pairs = pairs.Concat(Enumerable.Range(0, 300).Select(i =>
        {
            double previous = Math.ScaleB((random.NextDouble() + 1) * (i % 2 == 0 ? 1 : -1), random.Next(-1074, 1024));
            double current = i % 3 == 0 ? Math.BitIncrement(previous)
                : Math.ScaleB((random.NextDouble() + 1) * (i % 5 == 0 ? -1 : 1), random.Next(-1074, 1024));
            return (previous, current);
        }));
        foreach (var (previous, current) in pairs)
        {
            window.Reset();
            window.Add(previous, current);
            window.Add(current, previous);
            var market = Return(current, previous);
            var evaluation = Return(previous, current);
            Assert.Equal(0, market.CompareTo(new ReferenceFraction(window.MarketReturn) / unit));
            Assert.Equal(0, evaluation.CompareTo(new ReferenceFraction(window.EvaluationReturn) / unit));
            Bits(market.ToDouble(), window.MarketReturnValue);
            Bits(evaluation.ToDouble(), window.EvaluationReturnValue);
        }
    }

    [Fact]
    public void WideTrailingZeroNormalizationPreservesBothSigns()
    {
        foreach (var shift in new[] { 0, 1, 31, 63, 64, 1074, 2148, 4096 })
        foreach (var sign in new[] { -1, 1 })
        {
            var value = sign * ((BigInteger.One << 80) + 3) << shift;
            Assert.Equal(shift, ExactMeanAccumulator.TrailingBinaryZeros(value));
        }
    }

    public static IEnumerable<object[]> Contracts => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(OoplesFinance.StockIndicators.Indicators.WindowCorrelation) || c.IndicatorType == typeof(WindowPairStatistics)
            || c.IndicatorType == typeof(WindowReturnBeta) || c.IndicatorType == typeof(WindowBetaStatistics))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Contracts))]
    public async Task SharedConsumersKeepTheirIndependentNumericalContracts(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase, new IndicatorValidationOptions { BarsPerFixture = 32 });
        report.ThrowIfInvalid();
    }

    private static ReferenceFraction Return(double current, double previous) =>
        ReferenceFraction.FromDouble(previous).Sign == 0 ? new ReferenceFraction(0)
            : ((ReferenceFraction.FromDouble(current) - ReferenceFraction.FromDouble(previous))
                / ReferenceFraction.FromDouble(previous)).RoundExtendedBinary64();

    private static (ReferenceFraction A, ReferenceFraction B, ReferenceFraction C) Moments(
        IReadOnlyList<(ReferenceFraction X, ReferenceFraction Y)> values)
    {
        var zero = new ReferenceFraction(0);
        if (values.Count == 0) return (zero, zero, zero);
        var n = new ReferenceFraction(values.Count);
        var meanX = values.Aggregate(zero, (sum, v) => sum + v.X) / n;
        var meanY = values.Aggregate(zero, (sum, v) => sum + v.Y) / n;
        var a = zero; var b = zero; var c = zero;
        foreach (var value in values)
        {
            var x = value.X - meanX;
            var y = value.Y - meanY;
            a += x * x; b += y * y; c += x * y;
        }
        return (n * a, n * b, n * c);
    }

    private static void Bits(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
    }
}
