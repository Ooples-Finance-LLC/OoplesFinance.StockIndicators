using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CircularComparison
{
    internal static readonly IndicatorErrorBudget Budget = new(0, 4e-15, true);
    internal static readonly string[] Names = ["Sin", "Cos", "Tan", "Asin", "Acos", "Atan"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(
            (name, index) =>
                new ComparisonPair(
                    "TaLib.Functions." + name,
                    nameof(PriceCircularTransform),
                    (d, _) => Native(name, d),
                    (d, _) => Owned((PriceCircularOperation)index, d),
                    (d, _) => Reference(name, d),
                    MinimumInputCount: 2,
                    ErrorBudget: Budget
                )
        )
        .ToArray();

    internal static TALib.Core.RetCode Call<T>(
        string name,
        T[] input,
        System.Range range,
        T[] output,
        out System.Range outputRange
    )
        where T : IFloatingPointIeee754<T> =>
        name switch
        {
            "Sin" => Functions.Sin<T>(input, range, output, out outputRange),
            "Cos" => Functions.Cos<T>(input, range, output, out outputRange),
            "Tan" => Functions.Tan<T>(input, range, output, out outputRange),
            "Asin" => Functions.Asin<T>(input, range, output, out outputRange),
            "Acos" => Functions.Acos<T>(input, range, output, out outputRange),
            "Atan" => Functions.Atan<T>(input, range, output, out outputRange),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static ComparisonSeries Native(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        var code = Call(name, data.Closes, System.Range.All, values, out var range);
        if (
            code != TALib.Core.RetCode.Success
            || range.GetOffsetAndLength(data.Count) != (0, data.Count)
        )
            throw new InvalidOperationException(
                name + " returned an unexpected range or status: " + code
            );
        return VolumePriceComparison.Mask(values.Select(v => (double?)v).ToArray());
    }

    private static ComparisonSeries Owned(PriceCircularOperation operation, CompetitorData data)
    {
        var indicator = new PriceCircularTransform(operation);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    private static ComparisonSeries Reference(string name, CompetitorData data) =>
        VolumePriceComparison.Mask(data.Closes.Select(x => ReferenceValue(name, x)).ToArray());

    // Independent identity pi=4*(atan(1/2)+atan(1/3)), modular reduction,
    // and backwards polynomial evaluation. No runtime trigonometric calls.
    private const int Bits = 3072,
        PolynomialBits = 384;
    private static readonly BigInteger Scale = BigInteger.One << Bits;
    private static readonly BigInteger Unit = BigInteger.One << PolynomialBits;
    private static readonly BigInteger Pi = 4 * (ReciprocalAngle(2) + ReciprocalAngle(3));

    private static BigInteger ReciprocalAngle(int divisor)
    {
        BigInteger polynomial = 0;
        for (var k = Bits; k >= 0; k--)
            polynomial = Scale / (2 * k + 1) - polynomial / (divisor * divisor);
        return polynomial / divisor;
    }

    private static BigInteger Root(BigInteger n)
    {
        if (n.IsZero)
            return 0;
        var a = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            var b = (a + n / a) / 2;
            if (b >= a)
                return a;
            a = b;
        }
    }

    private static BigInteger Angle(BigInteger x)
    {
        if (x.Sign < 0)
            return -Angle(-x);
        if (x > Scale)
            return Pi / 2 - Angle(Scale * Scale / x);
        if (x < (Scale >> 28))
            return x;
        var shifted = x > Scale / 2;
        if (shifted)
            x = (x - Scale) * Scale / (x + Scale);
        var z = x >> (Bits - PolynomialBits);
        var square = z * z / Unit;
        BigInteger polynomial = 0;
        for (var k = 240; k >= 0; k--)
            polynomial = Unit / (2 * k + 1) - polynomial * square / Unit;
        return (shifted ? Pi / 4 : 0) + ((z * polynomial / Unit) << (Bits - PolynomialBits));
    }

    private static (BigInteger Sin, BigInteger Cos) Circular(BigInteger x)
    {
        var phase = x % (2 * Pi);
        if (phase.Sign < 0)
            phase += 2 * Pi;
        var quadrant = (int)(phase / (Pi / 2));
        var z = phase - quadrant * (Pi / 2);
        var reflected = z > Pi / 4;
        if (reflected)
            z = Pi / 2 - z;
        BigInteger sine,
            cosine;
        if (z < (Scale >> 28))
        {
            sine = z;
            cosine = Scale;
        }
        else
        {
            var argument = z >> (Bits - PolynomialBits);
            var square = argument * argument / Unit;
            BigInteger sp = Unit,
                cp = Unit;
            for (var k = 64; k >= 1; k--)
            {
                sp = Unit - sp * square / (Unit * (2 * k) * (2 * k + 1));
                cp = Unit - cp * square / (Unit * (2 * k - 1) * (2 * k));
            }
            sine = (argument * sp / Unit) << (Bits - PolynomialBits);
            cosine = cp << (Bits - PolynomialBits);
        }
        if (reflected)
            (sine, cosine) = (cosine, sine);
        return quadrant switch
        {
            0 => (sine, cosine),
            1 => (cosine, -sine),
            2 => (-sine, -cosine),
            _ => (-cosine, sine),
        };
    }

    internal static double? ReferenceValue(string name, double value)
    {
        if (name is "Asin" or "Acos" && (value < -1 || value > 1))
            return null;
        var x = Units(value) * Scale / Grid;
        if (name is "Sin" or "Cos" or "Tan")
        {
            var (s, c) = Circular(x);
            return name == "Tan" ? Round(s, c) : Round(name == "Sin" ? s : c, Scale);
        }
        if (name == "Atan")
            return Round(Angle(x), Scale);
        var root = Root(Scale * Scale - x * x);
        var angle = root.IsZero ? x.Sign * (Pi / 2) : Angle(x * Scale / root);
        return Round(name == "Asin" ? angle : Pi / 2 - angle, Scale);
    }

    internal static CompetitorData Fixture(string name, string shape, int count)
    {
        var values = ComparisonVerifier.Fixture(shape, count).Closes;
        return CompetitorData.FromCloses(
            values.Select(x => name is "Asin" or "Acos" ? x / (1 + Math.Abs(x)) : x).ToArray()
        );
    }

    internal static CompetitorData BoundaryFixture(string name) =>
        CompetitorData.FromCloses(
            name is "Asin" or "Acos"
                ?
                [
                    -1,
                    Math.BitIncrement(-1),
                    -.5,
                    -double.Epsilon,
                    0,
                    double.Epsilon,
                    .5,
                    Math.BitDecrement(1),
                    1,
                ]
                :
                [
                    -1e28,
                    -Math.PI,
                    Math.BitDecrement(-Math.PI / 2),
                    -Math.PI / 2,
                    -double.Epsilon,
                    0,
                    double.Epsilon,
                    Math.BitDecrement(Math.PI / 2),
                    Math.PI / 2,
                    Math.BitIncrement(Math.PI / 2),
                    Math.PI,
                    1e20,
                    1e28,
                ]
        );
}
