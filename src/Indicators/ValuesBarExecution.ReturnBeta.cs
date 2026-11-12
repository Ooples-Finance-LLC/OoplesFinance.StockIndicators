#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillReturnBeta(Bar[] source, Node node, WindowReturnBeta indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        double[] market = node.Values[0], evaluation = node.Values[1];
        Bar latest = default;
        // Capture once into the final owned columns. Arithmetic and output
        // failures remain behind validation of every raw input field.
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            latest = source[i];
            if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
            if (owned is not null) owned[i] = latest;
            market[i] = PairStatisticsWindow.Select(latest, indicator.Market);
            evaluation[i] = PairStatisticsWindow.Select(latest, indicator.Evaluation);
        }
        try
        {
            if (TryReturnBetaGrids(market, evaluation, indicator.Period, cancellation,
                out var xGrid, out var yGrid))
                FillFixedReturnBeta(market, evaluation, indicator.Period, xGrid.Scale, yGrid.Scale, cancellation);
            else
            {
                // Certification may have replaced a prefix with rounded returns.
                // Restore all prices before the unchanged exact fallback reads them.
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    market[i] = PairStatisticsWindow.Select(source[i], indicator.Market);
                    evaluation[i] = PairStatisticsWindow.Select(source[i], indicator.Evaluation);
                }
                var window = new ReturnBetaWindow(indicator.Period, ReturnBetaSelection.Standard);
                for (int i = 0; i < market.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    window.Add(market[i], evaluation[i]);
                    var value = window.Beta(0, true);
                    market[i] = value ?? 0;
                    evaluation[i] = value.HasValue ? 1 : 0;
                }
            }
        }
        catch (Exception error) when (error is not OperationCanceledException) { node.CaptureFailure(error); }
        return latest;
    }

    private struct ReturnBetaGrid
    {
        private int _end;
        internal int Scale;
        internal bool HasValues;
        internal readonly int Width => _end - Scale;

        internal void Include(double value)
        {
            var factor = new ExactMeanAccumulator.ProductFactor(value);
            long coefficient = factor.Mantissa;
            if (coefficient == 0) return;
            ulong magnitude = (ulong)(coefficient < 0 ? -coefficient : coefficient);
            int end = factor.Scale + 64 - BitOperations.LeadingZeroCount(magnitude);
            if (!HasValues) { Scale = factor.Scale; _end = end; HasValues = true; }
            else { Scale = Math.Min(Scale, factor.Scale); _end = Math.Max(_end, end); }
        }
    }

    private static bool TryReturnBetaGrids(double[] market, double[] evaluation, int period,
        CancellationToken cancellation, out ReturnBetaGrid xGrid, out ReturnBetaGrid yGrid)
    {
        xGrid = yGrid = default;
        if (market.Length <= period) return false;
        double previousMarket = market[0], previousEvaluation = evaluation[0];
        for (int i = 1; i < market.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            double marketPrice = market[i], evaluationPrice = evaluation[i];
            var x = ReturnBetaWindow.RoundedReturnForBatch(marketPrice, previousMarket);
            var y = ReturnBetaWindow.RoundedReturnForBatch(evaluationPrice, previousEvaluation);
            previousMarket = marketPrice; previousEvaluation = evaluationPrice;
            if (x.UpperShift != 0 || y.UpperShift != 0) return false;
            xGrid.Include(x.Mantissa); yGrid.Include(y.Mantissa);
            market[i] = x.Mantissa; evaluation[i] = y.Mantissa;
        }
        if (!xGrid.HasValues || !yGrid.HasValues || xGrid.Width > 63 || yGrid.Width > 63) return false;
        ulong countSquared = (ulong)period * (ulong)period;
        static int CeilingLog2(ulong value) => value <= 1 ? 0 : 64 - BitOperations.LeadingZeroCount(value - 1);
        // For |x|<2^wx and |y|<2^wy, each denominator term is bounded by
        // n^2*2^(2wx), and the numerator difference by 2*n^2*2^(wx+wy).
        // Strict bounds below 2^127 cover every update and final intermediate.
        return 2 * xGrid.Width + CeilingLog2(countSquared) <= 127
            && xGrid.Width + yGrid.Width + CeilingLog2(2 * countSquared) <= 127;
    }

    private static long ReturnBetaCoefficient(double value, int scale)
    {
        var factor = new ExactMeanAccumulator.ProductFactor(value);
        return factor.Mantissa == 0 ? 0 : factor.Mantissa << (factor.Scale - scale);
    }

    private static void FillFixedReturnBeta(double[] market, double[] evaluation, int period,
        int xScale, int yScale, CancellationToken cancellation)
    {
        var history = new (long X, long Y)[period];
        Int128 sx = 0, sy = 0, sxx = 0, sxy = 0;
        int cursor = 0;
        market[0] = evaluation[0] = 0;
        for (int i = 1; i < market.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long x = ReturnBetaCoefficient(market[i], xScale);
            long y = ReturnBetaCoefficient(evaluation[i], yScale);
            if (i > period)
            {
                var old = history[cursor];
                sx -= old.X; sy -= old.Y;
                sxx -= (Int128)old.X * old.X; sxy -= (Int128)old.X * old.Y;
            }
            history[cursor] = (x, y);
            if (++cursor == period) cursor = 0;
            sx += x; sy += y; sxx += (Int128)x * x; sxy += (Int128)x * y;
            if (i < period) { market[i] = evaluation[i] = 0; continue; }
            Int128 denominator = period * sxx - sx * sx;
            Int128 numerator = period * sxy - sx * sy;
            double value = 0;
            if (denominator != 0 && numerator != 0)
            {
                UInt128 magnitude = (UInt128)(numerator < 0 ? -numerator : numerator);
                if (CertifiedCompactRatio.TryDivide(magnitude, (UInt128)denominator, yScale - xScale, out var rounded))
                    value = numerator < 0 ? -rounded : rounded;
                else
                    value = ExactMeanAccumulator.ScaledRatio((BigInteger)numerator, (BigInteger)denominator, yScale - xScale);
            }
            market[i] = value;
            evaluation[i] = 1;
        }
    }
}
#endif
