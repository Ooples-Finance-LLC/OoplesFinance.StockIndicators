using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Full-history arrays model native scalar rounding and startup independently of
// production's streaming engines. No TA-Lib function is called by this oracle.
internal static class ClassicNativeReference
{
    internal static T[] Packed<T>(
        T[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var start = (int)
            Math.Min(
                int.MaxValue,
                Math.Max(requested, ClassicAverageComparison.First(p, method, suppression))
            );
        if (start > end)
            return [];
        if (p == 1)
            return input.Skip(start).Take(end - start + 1).ToArray();
        if (method == ClassicAverageMethod.Kama)
            return TaAdaptiveComparison.NativePacked(input, p, suppression, requested, end);
        if (method == ClassicAverageMethod.Mama)
            return DelayedPhaseComparison.NativePacked(input, .5, .05, suppression, requested, end)[
                0
            ];
        if (method == ClassicAverageMethod.T3)
            return Tillson(input, p, suppression, start, end);
        if (
            method
            is ClassicAverageMethod.Ema
                or ClassicAverageMethod.Dema
                or ClassicAverageMethod.Tema
        )
        {
            var order =
                method == ClassicAverageMethod.Ema ? 1
                : method == ClassicAverageMethod.Dema ? 2
                : 3;
            var delay = p - 1 + suppression;
            var layers = new T[order][];
            layers[0] = Exponential(input, p, first, suppression, start - (order - 1) * delay, end);
            for (var j = 1; j < order; j++)
                layers[j] = Exponential(
                    layers[j - 1],
                    p,
                    first,
                    suppression,
                    delay,
                    layers[j - 1].Length - 1
                );
            var result = new T[layers[^1].Length];
            for (var i = 0; i < result.Length; i++)
                result[i] =
                    order == 1 ? layers[0][i]
                    : order == 2 ? T.CreateChecked(2) * layers[0][i + delay] - layers[1][i]
                    : layers[2][i]
                        + (
                            T.CreateChecked(3) * layers[0][i + 2 * delay]
                            - T.CreateChecked(3) * layers[1][i + delay]
                        );
            return result;
        }
        return Window(input, p, method, start, end);
    }

    private static T[] Exponential<T>(
        T[] input,
        int p,
        bool first,
        int suppression,
        int start,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var alpha = T.CreateChecked(2) / (T.CreateChecked(p) + T.One);
        var value = T.Zero;
        int cursor;
        if (first)
        {
            value = input[0];
            cursor = 1;
        }
        else
        {
            cursor = start - (p - 1 + suppression);
            for (var j = 0; j < p; j++)
                value += input[cursor++];
            value /= T.CreateChecked(p);
        }
        var output = new T[end - start + 1];
        for (; cursor <= end; cursor++)
        {
            value = (input[cursor] - value) * alpha + value;
            if (cursor >= start)
                output[cursor - start] = value;
        }
        // A seed at start has no recurrence iteration to write it.
        if ((!first && suppression == 0) || (first && start == 0))
        {
            var seed = T.Zero;
            if (first)
                seed = input[0];
            else
                for (var j = start - p + 1; j <= start; j++)
                    seed += input[j];
            output[0] = first ? seed : seed / T.CreateChecked(p);
        }
        return output;
    }

    private static T[] Window<T>(T[] input, int p, ClassicAverageMethod method, int start, int end)
        where T : IFloatingPointIeee754<T>
    {
        var output = new T[end - start + 1];
        var begin = start - p + 1;
        if (method == ClassicAverageMethod.Sma)
        {
            var sum = T.Zero;
            for (var i = begin; i < start; i++)
                sum += input[i];
            for (var i = start; i <= end; i++)
            {
                sum += input[i];
                var value = sum;
                sum -= input[i - p + 1];
                output[i - start] = value / T.CreateChecked(p);
            }
            return output;
        }
        if (method == ClassicAverageMethod.Wma)
        {
            var sum = T.Zero;
            var weighted = T.Zero;
            for (var i = begin; i < start; i++)
            {
                sum += input[i];
                weighted += input[i] * T.CreateChecked(i - begin + 1);
            }
            var expired = T.Zero;
            var divisor = T.CreateChecked(unchecked(p * (p + 1)) >> 1);
            for (var i = start; i <= end; i++)
            {
                sum += input[i];
                sum -= expired;
                weighted += input[i] * T.CreateChecked(p);
                expired = input[i - p + 1];
                output[i - start] = weighted / divisor;
                weighted -= sum;
            }
            return output;
        }
        // Initial reverse/forward prefix sums, followed by separate left/right
        // history arrays, preserve native odd/even rounding without its ring indices.
        var half = p / 2;
        var odd = p % 2 != 0;
        var midpoint = begin + half - (odd ? 0 : 1);
        var h = T.CreateChecked(half);
        var factor = T.One / ((odd ? h + T.One : h) * (h + T.One));
        var left = new T[end - start + 1];
        var right = new T[left.Length];
        var numerator = new T[left.Length];
        for (var i = midpoint; i >= begin; i--)
        {
            left[0] += input[i];
            numerator[0] += left[0];
        }
        for (var i = midpoint + 1; i <= start; i++)
        {
            right[0] += input[i];
            numerator[0] += right[0];
        }
        output[0] = numerator[0] * factor;
        for (var i = start + 1; i <= end; i++)
        {
            var j = i - start;
            var crossing = input[i - half];
            numerator[j] = numerator[j - 1] - left[j - 1];
            left[j] = (left[j - 1] - input[i - p]) + crossing;
            var remainder = right[j - 1] - crossing;
            numerator[j] += odd ? right[j - 1] : remainder;
            right[j] = remainder + input[i];
            numerator[j] += input[i];
            output[j] = numerator[j] * factor;
        }
        return output;
    }

    private static T[] Tillson<T>(T[] input, int p, int suppression, int start, int end)
        where T : IFloatingPointIeee754<T>
    {
        var begin = start - (6 * (p - 1) + suppression);
        var count = end - begin + 1;
        var layers = Enumerable.Range(0, 6).Select(_ => new T[count]).ToArray();
        var alpha = T.CreateChecked(2) / (T.CreateChecked(p) + T.One);
        var complement = T.One - alpha;
        for (var j = 0; j < 6; j++)
        {
            T Input(int i) => j == 0 ? input[begin + i] : layers[j - 1][i];
            var seedAt = (j + 1) * (p - 1);
            var sum = T.Zero;
            for (var i = j * (p - 1); i <= seedAt; i++)
                sum += Input(i);
            layers[j][seedAt] = sum / T.CreateChecked(p);
            for (var i = seedAt + 1; i < count; i++)
                layers[j][i] = alpha * Input(i) + complement * layers[j][i - 1];
        }
        var v = T.CreateChecked(.7);
        var square = v * v;
        var two = T.CreateChecked(2);
        var three = T.CreateChecked(3);
        var c1 = -T.One * square * v;
        var c2 = three * (square - c1);
        var c3 = -T.One * two * three * square - three * (v - c1);
        var c4 = T.One + three * v - c1 + three * square;
        return Enumerable
            .Range(start - begin, end - start + 1)
            .Select(i =>
                c1 * layers[5][i] + c2 * layers[4][i] + c3 * layers[3][i] + c4 * layers[2][i]
            )
            .ToArray();
    }
}
