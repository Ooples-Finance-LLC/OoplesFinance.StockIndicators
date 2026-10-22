#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedSpearmanState : IMultiOutputState, IDisposable
    {
        private readonly int windowLimit;
        private readonly double[] window, rankY;
        private readonly List<double> sortedWindow;
        private readonly Dictionary<double, double> rankByValue;
        private readonly SimpleMovingAverageSmoother? _signal;
        private int windowCount, windowStart;
        public GeneratedSpearmanState(int length, int signalLength, int count)
        {
            // No expiry beyond the complete batch is observable; bound storage.
            windowLimit = Math.Max(1, Math.Min(Math.Max(1, length), count));
            window = new double[windowLimit]; rankY = new double[windowLimit];
            sortedWindow = new(windowLimit); rankByValue = new(windowLimit);
            if (Math.Max(1, signalLength) <= count) _signal = new(signalLength);
        }
        private static void InsertSorted(List<double> list, double value)
        {
            var index = list.BinarySearch(value);
            if (index < 0)
            {
                index = ~index;
            }

            list.Insert(index, value);
        }

        private static void RemoveSorted(List<double> list, double value)
        {
            var index = list.BinarySearch(value);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }

        private static double CalculateSpearman(
            IReadOnlyList<double> sorted,
            double[] windowValues,
            int startIndex,
            int count,
            Dictionary<double, double> rankByValue,
            double[] rankY)
        {
            if (count <= 1)
            {
                return 0;
            }

            rankByValue.Clear();
            double sumY = 0;
            double sumY2 = 0;
            var rank = 1;
            for (var i = 0; i < count; )
            {
                var value = sorted[i];
                var j = i + 1;
                while (j < count && sorted[j] == value)
                {
                    j++;
                }

                var span = j - i;
                var avgRank = (rank + (rank + span - 1)) / 2.0;
                rankByValue[value] = avgRank;
                sumY += avgRank * span;
                sumY2 += avgRank * avgRank * span;
                for (var k = i; k < j; k++)
                {
                    rankY[k] = avgRank;
                }

                rank += span;
                i = j;
            }

            double sumXY = 0;
            for (var i = 0; i < count; i++)
            {
                var valueX = windowValues[(startIndex + i) % windowValues.Length];
                var rankX = rankByValue[valueX];
                sumXY += rankX * rankY[i];
            }

            var n = (double)count;
            var numerator = (n * sumXY) - (sumY * sumY);
            var denomLeft = (n * sumY2) - (sumY * sumY);
            var denomRight = (n * sumY2) - (sumY * sumY);
            var denom = Math.Sqrt(denomLeft * denomRight);
            return denom != 0 ? numerator / denom : 0;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            var currentValue = bar.Close;
            if (windowCount == windowLimit)
            {
                var removed = window[windowStart];
                window[windowStart] = currentValue;
                windowStart = (windowStart + 1) % windowLimit;
                RemoveSorted(sortedWindow, removed);
            }
            else
            {
                window[(windowStart + windowCount) % windowLimit] = currentValue;
                windowCount++;
            }

            InsertSorted(sortedWindow, currentValue);

            var sc = CalculateSpearman(sortedWindow, window, windowStart, windowCount, rankByValue, rankY);
            sc = IsValueNullOrInfinity(sc) ? 0 : sc;
            outputs[0] = (double)sc * 100;
            outputs[1] = _signal?.Next(outputs[0], true) ?? 0;
        }
        public void Reset()
        {
            windowCount = windowStart = 0; sortedWindow.Clear(); rankByValue.Clear(); _signal?.Reset();
        }
        public void Dispose() => _signal?.Dispose();
    }
}
#endif
