using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TranscendentalComparison
{
    internal static readonly IndicatorErrorBudget Budget = new(0, 4e-15, true);
    internal static readonly string[] Names = ["Ln", "Log10", "Exp", "Sinh", "Cosh", "Tanh"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(
            (name, index) =>
                new ComparisonPair(
                    "TaLib.Functions." + name,
                    nameof(PriceTranscendentalTransform),
                    (d, _) => Native(name, d),
                    (d, _) => Owned((PriceTranscendentalOperation)index, d),
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
            "Ln" => Functions.Ln<T>(input, range, output, out outputRange),
            "Log10" => Functions.Log10<T>(input, range, output, out outputRange),
            "Exp" => Functions.Exp<T>(input, range, output, out outputRange),
            "Sinh" => Functions.Sinh<T>(input, range, output, out outputRange),
            "Cosh" => Functions.Cosh<T>(input, range, output, out outputRange),
            "Tanh" => Functions.Tanh<T>(input, range, output, out outputRange),
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

    private static ComparisonSeries Owned(
        PriceTranscendentalOperation operation,
        CompetitorData data
    )
    {
        var indicator = new PriceTranscendentalTransform(operation);
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

    // Independent 256-bit Horner exponential polynomial (validation uses a
    // forward 192-bit recurrence), with range reduction and eight squarings.
    private static readonly BigInteger Scale = BigInteger.One << 256;
    private static readonly BigInteger LogTwo = LogSeries(Scale / 3);
    private static readonly BigInteger LogTen = LogFixed(10 * Grid);

    private static BigInteger LogSeries(BigInteger z)
    {
        var square = z * z / Scale;
        var polynomial = Scale / 241;
        for (var n = 119; n >= 0; n--)
            polynomial = Scale / (2 * n + 1) + polynomial * square / Scale;
        return 2 * z * polynomial / Scale;
    }

    private static BigInteger LogFixed(BigInteger units)
    {
        var exponent = (int)units.GetBitLength() - 1075;
        var numerator = exponent < 0 ? units << -exponent : units;
        var denominator = exponent > 0 ? Grid << exponent : Grid;
        return LogSeries((numerator - denominator) * Scale / (numerator + denominator))
            + exponent * LogTwo;
    }

    internal static double? ReferenceValue(string name, double x)
    {
        if (name is "Ln" or "Log10")
            return x <= 0 ? null : Round(LogFixed(Units(x)), name == "Ln" ? Scale : LogTen);
        var magnitude = Math.Abs(x);
        if (name is "Sinh" or "Tanh" && magnitude < Math.ScaleB(1, -28))
            return x;
        if (name == "Tanh" && magnitude >= 20)
            return Math.Sign(x);
        if (magnitude > 800)
            return name == "Exp" && x < 0 ? 0
                : name == "Sinh" && x < 0 ? double.NegativeInfinity
                : double.PositiveInfinity;
        var argument = BigInteger.Abs(Units(x)) * Scale / (256 * Grid);
        var exponential = Scale;
        for (var n = 160; n >= 1; n--)
            exponential = Scale + argument * exponential / (Scale * n);
        for (var n = 0; n < 8; n++)
            exponential = exponential * exponential / Scale;
        if (name == "Exp")
            return x < 0 ? Round(Scale, exponential) : Round(exponential, Scale);
        var square = exponential * exponential;
        var unit = Scale * Scale;
        return name switch
        {
            "Sinh" => Round(Math.Sign(x) * (square - unit), 2 * exponential * Scale),
            "Cosh" => Round(square + unit, 2 * exponential * Scale),
            "Tanh" => Round(Math.Sign(x) * (square - unit), square + unit),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    internal static CompetitorData Fixture(string name, string shape, int count)
    {
        var values = ComparisonVerifier.Fixture(shape, count).Closes.Select(x => x / 64).ToArray();
        if (name is "Ln" or "Log10")
            values = values.Select(x => Math.Abs(x) + .125).ToArray();
        return CompetitorData.FromCloses(values);
    }

    internal static CompetitorData BoundaryFixture(string name) =>
        CompetitorData.FromCloses(
            name switch
            {
                "Ln" or "Log10" =>
                [
                    double.Epsilon,
                    Math.ScaleB(1, -100),
                    .1,
                    Math.BitDecrement(1),
                    1,
                    Math.BitIncrement(1),
                    10,
                    100,
                ],
                "Exp" => [-746, -745, -744, -1, -double.Epsilon, 0, double.Epsilon, 1, 709],
                "Sinh" or "Cosh" =>
                [
                    -710,
                    -1,
                    -double.Epsilon,
                    0,
                    double.Epsilon,
                    Math.ScaleB(1, -28),
                    1,
                    710,
                ],
                _ => [-20, -1, -double.Epsilon, 0, double.Epsilon, Math.ScaleB(1, -28), 1, 20],
            }
        );
}
