using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumeBalanceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 14);
        var name = indicator.BatchName; var zero = new ReferenceFraction(0);
        var output = bars.Select((_, i) =>
        {
            var up = zero; var down = zero; var start = Math.Max(0, i - length + 1);
            for (var j = start; j <= i; j++)
            {
                var bar = bars[j]; var volume = ReferenceFraction.FromDouble(bar.Volume);
                if (name == IndicatorName.VolumeAccumulationOscillator)
                {
                    var midpoint = (ReferenceFraction.FromDouble(bar.High) + ReferenceFraction.FromDouble(bar.Low)) / new ReferenceFraction(2);
                    up += (ReferenceFraction.FromDouble(bar.Close) - midpoint) * volume;
                }
                else
                {
                    var prior = name == IndicatorName.UpsideDownsideVolume ? j == 0 ? 0 : bars[j - 1].Close : bar.Open;
                    if (bar.Close > prior) up += volume;
                    if (bar.Close < prior)
                    {
                        if (name == IndicatorName.UpsideDownsideVolume) down -= volume;
                        else up -= volume;
                    }
                }
            }
            return name == IndicatorName.UpsideDownsideVolume ? down.Sign == 0 ? 0 : (up / down).ToDouble()
                : (up / new ReferenceFraction(name == IndicatorName.TFSVolumeOscillator ? length : i - start + 1)).ToDouble();
        }).ToArray();
        return Outputs((name == IndicatorName.UpsideDownsideVolume ? "Udv" : name == IndicatorName.TFSVolumeOscillator ? "Tfsvo" : "Vao", output));
    }
}
