using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Pins the Negative and Positive Volume Index formulas to Fosback's definition.
/// </summary>
/// <remarks>
/// Both indices start at a fixed level (1000 by convention) and, on qualifying days, compound that
/// period's rate of change onto the running index:
/// <code>
///     NVI = prevNVI + (prevNVI * (close - prevClose) / prevClose)      // when volume FALLS
///     PVI = prevPVI + (prevPVI * (close - prevClose) / prevClose)      // when volume RISES
/// </code>
/// On non-qualifying days the index carries forward unchanged. The expected values below are computed
/// by hand from that definition, so a regression in the formula fails here rather than silently
/// producing a plausible-looking but wrongly scaled series.
/// </remarks>
public class VolumeIndexFormulaTests
{
    private const double Tolerance = 1e-6;

    /// <summary>Close/volume pairs chosen so each branch of both indicators is exercised.</summary>
    private static List<TickerData> Series() =>
    [
        //                     close, volume
        Bar("2024-01-01", 100.0, 1_000),
        Bar("2024-01-02", 110.0,   500),   // volume FELL, +10%  -> NVI moves, PVI holds
        Bar("2024-01-03", 121.0, 2_000),   // volume ROSE, +10%  -> PVI moves, NVI holds
        Bar("2024-01-04", 108.9, 1_000),   // volume FELL, -10%  -> NVI moves, PVI holds
    ];

    private static TickerData Bar(string date, double close, double volume) => new()
    {
        Date = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
        Open = close,
        High = close,
        Low = close,
        Close = close,
        Volume = volume
    };

    [Fact]
    public void NegativeVolumeIndex_CompoundsRateOfChangeOnFallingVolume()
    {
        var actual = new StockData(Series()).CalculateNegativeVolumeIndex().CustomValuesList;

        // Day 1: seed.                                            1000
        // Day 2: volume fell, +10%  -> 1000 + 1000*0.10          = 1100
        // Day 3: volume rose        -> unchanged                 = 1100
        // Day 4: volume fell, -10%  -> 1100 + 1100*(-0.10)       =  990
        Assert.Equal(4, actual.Count);
        Assert.Equal(1000.0, actual[0], Tolerance);
        Assert.Equal(1100.0, actual[1], Tolerance);
        Assert.Equal(1100.0, actual[2], Tolerance);
        Assert.Equal(990.0, actual[3], Tolerance);
    }

    [Fact]
    public void PositiveVolumeIndex_CompoundsRateOfChangeOnRisingVolume()
    {
        var actual = new StockData(Series()).CalculatePositiveVolumeIndex().CustomValuesList;

        // Day 1: seed.                                            1000
        // Day 2: volume fell        -> unchanged                 = 1000
        // Day 3: volume rose, +10%  -> 1000 + 1000*0.10          = 1100
        // Day 4: volume fell        -> unchanged                 = 1100
        Assert.Equal(4, actual.Count);
        Assert.Equal(1000.0, actual[0], Tolerance);
        Assert.Equal(1000.0, actual[1], Tolerance);
        Assert.Equal(1100.0, actual[2], Tolerance);
        Assert.Equal(1100.0, actual[3], Tolerance);
    }

    [Fact]
    public void VolumeIndexes_RespectACustomInitialValue()
    {
        var nvi = new StockData(Series()).CalculateNegativeVolumeIndex(initialValue: 100).CustomValuesList;

        // Same 10% moves, seeded at 100 instead of 1000.
        Assert.Equal(100.0, nvi[0], Tolerance);
        Assert.Equal(110.0, nvi[1], Tolerance);
        Assert.Equal(110.0, nvi[2], Tolerance);
        Assert.Equal(99.0, nvi[3], Tolerance);
    }
}
