using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaReversalContinuationBenchmarks : SharedTaCandleBenchmarkBase
{
    [Params("AdvanceBlock", "Stalled", "ThreeStarsInSouth", "ThreeBlackCrows", "IdenticalThreeCrows", "ThreeWhiteSoldiers", "PiercingLine", "DarkCloudCover")]
    public override string Operation { get; set; } = "";
    protected override int Lookback => Operation switch
    {
        "PiercingLine" or "DarkCloudCover" => 11,
        "ThreeBlackCrows" => 13,
        _ => 12
    };

    protected override int NativeLookback() => Operation switch
    {
        "AdvanceBlock" => Candles.AdvanceBlockLookback(),
        "Stalled" => Candles.StalledLookback(),
        "ThreeStarsInSouth" => Candles.ThreeStarsInSouthLookback(),
        "ThreeBlackCrows" => Candles.ThreeBlackCrowsLookback(),
        "IdenticalThreeCrows" => Candles.IdenticalThreeCrowsLookback(),
        "ThreeWhiteSoldiers" => Candles.ThreeWhiteSoldiersLookback(),
        "PiercingLine" => Candles.PiercingLineLookback(),
        "DarkCloudCover" => Candles.DarkCloudCoverLookback(),
        _ => throw new ArgumentException("Unknown TA reversal/continuation operation.")
    };

    protected override IIndicator Create() => Operation switch
    {
        "AdvanceBlock" or "Stalled" or "ThreeStarsInSouth" => ExhaustionComparison.Create(Operation),
        "ThreeBlackCrows" or "IdenticalThreeCrows" or "ThreeWhiteSoldiers" => CrowSoldierComparison.Create(Operation),
        _ => PenetrationCandleComparison.Create(Operation)
    };

    protected override TALib.Core.RetCode ComputeNative(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> packed, out System.Range range) => Operation switch
    {
        "AdvanceBlock" => Candles.AdvanceBlock<double>(open, high, low, close, System.Range.All, packed, out range),
        "Stalled" => Candles.Stalled<double>(open, high, low, close, System.Range.All, packed, out range),
        "ThreeStarsInSouth" => Candles.ThreeStarsInSouth<double>(open, high, low, close, System.Range.All, packed, out range),
        "ThreeBlackCrows" => Candles.ThreeBlackCrows<double>(open, high, low, close, System.Range.All, packed, out range),
        "IdenticalThreeCrows" => Candles.IdenticalThreeCrows<double>(open, high, low, close, System.Range.All, packed, out range),
        "ThreeWhiteSoldiers" => Candles.ThreeWhiteSoldiers<double>(open, high, low, close, System.Range.All, packed, out range),
        "PiercingLine" => Candles.PiercingLine<double>(open, high, low, close, System.Range.All, packed, out range),
        _ => Candles.DarkCloudCover<double>(open, high, low, close, System.Range.All, packed, out range, .5)
    };
}
