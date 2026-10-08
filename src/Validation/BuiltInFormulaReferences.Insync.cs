using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> InsyncOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        Outputs(("Iidx", InsyncValues(bars, indicator, (indicator as IIndicator)?.Source is not null ? bars.Select(b => b.Close).ToArray() : null).Line));

    // Independent arrays and rational expressions; no production component/window calls.
    internal static (double[] Line, Dictionary<string, ReferenceFraction[]> Components, Dictionary<string, double[]> Votes)
        InsyncValues(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? selected = null)
    {
        var options = indicator.CreateOptions(); var count = bars.Count;
        int P(string name) => Math.Max(1, Integer(options, name));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var zero = R(0); var hundred = R(100);
        ReferenceFraction Sum(IEnumerable<ReferenceFraction> values) => values.Aggregate(zero, (s, v) => s + v);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period, bool partial = false) => values.Select((_, i) =>
            !partial && i + 1 < period ? zero : Round(Sum(values.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(period, i + 1))) / R(Math.Min(period, i + 1)))).ToArray();
        ReferenceFraction[] Ema(ReferenceFraction[] values, int period)
        {
            var result = new ReferenceFraction[count];
            for (var i = 0; i < count; i++) result[i] = i < period
                ? Round(Sum(values.Take(i + 1)) / R(i + 1))
                : Round((result[i - 1] * R(period - 1L) + values[i] * R(2)) / R(period + 1L));
            return result;
        }
        ReferenceFraction ClampPercent(ReferenceFraction value) => value.Sign < 0 ? zero : value.CompareTo(hundred) > 0 ? hundred : value;
        var price = bars.Select((b, i) => R(selected is null ? b.Close : selected[i])).ToArray();
        var typical = bars.Select((b, i) => selected is null ? Round((R(b.High) + R(b.Low) + R(b.Close)) / R(3)) : price[i]).ToArray();
        var cci = new ReferenceFraction[count]; var mfi = new ReferenceFraction[count];
        var rsi = new ReferenceFraction[count]; var stochastic = new ReferenceFraction[count];
        var roc = new ReferenceFraction[count]; var dpo = new ReferenceFraction[count]; var ease = new ReferenceFraction[count];
        var pct = new ReferenceFraction[count];
        var positive = new ReferenceFraction[count]; var negative = new ReferenceFraction[count];
        var fast = Ema(price, P("FastLength")); var slow = Ema(price, P("SlowLength"));
        var macd = fast.Select((v, i) => Round(v - slow[i])).ToArray();
        var middle = Mean(price, P("BbLength")); var dpoMiddle = Mean(price, P("DpoLength"));
        var high = bars.Select((b, i) => selected is null || (selected[i] >= b.Low && selected[i] <= b.High)
            ? b.High : Math.Max(selected[i], selected[Math.Max(0, i - 1)])).ToArray();
        var low = bars.Select((b, i) => selected is null || (selected[i] >= b.Low && selected[i] <= b.High)
            ? b.Low : Math.Min(selected[i], selected[Math.Max(0, i - 1)])).ToArray();
        var gain = zero; var loss = zero; var divisor = R(Number(options, 10000, "Divisor"));
        var mult = R(Number(options, 2, "StdDevMult"));
        var delay = (int)Math.Max(2, Math.Min(530, (P("DpoLength") + 1L) / 2 + 1));
        for (var i = 0; i < count; i++)
        {
            var samples = typical.Skip(Math.Max(0, i - P("CciLength") + 1)).Take(Math.Min(P("CciLength"), i + 1)).ToArray();
            var center = Sum(samples) / R(samples.Length);
            var distance = Sum(samples.Select(v => (v - center).Abs())) / R(samples.Length);
            cci[i] = samples.Length < P("CciLength") || distance.Sign == 0 ? zero : Round((typical[i] - center) / (R(.015) * distance));
            var direction = i == 0 ? 0 : typical[i].CompareTo(typical[i - 1]);
            positive[i] = direction > 0 ? typical[i] * R(bars[i].Volume) : zero;
            negative[i] = direction < 0 ? typical[i] * R(bars[i].Volume) : zero;
            var up = Sum(positive.Skip(Math.Max(0, i - P("MfiLength") + 1)).Take(Math.Min(P("MfiLength"), i + 1)));
            var down = Sum(negative.Skip(Math.Max(0, i - P("MfiLength") + 1)).Take(Math.Min(P("MfiLength"), i + 1)));
            mfi[i] = down.Sign == 0 ? hundred : up.Sign == 0 || (up + down).Sign == 0 ? zero : ClampPercent(Round(hundred * up / (up + down)));

            var change = i == 0 ? zero : Round(price[i] - price[i - 1]);
            gain = Round((gain * R(P("RsiLength") - 1L) + (change.Sign > 0 ? change : zero)) / R(P("RsiLength")));
            loss = Round((loss * R(P("RsiLength") - 1L) + (change.Sign < 0 ? change.Abs() : zero)) / R(P("RsiLength")));
            rsi[i] = i > 0 && P("RsiLength") > 1 && price[i].CompareTo(price[i - 1]) == 0 ? rsi[i - 1]
                : (gain + loss).Sign == 0 ? hundred : Round(hundred * gain / (gain + loss));
            var highs = high.Skip(Math.Max(0, i - P("StochLength") + 1)).Take(Math.Min(P("StochLength"), i + 1));
            var lows = low.Skip(Math.Max(0, i - P("StochLength") + 1)).Take(Math.Min(P("StochLength"), i + 1));
            var top = R(highs.Max()); var bottom = R(lows.Min());
            stochastic[i] = top.CompareTo(bottom) == 0 ? zero : ClampPercent(Round(hundred * (price[i] - bottom) / (top - bottom)));
            roc[i] = i < P("RocLength") || price[i - P("RocLength")].Sign == 0 ? zero
                : Round(hundred * (price[i] - price[i - P("RocLength")]) / price[i - P("RocLength")]);
            dpo[i] = Round((i < delay ? zero : price[i - delay]) - dpoMiddle[i]);
            ease[i] = i == 0 || bars[i].Volume == 0 ? zero : Round(divisor *
                (R(bars[i].High) + R(bars[i].Low) - R(bars[i - 1].High) - R(bars[i - 1].Low)) *
                (R(bars[i].High) - R(bars[i].Low)) / (R(2) * R(bars[i].Volume)));
            var window = price.Skip(Math.Max(0, i - P("BbLength") + 1)).Take(Math.Min(P("BbLength"), i + 1)).ToArray();
            var mean = Sum(window) / R(window.Length);
            var variance = Sum(window.Select(v => (v - mean) * (v - mean))) / R(window.Length);
            var width = (window.Length < P("BbLength") ? zero : R(variance.SqrtToDouble())) * mult;
            pct[i] = width.Sign == 0 ? zero : Round(hundred * (price[i] - middle[i] + width) / (R(2) * width));
        }
        var k = Mean(stochastic, P("StochKLength")); var d = Mean(k, P("StochDLength"));
        var components = new Dictionary<string, ReferenceFraction[]> { ["Cci"] = cci, ["Mfi"] = mfi, ["Rsi"] = rsi,
            ["PctB"] = pct, ["K"] = k, ["D"] = d, ["Macd"] = macd, ["Dpo"] = dpo, ["Roc"] = roc, ["Emv"] = ease };
        double[] Band(ReferenceFraction[] values, double lower, double upper) => values.Select(v => v.CompareTo(R(lower)) < 0 ? -5d : v.CompareTo(R(upper)) > 0 ? 5d : 0).ToArray();
        double[] Direction(ReferenceFraction[] values, bool inverse = false)
        {
            var means = Mean(values, P("SmaLength"), true);
            return values.Select((v, i) => inverse
                ? means[i].Sign > 0 && v.CompareTo(means[i]) > 0 ? 5d : means[i].Sign < 0 && v.CompareTo(means[i]) <= 0 ? -5d : 0
                : means[i].Sign > 0 && v.CompareTo(means[i]) >= 0 ? 5d : means[i].Sign < 0 && v.CompareTo(means[i]) < 0 ? -5d : 0).ToArray();
        }
        var votes = new Dictionary<string, double[]> { ["Cci"] = Band(cci, -100, 100), ["Mfi"] = Band(mfi, 20, 80), ["Rsi"] = Band(rsi, 30, 70),
            ["PctB"] = Band(pct, 5, 95), ["K"] = Band(k, 20, 80), ["D"] = Band(d, 20, 80), ["Macd"] = Direction(macd), ["Roc"] = Direction(roc), ["Emv"] = Direction(ease) };
        var buy = Direction(dpo); var sell = Direction(dpo, true);
        votes["DpoBuy"] = buy.Select((_, i) => i < P("SmaLength") ? 0 : buy[i - P("SmaLength")]).ToArray();
        votes["DpoSell"] = sell.Select((_, i) => i < P("SmaLength") ? 0 : sell[i - P("SmaLength")]).ToArray();
        return (Enumerable.Range(0, count).Select(i => 50 + votes.Values.Sum(v => v[i])).ToArray(), components, votes);
    }
}
