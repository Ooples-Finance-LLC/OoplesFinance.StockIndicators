using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ElementaryMathComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        Create("Add", CandleArithmeticOperation.Add),
        Create("Sub", CandleArithmeticOperation.Subtract),
        Create("Mult", CandleArithmeticOperation.Multiply),
        Create("Div", CandleArithmeticOperation.Divide),
        Create("Ceil", rounding: PriceRoundingOperation.Ceiling),
        Create("Floor", rounding: PriceRoundingOperation.Floor),
        Create("Sqrt", rounding: PriceRoundingOperation.SquareRoot),
    ];

    private static ComparisonPair Create(
        string name,
        CandleArithmeticOperation? arithmetic = null,
        PriceRoundingOperation? rounding = null
    ) =>
        new(
            "TaLib.Functions." + name,
            arithmetic.HasValue ? "CandleArithmetic" : "PriceRoundingTransform",
            (d, _) => Native(name, d),
            (d, _) => Owned(d, arithmetic, rounding),
            (d, _) => Reference(d, arithmetic, rounding),
            MinimumInputCount: 2
        );

    private static ComparisonSeries Native(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        var code = Call(name, data.Closes, data.Opens, System.Range.All, values, out var range);
        if (
            code != TALib.Core.RetCode.Success
            || range.GetOffsetAndLength(data.Count) != (0, data.Count)
        )
            throw new InvalidOperationException(
                name + " returned an unexpected code or range: " + code
            );
        // Nonfinite native values remain present, so the verifier cannot hide them as missing.
        return VolumePriceComparison.Mask(values.Select(v => (double?)v).ToArray());
    }

    internal static TALib.Core.RetCode Call<T>(
        string name,
        T[] left,
        T[] right,
        System.Range range,
        T[] output,
        out System.Range outputRange
    )
        where T : IFloatingPointIeee754<T> =>
        name switch
        {
            "Add" => Functions.Add<T>(left, right, range, output, out outputRange),
            "Sub" => Functions.Sub<T>(left, right, range, output, out outputRange),
            "Mult" => Functions.Mult<T>(left, right, range, output, out outputRange),
            "Div" => Functions.Div<T>(left, right, range, output, out outputRange),
            "Ceil" => Functions.Ceil<T>(left, range, output, out outputRange),
            "Floor" => Functions.Floor<T>(left, range, output, out outputRange),
            "Sqrt" => Functions.Sqrt<T>(left, range, output, out outputRange),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static ComparisonSeries Owned(
        CompetitorData data,
        CandleArithmeticOperation? arithmetic,
        PriceRoundingOperation? rounding
    )
    {
        IIndicator indicator = arithmetic.HasValue
            ? new CandleArithmetic(arithmetic.Value)
            : new PriceRoundingTransform(rounding!.Value);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Outputs[0]].ToArray();
        var flags = run[indicator.Outputs[1]].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        CandleArithmeticOperation? arithmetic,
        PriceRoundingOperation? rounding
    )
    {
        var result = new double?[data.Count];
        for (var i = 0; i < data.Count; i++)
            result[i] = ReferenceValue(
                Units(data.Closes[i]),
                Units(data.Opens[i]),
                arithmetic,
                rounding
            );
        return VolumePriceComparison.Mask(result);
    }

    private static double? ReferenceValue(
        BigInteger a,
        BigInteger b,
        CandleArithmeticOperation? arithmetic,
        PriceRoundingOperation? rounding
    )
    {
        if (arithmetic.HasValue)
        {
            if (arithmetic == CandleArithmeticOperation.Divide && b.IsZero)
                return null;
            return arithmetic switch
            {
                CandleArithmeticOperation.Add => Round(a + b, Grid),
                CandleArithmeticOperation.Subtract => Round(a - b, Grid),
                CandleArithmeticOperation.Multiply => Round(a * b, Grid * Grid),
                _ => Round(a, b),
            };
        }
        if (rounding == PriceRoundingOperation.SquareRoot)
            return a.Sign >= 0 ? DispersionReferenceArithmetic.Sqrt(a, Grid) : null;
        var integer = BigInteger.DivRem(a, Grid, out var remainder);
        if (rounding == PriceRoundingOperation.Ceiling && remainder.Sign > 0)
            integer++;
        else if (rounding == PriceRoundingOperation.Floor && remainder.Sign < 0)
            integer--;
        return Round(integer, BigInteger.One);
    }

    internal static CompetitorData Fixture(string shape, int count, bool squareRoot = false)
    {
        var close = ComparisonVerifier.Fixture(shape, count).Closes;
        if (squareRoot)
            close = close.Select(Math.Abs).ToArray();
        // Nonzero, signed, independent right operands; undefined native requests are
        // covered directly, not coerced into comparable finite outputs.
        var open = Enumerable
            .Range(0, count)
            .Select(i => i % 13 == 6 ? .25 : (i % 13) - 6d)
            .ToArray();
        return FromOperands(close, open);
    }

    internal static CompetitorData FromOperands(double[] left, double[] right) =>
        CompetitorData.FromOhlcv(
            right,
            left.Zip(right, Math.Max).ToArray(),
            left.Zip(right, Math.Min).ToArray(),
            left,
            Enumerable.Repeat(1d, left.Length).ToArray()
        );

    internal static CompetitorData BoundaryFixture(bool squareRoot) =>
        FromOperands(
            squareRoot
                ?
                [
                    0,
                    double.Epsilon,
                    2 * double.Epsilon,
                    .25,
                    2,
                    Math.BitDecrement(4),
                    4,
                    Math.BitIncrement(4),
                ]
                :
                [
                    -2.5,
                    Math.BitDecrement(-2),
                    -double.Epsilon,
                    0,
                    double.Epsilon,
                    Math.BitDecrement(2),
                    2,
                    Math.BitIncrement(2),
                ],
            [1, -1, 2, -.5, 4, -2, .5, -4]
        );
}
