using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaStarReversalBenchmarks : SharedTaCandleBenchmarkBase
{
    [Params("MorningStar", "EveningStar", "MorningDojiStar", "EveningDojiStar", "AbandonedBaby")]
    public override string Operation { get; set; } = "";
    protected override int Lookback => 12;

    protected override int NativeLookback() => Operation switch
        {
            "MorningStar" => Candles.MorningStarLookback(),
            "EveningStar" => Candles.EveningStarLookback(),
            "MorningDojiStar" => Candles.MorningDojiStarLookback(),
            "EveningDojiStar" => Candles.EveningDojiStarLookback(),
            "AbandonedBaby" => Candles.AbandonedBabyLookback(),
            _ => throw new ArgumentException("Unknown TA star reversal operation.")
        };

    protected override IIndicator Create() => Operation == "AbandonedBaby"
        ? new AbandonedBabyCandle() : StarReversalComparison.Create(Operation);

    protected override TALib.Core.RetCode ComputeNative(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> packed, out System.Range range) =>
        Operation switch
        {
            "MorningStar" => Candles.MorningStar<double>(open, high, low, close, System.Range.All, packed, out range, .3),
            "EveningStar" => Candles.EveningStar<double>(open, high, low, close, System.Range.All, packed, out range, .3),
            "MorningDojiStar" => Candles.MorningDojiStar<double>(open, high, low, close, System.Range.All, packed, out range, .3),
            "EveningDojiStar" => Candles.EveningDojiStar<double>(open, high, low, close, System.Range.All, packed, out range, .3),
            _ => Candles.AbandonedBaby<double>(open, high, low, close, System.Range.All, packed, out range, .3)
        };
}
