using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ScalarHilbertPhaseTests
{
    private static BigInteger Units(double value) => ExactVarianceWindow.Units(value);
    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    private static void Check(HilbertPhaseState state, IntegerPhase reference)
    {
        Assert.Equal(reference.Smooth, state.SmoothPrice.RoundedUnits());
        Assert.Equal(reference.InPhase, state.InPhase.RoundedUnits());
        Assert.Equal(reference.Quadrature, state.Quadrature.RoundedUnits());
        Bits(reference.Period, state.Period);
        Bits(reference.Phase, state.Phase);
    }

    [Theory]
    [InlineData(6, false, false)]
    [InlineData(6, false, true)]
    [InlineData(12, true, false)]
    [InlineData(37, true, false)]
    public void RoundedStagesAndControlsMatchIntegerHistory(int start, bool retainPeriod, bool retainOlder)
    {
        var state = new HilbertPhaseState(start, retainPeriod, retainOlder);
        foreach (double scale in new[] { 1d, Math.ScaleB(1, -520), Math.ScaleB(1, 520),
                     Math.ScaleB(1, -1070), double.MaxValue / 8 })
        {
            state.Reset();
            var reference = new IntegerPhase(start, retainPeriod, retainOlder);
            for (int i = 0; i < 180; i++)
            {
                double price = (i % 17 == 0 ? -0d : Math.Sin(i * .37) + .125 * Math.Cos(i * .11)) * scale;
                state.Update(price);
                reference.Update(Units(price));
                Check(state, reference);
            }
        }
    }

    [Theory]
    [InlineData(6, false, false)]
    [InlineData(6, false, true)]
    [InlineData(12, true, false)]
    [InlineData(37, true, false)]
    public void LateExtendedInputAndResetKeepTheEntirePreviousHistory(int start, bool retainPeriod, bool retainOlder)
    {
        var state = new HilbertPhaseState(start, retainPeriod, retainOlder);
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            var reference = new IntegerPhase(start, retainPeriod, retainOlder);
            bool extended = false;
            for (int i = 0; i < 150; i++)
            {
                BigInteger price = i is 61 or 64 ? (Units(double.MaxValue) << 180) + 1
                    : i == 62 ? -(Units(double.MaxValue) << 181)
                    : Units((i * 13 % 101 - 50) / 8d);
                // Exercise double updates before escalation and the exact unit
                // bridge at/after it, without rounding a wide input first.
                if (i < 61) state.Update((i * 13 % 101 - 50) / 8d);
                else state.Update(price);
                reference.Update(price);
                Check(state, reference);
                extended |= !state.SmoothPrice.TryDouble(out _);
            }
            Assert.True(extended);
        }
    }

    [Fact]
    public void PublishedDyadicsAreIndependentSnapshotsAcrossUpdatesAndReset()
    {
        var state = new HilbertPhaseState(6, false, true);
        for (int i = 0; i < 90; i++) state.Update(Math.Sin(i * .31));
        var snapshot = state.InPhase;
        BigInteger saved = snapshot.RoundedUnits();
        Assert.NotEqual(BigInteger.Zero, saved);
        var changed = snapshot;
        changed.Multiply(3);
        Assert.NotEqual(saved, changed.RoundedUnits());
        Assert.Equal(saved, state.InPhase.RoundedUnits());
        state.Update(BigInteger.One << 3000);
        Assert.Equal(saved, snapshot.RoundedUnits());
        state.Reset();
        Assert.Equal(BigInteger.Zero, state.InPhase.RoundedUnits());
        Assert.Equal(BigInteger.Zero, state.Quadrature.RoundedUnits());
        Assert.Equal(BigInteger.Zero, state.SmoothPrice.RoundedUnits());
        Bits(0, state.Period);
        Bits(0, state.Phase);
        Assert.Equal(saved, snapshot.RoundedUnits());
    }

    // Independent padded-integer arithmetic with absolute-history indexing:
    // no JurikDyadic, floating certificates, scalar rings or state helpers.
    private sealed class IntegerPhase(int start, bool retainPeriod, bool retainOlder)
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private static readonly BigInteger Outer = Units(.0962), Inner = Units(.5769),
            NewWeight = Units(.2), OldWeight = Units(.8);
        private readonly List<BigInteger> _prices = [], _smooth = [], _detrended = [],
            _quadrature = [], _inphase = [];
        private BigInteger _i2, _q2, _real, _imaginary;
        private double _olderPeriod, _olderPhase;
        public BigInteger Smooth { get; private set; }
        public BigInteger InPhase { get; private set; }
        public BigInteger Quadrature { get; private set; }
        public double Period { get; private set; }
        public double Phase { get; private set; }
        private static BigInteger At(List<BigInteger> history, int i) => i < 0 ? 0 : history[i];
        private static BigInteger Round(BigInteger numerator, BigInteger denominator) =>
            RocBankValue.RoundUnits(numerator, denominator);
        private static BigInteger Blend(BigInteger value, BigInteger old) =>
            Round(value * NewWeight + old * OldWeight, Grid);
        private static double Ratio(BigInteger numerator, BigInteger denominator)
        {
            // Finite snapshots divide as binary64, including zero/negative.
            // Extended dyadics use the original exact ratio's canonical zero.
            if (numerator.IsZero && BigInteger.Abs(denominator) > Units(double.MaxValue)) return 0;
            double ratio = ExactMeanAccumulator.ScaledRatio(numerator, BigInteger.Abs(denominator), 0);
            return denominator.Sign < 0 ? -ratio : ratio;
        }
        private static BigInteger Fir(BigInteger value, List<BigInteger> history, int index, double correction) =>
            Round(Round(Outer * value + Inner * At(history, index - 2)
                - Inner * At(history, index - 4) - Outer * At(history, index - 6), Grid)
                * Units(correction), Grid);

        public void Update(BigInteger price)
        {
            int index = _prices.Count;
            BigInteger smooth = 0, detrended = 0, inphase = 0, quadrature = 0;
            if (index >= start)
            {
                double correction = .075 * Period + .54;
                smooth = Round(4 * price + 3 * At(_prices, index - 1)
                    + 2 * At(_prices, index - 2) + At(_prices, index - 3), 10);
                detrended = Fir(smooth, _smooth, index, correction);
                quadrature = Fir(detrended, _detrended, index, correction);
                inphase = At(_detrended, index - 3);
                BigInteger ji = Fir(inphase, _inphase, index, correction),
                    jq = Fir(quadrature, _quadrature, index, correction);
                BigInteger i2 = Blend(Round(inphase - jq, 1), _i2),
                    q2 = Blend(Round(quadrature + ji, 1), _q2),
                    real = Blend(Round(i2 * _i2 + q2 * _q2, Grid), _real),
                    imaginary = Blend(Round(i2 * _q2 - q2 * _i2, Grid), _imaginary);
                double measured = imaginary.Sign != 0 && real.Sign != 0
                    ? 2 * Math.PI / Math.Atan(Ratio(imaginary, real))
                    : retainPeriod ? Period : retainOlder ? _olderPeriod : 0;
                measured = Math.Min(measured, 1.5 * Period);
                measured = Math.Max(measured, .67 * Period);
                measured = Math.Max(6, Math.Min(50, measured));
                double period = .2 * measured + .8 * Period;
                double phase = inphase.Sign != 0 ? Math.Atan(Ratio(quadrature, inphase)) * 180 / Math.PI
                    : retainOlder ? _olderPhase : 0;
                Smooth = smooth;
                InPhase = inphase;
                Quadrature = quadrature;
                _i2 = i2; _q2 = q2; _real = real; _imaginary = imaginary;
                _olderPeriod = Period; Period = period;
                _olderPhase = Phase; Phase = phase;
            }
            _prices.Add(price); _smooth.Add(smooth); _detrended.Add(detrended);
            _inphase.Add(inphase); _quadrature.Add(quadrature);
        }
    }
}
