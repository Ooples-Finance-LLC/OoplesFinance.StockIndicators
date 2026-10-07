using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OrderStatisticNumericalTests
{
    public static IEnumerable<object[]> Configurations()
    {
        foreach (var kind in new[] { "highest", "lowest", "max", "min", "rank", "median", "median-ma", "trimean" })
        foreach (var period in new[] { 1, 2, 3, 14, 37 })
            yield return new object[] { kind, period };
    }

    private static IIndicator Create(string kind, int period) => kind switch
    {
        "highest" => new HighestHigh(period), "lowest" => new LowestLow(period),
        "max" => new RollingMax(period), "min" => new RollingMin(period),
        "rank" => new PercentRank(period), "median" => new MedianValue(period), "median-ma" => new MedianMa(period), _ => new Trimean(period)
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task AllOrderStatisticsAutomaticallyReceiveEveryNumericalClass(string kind, int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new(Create(kind, period).GetType(), "order-numerics",
            () => Create(kind, period)));
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task MixedFieldsExtremeEvictionPreviewAndResetAgreeAcrossRoutes(string kind, int period)
    {
        var levels = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0d,
            1e100, 1d, -1e100, 3d, 2d, 3d, 1d };
        var bars = Enumerable.Range(0, 3 * period + levels.Length).Select(i =>
        {
            var close = i <= period ? double.MaxValue : levels[i % levels.Length];
            var other = i <= period ? double.MaxValue : levels[(i + 3) % levels.Length];
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), close, Math.Max(close, other), Math.Min(close, other), close, 1);
        }).ToArray();
        var indicator = Create(kind, period);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var stock = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(),
            bars.Select(b => b.Time).ToList());
        switch (kind)
        {
            case "highest": stock.CalculateHighestHigh(period); break;
            case "lowest": stock.CalculateLowestLow(period); break;
            case "max": stock.CalculateRollingMax(period); break;
            case "min": stock.CalculateRollingMin(period); break;
            case "rank": stock.CalculatePercentRank(period); break;
            case "median": case "median-ma": stock.CalculateMedianValue(period); break;
            default: stock.CalculateTrimean(period); break;
        }
        var expected = run[indicator].ToArray();
        Assert.Equal(expected, stock.CustomValuesList);
        if (kind is "trimean" or "median" or "median-ma")
        {
            var core = new double[bars.Length];
            var close = bars.Select(b => b.Close).ToArray();
            if (kind == "trimean") MovingAverageCore.Trimean(close, core, period);
            else if (kind == "median-ma") MovingAverageCore.MedianMovingAverage(close, core, period);
            else OscillatorCore.MedianValue(close, core, period);
            Assert.Equal(expected, core);
        }
        IStreamingIndicatorState state = kind switch
        {
            "highest" => new HighestHighState(period), "lowest" => new LowestLowState(period),
            "max" => new RollingMaxState(period), "min" => new RollingMinState(period),
            "rank" => new PercentRankState(period), "median" or "median-ma" => new MedianValueState(period), _ => new TrimeanState(period)
        };
        using var lifetime = state as IDisposable;
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("ORDER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var preview = state.Update(input, false, true);
                var committed = state.Update(input, true, true);
                Assert.Equal(expected[i], preview.Value); Assert.Equal(expected[i], committed.Value);
                foreach (var output in committed.Outputs!)
                {
                    Assert.True(double.IsFinite(output.Value));
                    Assert.Equal(stock.OutputValues[output.Key][i], output.Value);
                    Assert.Equal(output.Value, preview.Outputs![output.Key]);
                }
            }
        }
    }
}
