using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// Six zero-seeded smoothers feed an exact cubic blend. The difference form
// retains cancellation even when the legacy coefficient's cube overflows.
internal sealed class CoralTrendWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly BigInteger _gain, _carry, _coefficient;
    private readonly BigInteger[] _stages = new BigInteger[6], _scratch = new BigInteger[6];
    private BigInteger _previousSpread;
    internal CoralTrendWindow(int length, double cd)
    {
        if (double.IsNaN(cd) || double.IsInfinity(cd)) throw new ArgumentOutOfRangeException(nameof(cd));
        var gain = 4d / (Math.Max(1, length) + 3d);
        _gain = ExactVarianceWindow.Units(gain); _carry = ExactVarianceWindow.Units(1 - gain); _coefficient = ExactVarianceWindow.Units(cd);
    }
    internal (double Value, Signal Signal) Next(double price, bool final)
    {
        var input = ExactVarianceWindow.Units(price); var next = _scratch;
        for (var i = 0; i < next.Length; i++)
        {
            var current = i == 0 ? input : next[i - 1];
            var incoming = RocBankValue.RoundUnits(current * _gain, Unit);
            var retained = RocBankValue.RoundUnits(_stages[i] * _carry, Unit);
            next[i] = RocBankValue.RoundUnits(incoming + retained, BigInteger.One);
        }
        var firstDifference = next[2] - next[3];
        var secondDifference = next[2] - 2 * next[3] + next[4];
        var thirdDifference = next[2] - 3 * next[3] + 3 * next[4] - next[5];
        var square = _coefficient * _coefficient; var unitSquare = Unit * Unit; var unitCube = unitSquare * Unit;
        var blend = next[2] * unitCube + 3 * _coefficient * firstDifference * unitSquare
            + 3 * square * secondDifference * Unit + square * _coefficient * thirdDifference;
        var line = RocBankValue.RoundUnits(blend, unitCube); var spread = input - line;
        var signal = spread.Sign > 0 && spread > _previousSpread ? Signal.StrongBuy : spread.Sign < 0 && spread < _previousSpread ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { Array.Copy(next, _stages, next.Length); _previousSpread = spread; }
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), signal);
    }
    internal void Reset() { Array.Clear(_stages, 0, _stages.Length); _previousSpread = default; }
    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, double cd)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var window = new CoralTrendWindow(length, cd);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true).Value;
    }
}
