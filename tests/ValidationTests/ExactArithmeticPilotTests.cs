using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Streaming;
using SeriesKey = OoplesFinance.StockIndicators.Streaming.SeriesKey;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactArithmeticPilotTests
{
    [Fact]
    public void SchaffPublishesItsMacdInTheNamedOutputSlot()
    {
        using var state = new SchaffTrendCycleShkState(fastLength: 3, slowLength: 5, cycleLength: 4);
        StreamingIndicatorStateResult result = default;
        var bars = MakeBars(new[] { 1d, 2, 3, 5 });
        foreach (var b in bars)
            result = state.Update(new OhlcvBar("PILOT", BarTimeframe.Minutes(1), b.Time, b.Time,
                b.Open, b.High, b.Low, b.Close, b.Volume, true), true, true);
        Assert.Equal(.75, result.Outputs!["Macd"]);
        Assert.Equal(50, result.Outputs["Stc"]);
    }
    [Fact]
    public async Task RsiMatchesExactWilderGainLossRatios()
    {
        var values = new[] { 100d, 102, 99, 101, 101, 98, 105, 97, 100 };
        var bars = MakeBars(values);
        var indicator = new Rsi(3);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var actual = run[indicator.Outputs[0]].ToArray();
        var gain = Rational.Zero; var loss = Rational.Zero;
        for (var i = 0; i < values.Length; i++)
        {
            var change = i == 0 ? 0 : values[i] - values[i - 1];
            gain = (gain * new Rational(2) + Rational.FromDouble(Math.Max(0, change))) / new Rational(3);
            loss = (loss * new Rational(2) + Rational.FromDouble(Math.Max(0, -change))) / new Rational(3);
            var expected = loss.ToDouble() == 0 ? 100 : (new Rational(100) * gain / (gain + loss)).ToDouble();
            Assert.InRange(Math.Abs(expected - actual[i]), 0, 2e-12);
        }
    }

    [Fact]
    public void RsmkMatchesAnIndependentConvergentLogSeries()
    {
        var primary = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var benchmark = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var context = new MultiSeriesContext(new SeriesStore());
        using var state = new RSMKIndicatorState(primary, benchmark, length: 1, smoothLength: 1);
        var p = new[] { 100d, 110, 121, 90 }; var b = new[] { 100d, 200, 100, 110 };
        var previous = Rational.Zero;
        for (var i = 0; i < p.Length; i++)
        {
            var time = DateTime.UnixEpoch.AddMinutes(i);
            OhlcvBar BarFor(string symbol, double value) => new(symbol, primary.Timeframe, time, time, value, value, value, value, 1, true);
            state.Update(context, benchmark, BarFor("BENCHMARK", b[i]), true, false);
            var actual = state.Update(context, primary, BarFor("PRIMARY", p[i]), true, false).Value;
            var logarithm = LogSeries(Rational.FromDouble(p[i]) / Rational.FromDouble(b[i]));
            var expected = i == 0 ? 0 : (new Rational(100) * (logarithm - previous)).ToDouble();
            Assert.InRange(Math.Abs(expected - actual), 0, 2e-12);
            previous = logarithm;
        }
    }

    [Fact]
    public async Task RecursiveFilterMatchesItsAnalyticImpulseResponse()
    {
        const int period = 10;
        var values = Enumerable.Range(0, 80).Select(i => i == 0 ? 1d : 0).ToArray();
        var indicator = new SuperSmoother(period);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(MakeBars(values))).ConfigureIndicators(indicator).BuildAsync();
        var actual = run[indicator.Outputs[0]].ToArray();
        var angle = Math.Sqrt(2) * Math.PI / period;
        var radius = Math.Exp(-angle);
        var gain = 1 - 2 * radius * Math.Cos(angle) + radius * radius;
        double Impulse(int i) => i < 0 ? 0 : Math.Pow(radius, i) * Math.Sin((i + 1) * angle) / Math.Sin(angle);
        for (var i = 0; i < actual.Length; i++)
            Assert.InRange(Math.Abs(actual[i] - gain / 2 * (Impulse(i) + Impulse(i - 1))), 0, 2e-14);
    }

    [Fact]
    public async Task MixedBuiltInAndCustomerAveragesMatchAnIndependentWindowFormula()
    {
        var values = new[] { 1d, 4, 2, 8, 3, 9, 5, 2, 7, 6 };
        var indicator = new AwesomeOscillator(5, new Sma(3), new PilotMean(5));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(MakeBars(values))).ConfigureIndicators(indicator).BuildAsync();
        var actual = run[indicator.Outputs[0]].ToArray();
        double Mean(int i, int period) => i + 1 < period ? 0 :
            (values.Skip(i - period + 1).Take(period).Select(Rational.FromDouble)
                .Aggregate(Rational.Zero, (a, b) => a + b) / new Rational(period)).ToDouble();
        for (var i = 0; i < actual.Length; i++) Assert.InRange(Math.Abs(actual[i] - (Mean(i, 3) - Mean(i, 5))), 0, 2e-14);
    }

    private static Bar[] MakeBars(IEnumerable<double> values) => values.Select((v, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();

    private static Rational LogSeries(Rational value)
    {
        // log(x)=2*(z+z^3/3+...), z=(x-1)/(x+1). All pilot x are in [1/2,2].
        // After 64 terms, the geometric tail is < 2*(1/3)^129/(129*(1-1/9)).
        var z = (value - new Rational(1)) / (value + new Rational(1));
        var power = z; var result = Rational.Zero;
        for (var i = 0; i < 64; i++) { result += power / new Rational(2 * i + 1); power = power * z * z; }
        return result * new Rational(2);
    }

    public sealed class PilotMean(int length = 5) : IndicatorBase, IMovingAverage, IIndicatorValidationContract
    {
        public IEnumerable<IndicatorValidationRule> ValidationRules => new[]
        { IndicatorValidationRule.Reference(0, bars => bars.Select((_, i) => i + 1 < length ? 0 :
            bars.Skip(i - length + 1).Take(length).Sum(b => b.Close) / length).ToArray()) };
        protected internal override object CreateState() => new State(length);
        private sealed class State(int length) : IIndicatorState
        {
            private readonly Queue<double> _window = new();
            public void Reset() => _window.Clear();
            public double Update(in Bar bar)
            {
                _window.Enqueue(bar.Close);
                if (_window.Count > length) _window.Dequeue();
                return _window.Count < length ? 0 : _window.Sum() / length;
            }
        }
    }
    public static IEnumerable<object[]> Cases =>
        (from name in new[] { "SMA", "EMA", "population-deviation" }
        from shape in new[] { "tiny", "large-offset", "large", "evicted-spike", "zero", "negative" }
        select new object[] { name, shape }).Concat(new[] { "overflow-adjacent", "cancelled-spike", "subnormal", "alternating-scale" }
            .Select(shape => new object[] { "SMA", shape }));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ArithmeticMatchesExactBinaryInputReference(string name, string shape)
    {
        const int period = 3;
        var fixture = IndicatorAdversarialCases.Generate(32, 244).Single(f => f.Name.EndsWith("/" + shape));
        IIndicator indicator = name switch { "SMA" => new Sma(period), "EMA" => new Ema(period), _ => new StdDev(period) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(fixture.Bars))
            .ConfigureIndicators(indicator).BuildAsync();
        var actual = run[indicator.Outputs[0]].ToArray();
        var input = fixture.Bars.Select(b => Rational.FromDouble(b.Close)).ToArray();
        IStreamingIndicatorState state = name switch
        {
            "SMA" => new SimpleMovingAverageState(period),
            "EMA" => new ExponentialMovingAverageState(period),
            _ => new StandardDeviationState(length: period)
        };
        using var lifetime = state as IDisposable;
        var previous = Rational.Zero;
        for (var i = 0; i < input.Length; i++)
        {
            var start = Math.Max(0, i - period + 1);
            var window = input.Skip(start).Take(i - start + 1).ToArray();
            var sum = window.Aggregate(Rational.Zero, (a, b) => a + b);
            Rational value;
            if (name == "EMA")
                value = i < period ? sum / new Rational(i + 1) : (previous + input[i]) / new Rational(2);
            else if (i + 1 < period) value = Rational.Zero;
            else if (name == "SMA") value = sum / new Rational(period);
            else
            {
                // E[X^2]-E[X]^2 is exact here: no floating cancellation or production recurrence.
                var mean = sum / new Rational(period);
                value = window.Aggregate(Rational.Zero, (a, b) => a + b * b) / new Rational(period) - mean * mean;
            }
            previous = value;
            var expected = name == "population-deviation" ? Math.Sqrt(value.ToDouble()) : value.ToDouble();
            // Relative-only for nonzero quantities; exact zero for a zero mathematical result.
            var budget = new IndicatorErrorBudget(0, 2e-12, requireSameSign: true);
            Assert.True(budget.Accepts(expected, actual[i]),
                $"{name}/{fixture.Name}, bar {i}: exact-input reference {expected:R}, actual {actual[i]:R}");
            var b = fixture.Bars[i];
            var streamedBar = new OhlcvBar("PILOT", BarTimeframe.Minutes(1), b.Time, b.Time,
                b.Open, b.High, b.Low, b.Close, b.Volume, true);
            var preview = state.Update(streamedBar, false, false).Value;
            var final = state.Update(streamedBar, true, false).Value;
            Assert.True(budget.Accepts(expected, preview), $"{name}/{shape}, preview bar {i}: expected {expected:R}, got {preview:R}");
            Assert.True(budget.Accepts(expected, final), $"{name}/{shape}, final bar {i}: expected {expected:R}, got {final:R}");
        }
    }

    [Fact]
    public void RationalReferenceRetainsCancellationAndSubnormalInput()
    {
        var big = Rational.FromDouble(1e100);
        Assert.Equal(1, (big + new Rational(1) - big).ToDouble());
        Assert.Equal(double.Epsilon, Rational.FromDouble(double.Epsilon).ToDouble());
        Assert.Equal(0, (Rational.FromDouble(double.MaxValue) - Rational.FromDouble(double.MaxValue)).ToDouble());
    }

    // Test-only independent exact arithmetic. No production helpers or decimal conversions.
    private readonly struct Rational
    {
        private readonly BigInteger _n, _d;
        internal static Rational Zero => new(0);
        internal Rational(long value) : this(value, BigInteger.One) { }
        private Rational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException();
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var common = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            _n = numerator / common; _d = denominator / common;
        }
        internal static Rational FromDouble(double value)
        {
            if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            var bits = BitConverter.DoubleToInt64Bits(value);
            var exponent = (int)((bits >> 52) & 0x7ff);
            var significand = new BigInteger(bits & 0x000fffffffffffffL);
            if (exponent != 0) significand += BigInteger.One << 52;
            var power = exponent == 0 ? -1074 : exponent - 1075;
            if (bits < 0) significand = -significand;
            return power >= 0 ? new(significand << power, BigInteger.One) : new(significand, BigInteger.One << -power);
        }
        internal double ToDouble()
        {
            if (_n.IsZero) return 0;
            var magnitude = BigInteger.Abs(_n);
            var nShift = Math.Max(0, (int)magnitude.GetBitLength() - 60);
            var dShift = Math.Max(0, (int)_d.GetBitLength() - 60);
            return _n.Sign * Math.ScaleB((double)(magnitude >> nShift) / (double)(_d >> dShift), nShift - dShift);
        }
        public static Rational operator +(Rational a, Rational b) => new(a._n * b._d + b._n * a._d, a._d * b._d);
        public static Rational operator -(Rational a, Rational b) => new(a._n * b._d - b._n * a._d, a._d * b._d);
        public static Rational operator *(Rational a, Rational b) => new(a._n * b._n, a._d * b._d);
        public static Rational operator /(Rational a, Rational b) => new(a._n * b._d, a._d * b._n);
    }
}
