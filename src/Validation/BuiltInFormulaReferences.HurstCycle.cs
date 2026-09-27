using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HurstCycleOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return HurstCycleOutputs(bars, Integer(o, "FastLength", 10), Integer(o, "SlowLength", 30), AverageKind(o, 6), Number(o, 1, "FastMult"), Number(o, 3, "SlowMult")); }
    internal static IReadOnlyDictionary<string, double[]> HurstCycleOutputs(IReadOnlyList<Bar> bars, int fastLength, int slowLength, int kind, double fastFactor, double slowFactor, double[][]? external = null)
    {
        int Half(int length) => Math.Min(530, Math.Max(2, (int)Math.Ceiling(Math.Max(1, length) / 2d))); var fc = Half(fastLength); var sc = Half(slowLength); var fd = Half(fc); var sd = Half(sc);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var ranges = bars.Select((b,i) => new[] { ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low), (ReferenceFraction.FromDouble(b.High) - prices[i == 0 ? 0 : i - 1]).Abs(), (ReferenceFraction.FromDouble(b.Low) - prices[i == 0 ? 0 : i - 1]).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var fa = external is null ? SmoothRocBankStage(ranges, fc, kind) : external[0].Select(ReferenceFraction.FromDouble).ToArray(); var sa = external is null ? SmoothRocBankStage(ranges, sc, kind) : external[1].Select(ReferenceFraction.FromDouble).ToArray(); var fm = external is null ? SmoothRocBankStage(prices, fc, kind) : external[2].Select(ReferenceFraction.FromDouble).ToArray(); var sm = external is null ? SmoothRocBankStage(prices, sc, kind) : external[3].Select(ReferenceFraction.FromDouble).ToArray();
        var keys = new[] { "FastUpperBand", "FastMiddleBand", "FastLowerBand", "SlowUpperBand", "SlowMiddleBand", "SlowLowerBand", "OMed", "OShort" }; var output = keys.ToDictionary(k => k, _ => new double[bars.Count]); var ff = ReferenceFraction.FromDouble(fastFactor); var sf = ReferenceFraction.FromDouble(slowFactor); var two = new ReferenceFraction(2);
        for (var i = 0; i < bars.Count; i++)
        {
            var fast = i < fd ? prices[i] : fm[i - fd]; var slow = i < sd ? prices[i] : sm[i - sd]; var fu = (fast + ff * fa[i]).RoundExtendedBinary64(); var fl = (fast - ff * fa[i]).RoundExtendedBinary64(); var su = (slow + sf * sa[i]).RoundExtendedBinary64(); var sl = (slow - sf * sa[i]).RoundExtendedBinary64(); var mid = ((fu + fl) / two).RoundExtendedBinary64(); var slowMid = ((su + sl) / two).RoundExtendedBinary64(); var width = su - sl;
            output["FastUpperBand"][i] = fu.ToDouble(); output["FastMiddleBand"][i] = mid.ToDouble(); output["FastLowerBand"][i] = fl.ToDouble(); output["SlowUpperBand"][i] = su.ToDouble(); output["SlowMiddleBand"][i] = slowMid.ToDouble(); output["SlowLowerBand"][i] = sl.ToDouble(); output["OMed"][i] = width.Sign == 0 ? 0 : ((mid - sl) / width).ToDouble(); output["OShort"][i] = width.Sign == 0 ? 0 : ((prices[i] - sl) / width).ToDouble();
        }
        return output;
    }
}
