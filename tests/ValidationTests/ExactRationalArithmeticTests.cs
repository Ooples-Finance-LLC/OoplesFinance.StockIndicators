using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Validation;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactRationalArithmeticTests
{
    // Inspect the full value: publication alone hides errors below underflow or above overflow.
    private static ReferenceFraction Exact(Number value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var coefficient = (BigInteger)typeof(Number).GetField("_coefficient", flags)!.GetValue(value)!;
        var denominator = (BigInteger)typeof(Number).GetField("_denominator", flags)!.GetValue(value)!;
        var exponent = (int)typeof(Number).GetField("_exponent", flags)!.GetValue(value)!;
        if (denominator.IsZero) denominator = BigInteger.One;
        return exponent >= 0
            ? new ReferenceFraction(coefficient << exponent) / new ReferenceFraction(denominator)
            : new ReferenceFraction(coefficient) / new ReferenceFraction(denominator << -exponent);
    }
    private static void Equal(ReferenceFraction expected, Number actual)
    {
        Assert.Equal(0, expected.CompareTo(Exact(actual)));
        Assert.Equal(expected.ToDouble(), actual.Publish());
    }
    [Fact]
    public void OperatorsMatchIndependentFractionsAcrossSignsAndExponents()
    {
        double[] values = [0, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            1, -1, 0.1, -17.25, Math.ScaleB(1, -511), Math.ScaleB(1, 512)];
        foreach (var x in values) foreach (var y in values)
        {
            var a = Number.Of(x).Divide(45); var b = Number.Of(y).Divide(75);
            var ar = ReferenceFraction.FromDouble(x) / new ReferenceFraction(45);
            var br = ReferenceFraction.FromDouble(y) / new ReferenceFraction(75);
            Equal(ar + br, a + b); Equal(ar - br, a - b); Equal(ar * br, a * b);
            if (y != 0) Equal(ar / br, a.Divide(b));
        }
    }
    [Fact]
    public void CrossCancellationRetainsUnpublishableFactors()
    {
        // (2/3)*(9/10)=3/5 requires cancellation across both operand pairs.
        var left = Number.Of(2).Divide(3); var right = Number.Of(9).Divide(10);
        var threeFifths = new ReferenceFraction(3) / new ReferenceFraction(5);
        Equal(threeFifths, left * right); Equal(threeFifths, right * left);
        Equal(new ReferenceFraction(-3) / new ReferenceFraction(5), left * (default(Number) - right));
        var huge = Number.Of(double.MaxValue).Divide(Number.Of(double.Epsilon));
        var tiny = Number.Of(double.Epsilon).Divide(Number.Of(double.MaxValue));
        Equal(new ReferenceFraction(1), huge * tiny);
        Equal(new ReferenceFraction(1), huge.Divide(huge));
        Equal(new ReferenceFraction(1), tiny.Divide(tiny));
        Equal(new ReferenceFraction(0), huge - huge);
        Equal(new ReferenceFraction(0), default(Number) * huge);
        Equal(new ReferenceFraction(0), default(Number).Divide(tiny));
    }
    [Fact]
    public void AdditionReducesFactorsIntroducedByExponentAlignment()
    {
        var a = Number.Of(1).Divide(6).Round();
        var b = Number.Of(1).Divide(10);
        Equal(Exact(a) + Exact(b), a + b);
        Equal(Exact(a) - Exact(b), a - b);
        Equal(new ReferenceFraction(0), (a + b) - (b + a));
    }
    [Fact]
    public void SharedDenominatorsAndRepeatedWindowExpiryRemainExact()
    {
        Number sum = default; var expected = new ReferenceFraction(0);
        var queue = new Queue<Number>(); var references = new Queue<ReferenceFraction>();
        for (var i = 1; i <= 80; i++)
        {
            var value = Number.Of(i % 2 == 0 ? i : -i).Divide(3L * i);
            var reference = new ReferenceFraction(i % 2 == 0 ? i : -i) / new ReferenceFraction(3L * i);
            queue.Enqueue(value); references.Enqueue(reference); sum += value; expected += reference;
            if (queue.Count > 7) { sum -= queue.Dequeue(); expected -= references.Dequeue(); }
            Equal(expected, sum);
        }
    }
    [Fact]
    public void DivisionByZeroStillThrows() => Assert.Throws<DivideByZeroException>(() => Number.Of(1).Divide(default(Number)));
}
