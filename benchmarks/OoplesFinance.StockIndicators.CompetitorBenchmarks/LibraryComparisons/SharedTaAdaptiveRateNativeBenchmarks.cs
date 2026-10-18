using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaAdaptiveRateNativeBenchmarks : SharedTaPackedBenchmarkBase
{
    [Params("TaLib.Functions.Trix", "TaLib.Functions.Kama", "TaLib.Functions.Mavp")]
    public string Operation { get; set; } = "";
    private double[] _periods = [];
    protected override string Mapping => Operation;
    protected override int Lookback => RequiredLookback(Operation);
    protected override int Columns => 1;
    internal static int RequiredLookback(string operation) => operation switch
    {
        "TaLib.Functions.Trix" => 58,
        "TaLib.Functions.Kama" => 20,
        "TaLib.Functions.Mavp" => 29,
        _ => throw new ArgumentException("Unknown TA adaptive/rate operation.")
    };
    protected override void PrepareNative() =>
        _periods = Operation == "TaLib.Functions.Mavp" ? _bars.Select(VariablePeriodComparison.Selection).ToArray() : [];
    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.Trix" => new TripleExponentialRate(20, TripleRateSeed.CascadedMeans),
        "TaLib.Functions.Kama" => new SeededAdaptiveAverage(20, resetFlat: false, fastFlat: true, outputDelay: 1),
        "TaLib.Functions.Mavp" => new VariablePeriodClassicAverage(VariablePeriodComparison.Selection),
        _ => throw new ArgumentException("Unknown TA adaptive/rate operation.")
    };
    protected override TALib.Core.RetCode Call(System.Range input, double[][] output, out System.Range range) => Operation switch
    {
        "TaLib.Functions.Trix" => Functions.Trix<double>(Data.Closes, input, output[0], out range, 20),
        "TaLib.Functions.Kama" => Functions.Kama<double>(Data.Closes, input, output[0], out range, 20),
        "TaLib.Functions.Mavp" => Functions.Mavp<double>(Data.Closes, _periods, input,
            output[0], out range, 2, 30, TALib.Core.MAType.Sma),
        _ => throw new ArgumentException("Unknown TA adaptive/rate operation.")
    };

    private protected override void VerifyBuilder(ComparisonPair pair, string[] names)
    {
        if (Operation == "TaLib.Functions.Mavp") { base.VerifyBuilder(pair, names); return; }
        // These owners publish additional columns beyond the native scalar.
        // Retain and independently verify every value/presence column.
        var expected = Operation == "TaLib.Functions.Trix"
            ? TripleRateComparison.Reference(Data.Closes, 20, TripleRateSeed.CascadedMeans)
            : SeededAdaptiveComparison.ReferenceValues(Data.Closes, 20, 2, 30, false, false, false, true, 1);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create();
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                var values = expected[slot].Select(v => v ?? 0).ToArray();
                var present = expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray();
                AsinFeasibilityBenchmarks.RequireSame(values, run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(present, run[owner.Outputs[slot + expected.Length]].ToArray());
                if (Count == 0) continue;
                AsinFeasibilityBenchmarks.RequireSame([values[^1]], [run.Latest[owner.Outputs[slot]]]);
                AsinFeasibilityBenchmarks.RequireSame([present[^1]], [run.Latest[owner.Outputs[slot + expected.Length]]]);
            }
        }
    }
}
