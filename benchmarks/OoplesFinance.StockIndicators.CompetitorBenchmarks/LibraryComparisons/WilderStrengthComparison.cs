using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WilderStrengthComparison
{
    internal static readonly ComparisonPair[] Pairs = Enum.GetValues<WilderStrengthConvention>()
        .Select(Create)
        .ToArray();

    internal static ComparisonPair Create(WilderStrengthConvention convention) =>
        new(
            convention switch
            {
                WilderStrengthConvention.RsiHundredFlat => "Skender.GetRsi",
                WilderStrengthConvention.ChandeZeroFlat => "TaLib.Functions.Cmo",
                _ => "TaLib.Functions.Rsi",
            },
            nameof(WilderStrengthOscillator),
            (d, p) => Native(d, p, convention),
            (d, p) => Owned(d.IndicatorBars, p, convention),
            (d, p) => OwnedReference(d.Closes, p, convention),
            MinimumInputCount: convention == WilderStrengthConvention.RsiHundredFlat ? 1 : 2,
            CompetitorReference: (d, p) => NativeSeries(d, p, convention),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static TALib.Core.RetCode Call<T>(
        bool signed,
        T[] input,
        System.Range range,
        T[] output,
        out System.Range outputRange,
        int period
    )
        where T : IFloatingPointIeee754<T> =>
        signed
            ? TALib.Functions.Cmo<T>(input, range, output, out outputRange, period)
            : TALib.Functions.Rsi<T>(input, range, output, out outputRange, period);

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        WilderStrengthConvention convention
    )
    {
        if (convention == WilderStrengthConvention.RsiHundredFlat)
            return VolumePriceComparison.Mask(
                data.Quotes.GetRsi(period).Select(r => r.Rsi).ToArray()
            );
        if (
            TALib.Core.CompatibilitySettings.Get() != TALib.Core.CompatibilityMode.Default
            || TALib.Core.UnstablePeriodSettings.Get(
                convention == WilderStrengthConvention.ChandeZeroFlat
                    ? TALib.Core.UnstableFunc.Cmo
                    : TALib.Core.UnstableFunc.Rsi
            ) != 0
        )
            throw new InvalidOperationException(
                "Default strength comparisons require default native settings."
            );
        var buffer = new double[data.Count];
        var code = Call(
            convention == WilderStrengthConvention.ChandeZeroFlat,
            data.Closes,
            System.Range.All,
            buffer,
            out var range,
            period
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("Native strength failed: " + code);
        var expectedStart = data.Count > period ? period : 0;
        var count = Math.Max(0, data.Count - period);
        if (!range.Equals(new System.Range(expectedStart, expectedStart + count)))
            throw new InvalidOperationException("Unexpected native strength range.");
        var values = new double?[data.Count];
        for (var i = 0; i < count; i++)
            values[expectedStart + i] = buffer[i];
        return VolumePriceComparison.Mask(values);
    }

    internal static ComparisonSeries Owned(
        IReadOnlyList<Bar> bars,
        int period,
        WilderStrengthConvention convention,
        int unstable = 0
    )
    {
        var indicator = new WilderStrengthOscillator(period, convention, unstable);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            run[indicator.Value]
                .ToArray()
                .Select((v, i) => flags[i] > 0 ? (double?)v : null)
                .ToArray()
        );
    }

    internal readonly record struct Fraction(BigInteger N, BigInteger D);

    // Independent normalization oracle: move an exact positive ratio into [0.5,2],
    // use the existing integer-grid binary64 rounder, then restore its power of two.
    internal static Fraction Stage(BigInteger n, BigInteger d)
    {
        if (n.IsZero)
            return new(0, 1);
        var exponent = (int)(n.GetBitLength() - d.GetBitLength());
        var normal = exponent >= 0 ? Round(n, d << exponent) : Round(n << -exponent, d);
        n = Units(normal);
        d = Grid;
        if (exponent >= 0)
            n <<= exponent;
        else
            d <<= -exponent;
        var divisor = BigInteger.GreatestCommonDivisor(n, d);
        return new(n / divisor, d / divisor);
    }

    internal static ComparisonSeries OwnedReference(
        double[] prices,
        int period,
        WilderStrengthConvention convention,
        int unstable = 0
    )
    {
        var result = new double?[prices.Length];
        BigInteger seedGain = 0,
            seedLoss = 0;
        var gain = new Fraction(0, 1);
        var loss = new Fraction(0, 1);
        for (var i = 1; i < prices.Length; i++)
        {
            var difference = Units(prices[i]) - Units(prices[i - 1]);
            var up = BigInteger.Max(difference, 0);
            var down = BigInteger.Max(-difference, 0);
            if (i <= period)
            {
                seedGain += up;
                seedLoss += down;
                if (i < period)
                    continue;
                gain = Stage(seedGain, Grid * period);
                loss = Stage(seedLoss, Grid * period);
            }
            else
            {
                gain = Stage(gain.N * (period - 1) * Grid + up * gain.D, gain.D * Grid * period);
                loss = Stage(loss.N * (period - 1) * Grid + down * loss.D, loss.D * Grid * period);
            }
            if (i < (long)period + unstable)
                continue;
            var upper = gain.N * loss.D;
            var lower = loss.N * gain.D;
            result[i] = (upper + lower).IsZero
                ? (convention == WilderStrengthConvention.RsiHundredFlat ? 100 : 0)
                : Round(
                    100
                        * (
                            convention == WilderStrengthConvention.ChandeZeroFlat
                                ? upper - lower
                                : upper
                        ),
                    upper + lower
                );
        }
        return VolumePriceComparison.Mask(result);
    }

    private static ComparisonSeries NativeSeries(
        CompetitorData data,
        int period,
        WilderStrengthConvention convention
    )
    {
        var prices =
            convention == WilderStrengthConvention.RsiHundredFlat
                ? data.Quotes.Select(q => (double)q.Close).ToArray()
                : data.Closes;
        var (values, range) = NativeReference(prices, period, convention);
        var output = new double?[prices.Length];
        for (var i = 0; i < values.Length; i++)
            output[range.Start.Value + i] = values[i];
        return VolumePriceComparison.Mask(output);
    }

    internal static (double[] Values, System.Range Range) NativeReference(
        double[] prices,
        int period,
        WilderStrengthConvention convention,
        int start = 0,
        int? end = null,
        int unstable = 0,
        bool metastock = false
    )
    {
        var last = end ?? prices.Length - 1;
        var lookback = period + unstable - (metastock ? 1 : 0);
        var first = Math.Max(start, lookback);
        if (first > last)
            return ([], 0..0);
        var origin = first - lookback;
        var signed = convention == WilderStrengthConvention.ChandeZeroFlat;
        double Publish(double gain, double loss)
        {
            if (convention == WilderStrengthConvention.RsiHundredFlat)
                return loss > 0 ? Subtract(100, Divide(100, Add(1, Divide(gain, loss)))) : 100;
            var total = Add(gain, loss);
            return total == 0
                ? 0
                : Multiply(100, Divide(signed ? Subtract(gain, loss) : gain, total)); // NOSONAR: Exact zero is the native flat convention.
        }
        (double Gain, double Loss) Seed(int from, double previous)
        {
            double gains = 0,
                losses = 0;
            for (var j = from; j < from + period; j++)
            {
                var difference = Subtract(prices[j], previous);
                previous = prices[j];
                if (difference < 0)
                    losses = Subtract(losses, difference);
                else
                    gains = Add(gains, difference);
            }
            return (Divide(gains, period), Divide(losses, period));
        }
        var result = new List<double>();
        var seedEnd = origin + period;
        var (gain, loss) = Seed(origin + 1, prices[origin]);
        if (metastock && unstable == 0)
        {
            result.Add(Publish(gain, loss));
            if (seedEnd == last)
                return (result.ToArray(), new(first, first + result.Count));
            (gain, loss) = Seed(origin + 2, prices[origin]);
            seedEnd++;
        }
        if (seedEnd >= first)
            result.Add(Publish(gain, loss));
        for (var i = seedEnd + 1; i <= last; i++)
        {
            var difference = Subtract(prices[i], prices[i - 1]);
            gain = Divide(Add(Multiply(gain, period - 1), Math.Max(difference, 0)), period);
            loss = Divide(Add(Multiply(loss, period - 1), Math.Max(-difference, 0)), period);
            if (i >= first)
                result.Add(Publish(gain, loss));
        }
        return (result.ToArray(), new(first, first + result.Count));
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([0, 1, 1, 0, -2, 3, 3, 3, 2, 7, 1, -1, 0, 2, 2, 2, 2, 2, 2, 2]);
}
