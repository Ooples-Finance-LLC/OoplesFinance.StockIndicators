using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class OldestHilbertComparisonTests
{
    [Fact]
    public async Task IndependentReferenceCoversLifecycleAndSelectedSource()
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(OldestFirstHilbertTrendline),
                "oldest-first Hilbert",
                () => new OldestFirstHilbertTrendline()
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        ComparisonVerifier.Check(OldestHilbertComparison.Pair(), CompetitorData.Create(150), 20);
    }

    [Fact]
    public void FirstTenPricesAreUnfilteredAndFlatInputsRemainFlat()
    {
        var data = CompetitorData.FromCloses(
            Enumerable.Range(1, 40).Select(i => (double)i).ToArray()
        );
        var values = OldestHilbertComparison.Owned(data.IndicatorBars).Outputs["Value"].Values;
        Assert.Equal(data.Closes.Take(10), values.Take(10));
        Assert.True(values[10] < data.Closes[10]);
        Assert.Equal(OldestHilbertComparison.Reference(data.Closes), values);
        var flat = CompetitorData.FromCloses(Enumerable.Repeat(10d, 50).ToArray());
        Assert.All(
            OldestHilbertComparison.Owned(flat.IndicatorBars).Outputs["Value"].Values,
            v => Assert.Equal(10, v)
        );
    }

    [Fact]
    public void WideAndSubnormalMeansAvoidUnusedNativePhasorOverflow()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable.Range(0, 90).Select(i => i % 3 == 0 ? -scale : scale).ToArray();
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 0))
                .ToArray();
            Assert.Equal(
                OldestHilbertComparison.Reference(prices),
                OldestHilbertComparison.Owned(bars).Outputs["Value"].Values
            );
        }
    }

    [Fact]
    public void NativeCurrentPhasorIdentityAndResetLimitationAreExplicit()
    {
        var buffer = new QuanTAlib.CircularBuffer(2);
        buffer.Add(3);
        buffer.Add(7);
        Assert.Equal(7, buffer[1]);
        var indicator = new QuanTAlib.Htit();
        foreach (var v in Enumerable.Range(1, 25))
            indicator.Calc(new QuanTAlib.TValue(v, true, false));
        indicator.Init();
        var replay = Enumerable
            .Range(30, 30)
            .Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
            .ToArray();
        var clean = new QuanTAlib.Htit();
        var fresh = Enumerable
            .Range(30, 30)
            .Select(v => clean.Calc(new QuanTAlib.TValue(v, true, false)).Value)
            .ToArray();
        Assert.NotEqual(fresh, replay);
        var inputs = Enumerable.Range(1, 80).Select(i => 10 + Math.Sin(i)).ToArray();
        var native = new QuanTAlib.Htit();
        Assert.Equal(
            OldestHilbertComparison.NativeReference(inputs),
            inputs.Select(v => native.Calc(new QuanTAlib.TValue(v, true, false)).Value)
        );
    }

    [Fact]
    public void CorruptedOutputFailsExactComparison()
    {
        var data = CompetitorData.Create(60);
        var expected = OldestHilbertComparison.Owned(data.IndicatorBars);
        var changed = OldestHilbertComparison.Owned(data.IndicatorBars);
        changed.Outputs["Value"].Values[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                changed,
                "Hilbert corruption",
                IndicatorErrorBudget.Exact
            )
        );
    }
}
