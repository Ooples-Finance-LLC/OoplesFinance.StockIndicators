using FluentAssertions;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// A MovingAvgType has two ways of being computed: the span fast path in MovingAverageCore, taken when the
/// type is listed as a verified fast path, and the Calculate* batch otherwise. A caller names the type, not
/// the route, so the two have to be the same filter. MovingAvgType.HybridConvolutionFilter was not - the
/// fast path ran an error-weighted blend of an EMA and a WMA where the batch ran a raised cosine recursion -
/// and being on the verified list is what stopped anyone noticing.
/// </summary>
public sealed class MovingAvgTypeRouteParityTests
{
    private const int Length_ = 20;

    private const double Tolerance_ = 1e-8;

    private static List<TickerData> Walk()
    {
        var random = new Random(31);
        var bars = new List<TickerData>();
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        var last = 100d;

        for (var i = 0; i < 400; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new TickerData
            {
                Date = start.AddMinutes(i),
                Open = open,
                High = Math.Max(open, close) + random.NextDouble(),
                Low = Math.Max(0.01, Math.Min(open, close) - random.NextDouble()),
                Close = close,
                Volume = random.Next(1000, 400000)
            });
            last = close;
        }

        return bars;
    }

    [Fact]
    public void EveryVerifiedFastPathComputesWhatItsCalculationComputes()
    {
        var bars = Walk();
        var wrong = new List<string>();
        var compared = 0;

        foreach (var type in Enum.GetValues<MovingAvgType>())
        {
            var viaFastPath = new StockData(bars);
            var buffer = new double[viaFastPath.Count];
            if (!CalculationsHelper.TryComputeMovingAverage(viaFastPath, type, Length_,
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(viaFastPath.InputValues), buffer))
            {
                continue;
            }

            var viaCalculation = CalculationsHelper.GetMovingAverageListByCalculation(
                new StockData(bars), type, Length_);

            compared++;

            // A missing arm answers with an empty list rather than throwing, so the count is checked first:
            // TrueRangeAdjustedExponentialMovingAverage had no arm at all and read as zero values, not as a
            // disagreement about any of them.
            if (viaCalculation.Count != buffer.Length)
            {
                wrong.Add(type + ": fast path gives " + buffer.Length + " values, calculation gives "
                    + viaCalculation.Count);
                continue;
            }

            double worst = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                var difference = Math.Abs(buffer[i] - viaCalculation[i]);
                if (difference > worst)
                {
                    worst = difference;
                }
            }

            if (worst > Tolerance_)
            {
                wrong.Add(type + ": routes differ by " + worst.ToString("G6"));
            }
        }

        compared.Should().BeGreaterThan(40, "the verified fast paths are what this exists to check");
        wrong.Should().BeEmpty("a caller names a MovingAvgType, not a route; " + compared
            + " verified fast paths compared, " + wrong.Count + " disagree: " + string.Join(" | ", wrong));
    }
}
