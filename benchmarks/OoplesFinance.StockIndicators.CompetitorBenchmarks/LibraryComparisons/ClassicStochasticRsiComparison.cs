using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ClassicStochasticRsiComparison
{
    internal static readonly string[] Names = ["K", "D"];

    internal static ComparisonSeries Series(double?[][] values) =>
        RetrospectivePriceComparison.Series(Names, values);

    internal static ComparisonPair Pair(
        int rsi = 14,
        int window = 5,
        int signal = 3,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool first = false,
        int rsiSuppression = 0,
        int averageSuppression = 0
    ) =>
        new(
            "TaLib.Functions.StochRsi",
            nameof(ClassicStochasticRsi),
            (d, _) => Native(d, rsi, window, signal, method),
            (d, _) =>
                Owned(
                    d.IndicatorBars,
                    rsi,
                    window,
                    signal,
                    method,
                    first,
                    rsiSuppression,
                    averageSuppression
                ),
            (d, _) =>
                Series(
                    Reference(
                        d.Closes,
                        rsi,
                        window,
                        signal,
                        method,
                        first,
                        rsiSuppression,
                        averageSuppression
                    )
                ),
            Names,
            MinimumInputCount: 2,
            CompetitorReference: (d, _) =>
            {
                var packed = NativePacked(
                    d.Closes,
                    rsi,
                    window,
                    signal,
                    method,
                    first,
                    rsiSuppression,
                    averageSuppression,
                    0,
                    d.Count - 1
                );
                var values = Enumerable.Range(0, 2).Select(_ => new double?[d.Count]).ToArray();
                for (var j = 0; j < 2; j++)
                for (var i = 0; i < packed.Values[j].Length; i++)
                    values[j][packed.Start + i] = packed.Values[j][i];
                return Series(values);
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly ClassicAverageComparison.Settings _averages;
        private readonly int _rsi = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Rsi);

        internal Settings(bool first, int rsiSuppression, int averageSuppression)
        {
            _averages = new(first, averageSuppression);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Rsi, rsiSuppression);
        }

        public void Dispose()
        {
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Rsi, _rsi);
            _averages.Dispose();
        }
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int rsi,
        int window,
        int signal,
        ClassicAverageMethod method,
        bool first,
        int rsiSuppression,
        int averageSuppression
    )
    {
        var indicator = new ClassicStochasticRsi(
            rsi,
            window,
            signal,
            method,
            first,
            rsiSuppression,
            averageSuppression
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 2)
                .Select(j =>
                {
                    var values = run[indicator.Outputs[j]].ToArray();
                    var flags = run[indicator.Outputs[j + 2]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData data,
        int rsi,
        int window,
        int signal,
        ClassicAverageMethod method
    )
    {
        var values = Enumerable.Range(0, 2).Select(_ => new double?[data.Count]).ToArray();
        if (data.Count == 0)
            return Series(values);
        var k = new double[data.Count];
        var d = new double[data.Count];
        var code = Functions.StochRsi<double>(
            data.Closes,
            System.Range.All,
            k,
            d,
            out var range,
            rsi,
            window,
            signal,
            (TaCore.MAType)method
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA stochastic RSI: " + code);
        for (var i = range.Start.Value; i < range.End.Value; i++)
        {
            values[0][i] = k[i - range.Start.Value];
            values[1][i] = d[i - range.Start.Value];
        }
        return Series(values);
    }

    internal static double?[][] Reference(
        double[] prices,
        int rsiPeriod,
        int window,
        int signal,
        ClassicAverageMethod method,
        bool first,
        int rsiSuppression,
        int averageSuppression
    )
    {
        var output = Enumerable.Range(0, 2).Select(_ => new double?[prices.Length]).ToArray();
        var start = (long)rsiPeriod + rsiSuppression + window - 1;
        if (start >= prices.Length)
            return output;
        var rsi = WilderStrengthComparison
            .OwnedReference(prices, rsiPeriod, WilderStrengthConvention.RsiZeroFlat, rsiSuppression)
            .Outputs["Value"]
            .Values;
        var raw = new double[prices.Length - (int)start];
        for (var i = (int)start; i < prices.Length; i++)
        {
            var values = rsi.Skip(i - window + 1).Take(window).Select(Units).ToArray();
            var bottom = values.Min();
            var top = values.Max();
            raw[i - (int)start] =
                top == bottom ? 0 : Round(100 * (values[^1] - bottom), top - bottom);
        }
        var average = ClassicAverageComparison.Reference(
            raw,
            signal,
            method,
            first,
            averageSuppression
        );
        for (var i = 0; i < raw.Length; i++)
        {
            if (!average[i].HasValue)
                continue;
            output[0][(int)start + i] = raw[i];
            output[1][(int)start + i] = average[i];
        }
        return output;
    }

    internal static (int Start, T[][] Values) NativePacked<T>(
        T[] prices,
        int rsi,
        int window,
        int signal,
        ClassicAverageMethod method,
        bool first,
        int rsiSuppression,
        int averageSuppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var stochasticLookback =
            window - 1 + (int)ClassicAverageComparison.First(signal, method, averageSuppression);
        var start = Math.Max(
            requested,
            rsi + rsiSuppression - (first ? 1 : 0) + stochasticLookback
        );
        if (start > end)
            return (
                0,
                [
                    [],
                    [],
                ]
            );
        var buffer = new T[end - start + 1 + stochasticLookback];
        var strength = NativeRsi(
            prices,
            rsi,
            first,
            rsiSuppression,
            start - stochasticLookback,
            end
        );
        strength.CopyTo(buffer, 0); // Native passes the entire allocation, including its Metastock zero tail.
        var packed = ClassicStochNativeReference.Fast(
            buffer,
            buffer,
            buffer,
            window,
            signal,
            method,
            first,
            averageSuppression,
            0,
            buffer.Length - 1
        );
        return (start, packed.Values);
    }

    internal static T[] NativeRsi<T>(
        T[] prices,
        int period,
        bool metastock,
        int suppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var lookback = period + suppression - (metastock ? 1 : 0);
        var first = Math.Max(requested, lookback);
        if (first > end)
            return [];
        var origin = first - lookback;
        var divisor = T.CreateChecked(period);
        T Publish(T gain, T loss) =>
            T.IsZero(gain + loss) ? T.Zero : T.CreateChecked(100) * (gain / (gain + loss));
        (T Gain, T Loss) Seed(int begin)
        {
            var previous = prices[origin];
            var gains = T.Zero;
            var losses = T.Zero;
            foreach (var value in prices.Skip(begin).Take(period))
            {
                var delta = value - previous;
                previous = value;
                if (delta < T.Zero)
                    losses -= delta;
                else
                    gains += delta;
            }
            if (begin + period > prices.Length)
                throw new IndexOutOfRangeException();
            return (gains / divisor, losses / divisor);
        }
        var result = new List<T>();
        var (gain, loss) = Seed(origin + 1);
        var seedEnd = origin + period;
        if (metastock && suppression == 0)
        {
            result.Add(Publish(gain, loss));
            if (seedEnd == end)
                return result.ToArray();
            (gain, loss) = Seed(origin + 2);
            seedEnd++;
        }
        if (seedEnd >= first)
            result.Add(Publish(gain, loss));
        for (var i = seedEnd + 1; i <= end; i++)
        {
            var delta = prices[i] - prices[i - 1];
            gain *= T.CreateChecked(period - 1);
            loss *= T.CreateChecked(period - 1);
            if (delta < T.Zero)
                loss -= delta;
            else
                gain += delta;
            gain /= divisor;
            loss /= divisor;
            if (i >= first)
                result.Add(Publish(gain, loss));
        }
        return result.ToArray();
    }
}
