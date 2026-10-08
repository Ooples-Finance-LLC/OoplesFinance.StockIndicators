using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class FixedKernelSnapshotComparisonTests
{
    [Fact]
    public void FibonacciOrientationAndPrefixNormalizationHaveGoldens()
    {
        var data = CompetitorData.FromCloses([1, 2, 4, 8]);
        Assert.Equal(
            new[] { 1d, 5d / 3, 2.75, 5.5 },
            FibonacciWeightedSnapshot.Calculate(data.IndicatorBars, 3)
        );
        Assert.Equal(data.Closes, FibonacciWeightedSnapshot.Calculate(data.IndicatorBars, 1));
        ComparisonVerifier.Check(FixedKernelSnapshotComparison.Pair(true), data, 3);
    }

    [Fact]
    public void SincOldestFirstStartupRepeatsNewestValue()
    {
        var data = CompetitorData.FromCloses([1, 2, 4, 8]);
        Assert.Equal(
            new[] { 1d, 2, 2, 4 },
            WindowedSincSnapshot.Calculate(data.IndicatorBars, 3, 3)
        );
        Assert.Equal(
            data.Closes,
            WindowedSincSnapshot.Calculate(data.IndicatorBars, 3, 1, SincWindow.Rectangular)
        );
        ComparisonVerifier.Check(FixedKernelSnapshotComparison.Pair(false, 3), data, 3);
    }

    [Fact]
    public void InvalidAndUndefinedKernelsAreExplicit()
    {
        Assert.Throws<ArgumentNullException>(() => FibonacciWeightedSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FibonacciWeightedSnapshot.Calculate([], 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowedSincSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowedSincSnapshot.Calculate([], taps: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowedSincSnapshot.Calculate([], taps: 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowedSincSnapshot.Calculate([], taps: 2)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowedSincSnapshot.Calculate([], window: (SincWindow)5)
        );
        Assert.Empty(FibonacciWeightedSnapshot.Calculate([], int.MaxValue));
        Assert.Throws<IndexOutOfRangeException>(() => new QuanTAlib.Fwma(1));
        foreach (var taps in new[] { 1, 2 })
        {
            var native = new QuanTAlib.Afirma(3, taps, QuanTAlib.Afirma.WindowType.Hanning1);
            Assert.True(double.IsNaN(native.Calc(new QuanTAlib.TValue(1, true, false)).Value));
        }
    }

    [Fact]
    public void ExactFibonacciCoefficientsSurviveNativeOverflow()
    {
        var data = CompetitorData.FromCloses([1, 2, 4, 8, 16, 32]);
        Assert.Equal(
            FixedKernelSnapshotComparison.Reference(
                data.Closes,
                1500,
                true,
                21,
                SincWindow.Hann,
                false
            ),
            FibonacciWeightedSnapshot.Calculate(data.IndicatorBars, 1500)
        );
        var native = new QuanTAlib.Fwma(1500);
        Assert.Contains(
            data.Closes.Select(v => native.Calc(new QuanTAlib.TValue(v, true, false)).Value),
            v => !double.IsFinite(v)
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WideTinyAndSignedPricesMatchIndependentReferences(bool fibonacci)
    {
        foreach (var scale in new[] { double.MaxValue / 4, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable.Range(0, 20).Select(i => i % 3 == 0 ? -scale : scale).ToArray();
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 0))
                .ToArray();
            var actual = fibonacci
                ? FibonacciWeightedSnapshot.Calculate(bars, 5)
                : WindowedSincSnapshot.Calculate(bars, 5, 7, SincWindow.Hamming);
            Assert.Equal(
                FixedKernelSnapshotComparison.Reference(
                    prices,
                    5,
                    fibonacci,
                    7,
                    SincWindow.Hamming,
                    false
                ),
                actual
            );
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RepeatedCallsPrefixesAndCorruptionAreVerified(bool fibonacci)
    {
        var data = CompetitorData.Create(70);
        var pair = FixedKernelSnapshotComparison.Pair(fibonacci);
        var expected = pair.Ooples(data, 14);
        var changed = pair.Ooples(data, 14);
        ComparisonVerifier.Compare(expected, changed, "repeat", IndicatorErrorBudget.Exact);
        changed.Outputs["Value"].Values[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(expected, changed, "corruption", IndicatorErrorBudget.Exact)
        );
        var prefix = CompetitorData.FromCloses(data.Closes.Take(30).ToArray());
        Assert.Equal(
            expected.Outputs["Value"].Values.Take(30),
            pair.Ooples(prefix, 14).Outputs["Value"].Values
        );
    }
}
