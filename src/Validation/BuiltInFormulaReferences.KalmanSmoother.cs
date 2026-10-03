using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KalmanSmootherOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => KalmanSmootherValues(bars, Integer(indicator.CreateOptions(), "Length", 200));
    internal static Dictionary<string, double[]> KalmanSmootherValues(IReadOnlyList<Bar> bars, int length)
    {
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var q = new ReferenceFraction(Math.Max(1, length)) / new ReferenceFraction(10000); var r = two * q;
        (ReferenceFraction A, ReferenceFraction B) Add((ReferenceFraction A, ReferenceFraction B) x, (ReferenceFraction A, ReferenceFraction B) y) => (x.A + y.A, x.B + y.B);
        (ReferenceFraction A, ReferenceFraction B) Mul((ReferenceFraction A, ReferenceFraction B) x, (ReferenceFraction A, ReferenceFraction B) y) => (x.A * y.A + r * x.B * y.B, x.A * y.B + x.B * y.A);
        double Publish((ReferenceFraction A, ReferenceFraction B) x)
        {
            if (x.B.Sign == 0) return x.A.ToDouble();
            if (x.A.Sign != x.B.Sign && (x.A * x.A).CompareTo(r * x.B * x.B) == 0) return 0;
            for (var bits = 80; ; bits = checked(bits * 2))
            {
                var root = UltimateReferenceArithmetic.Root(r, bits);
                var low = (x.A + x.B * (x.B.Sign > 0 ? root.Low : root.High)).ToDouble();
                var high = (x.A + x.B * (x.B.Sign > 0 ? root.High : root.Low)).ToDouble();
                if (low == high) return low;
            }
        }
        var output = new double[bars.Count];
        if (bars.Count == 0) return new() { ["Ks"] = output };
        var previous = (A: ReferenceFraction.FromDouble(bars[0].Close), B: zero); var older = previous;
        output[0] = bars[0].Close;
        // Eliminate velocity: y[n]=(2-q-g)y[n-1]+(g-1)y[n-2]+(q+g)x[n]-g*x[n-1].
        for (var i = 1; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close); var priorPrice = ReferenceFraction.FromDouble(bars[i - 1].Close);
            var next = Add(Add(Mul((two - q, zero - one), previous), Mul((zero - one, one), older)), (q * current, current - priorPrice));
            output[i] = Publish(next); older = previous; previous = next;
        }
        return new() { ["Ks"] = output };
    }
}
