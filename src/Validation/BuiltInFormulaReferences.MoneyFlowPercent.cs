using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MoneyFlowPercentOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator,
        double[]? customerVolumes = null, double[]? customerFlows = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 10);
        var twiggs = indicator.BatchName == IndicatorName.TwiggsMoneyFlow;
        var zero = new ReferenceFraction(0);
        var flows = bars.Select((bar, i) =>
        {
            var previous = i == 0 ? 0 : bars[i - 1].Close;
            var h = twiggs ? Math.Max(bar.High, previous) : bar.High; var l = twiggs ? Math.Min(bar.Low, previous) : bar.Low;
            var high = ReferenceFraction.FromDouble(h); var low = ReferenceFraction.FromDouble(l);
            return h == l ? zero : RoundRocBankStage((new ReferenceFraction(2) * ReferenceFraction.FromDouble(bar.Close) - high - low) * ReferenceFraction.FromDouble(bar.Volume) / (high - low));
        }).ToArray();
        var volumes = bars.Select(b => ReferenceFraction.FromDouble(b.Volume)).ToArray();
        if (twiggs)
        {
            flows = customerFlows is null ? SmoothRocBankStage(flows, length, AverageKind(options, 3)) : customerFlows.Select(ReferenceFraction.FromDouble).ToArray();
            volumes = customerVolumes is null ? SmoothRocBankStage(volumes, length, AverageKind(options, 3)) : customerVolumes.Select(ReferenceFraction.FromDouble).ToArray();
        }
        var bound = twiggs ? 1d : 100d;
        var output = flows.Select((v, i) =>
        {
            var numerator = twiggs ? v : new ReferenceFraction(100) * Window(flows, i, length).Aggregate(zero, (a, b) => a + b);
            var denominator = twiggs ? volumes[i] : Window(volumes, i, length).Aggregate(zero, (a, b) => a + b);
            return denominator.Sign == 0 ? 0 : Math.Min(bound, Math.Max(-bound, (numerator / denominator).ToDouble()));
        }).ToArray();
        return Outputs((twiggs ? "Tmf" : "Vapc", output));
    }
}
