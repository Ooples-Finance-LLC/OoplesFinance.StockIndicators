"""Read-only seeded fixture/path model; no production build or timing claims."""
import json, math, struct
from fractions import Fraction
from pathlib import Path

class SeededRandom:
    # System.Random(seed)'s compatibility subtractive generator.
    def __init__(self, seed):
        self.a = [0] * 56
        mj = 161803398 - abs(seed)
        self.a[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = 21 * i % 55
            self.a[ii] = mk
            mk = mj - mk
            if mk < 0: mk += 2147483647
            mj = self.a[ii]
        for _ in range(4):
            for i in range(1, 56):
                self.a[i] -= self.a[1 + (i + 30) % 55]
                if self.a[i] < 0: self.a[i] += 2147483647
        self.i, self.j = 0, 21

    def sample(self):
        self.i = self.i % 55 + 1
        self.j = self.j % 55 + 1
        n = self.a[self.i] - self.a[self.j]
        if n == 2147483647: n -= 1
        if n < 0: n += 2147483647
        self.a[self.i] = n
        return n * (1.0 / 2147483647)

def inspect(count):
    rng = SeededRandom(42)
    last = 100.0
    flows, fallbacks = [], 0
    total = Fraction(0)
    widest, wide_count = 0, 0
    for _ in range(count):
        op = last + (rng.sample() - .5) * .5
        close = max(1.0, op + (rng.sample() - .5) * 2)
        high = max(op, close) + rng.sample()
        low = max(.01, min(op, close) - rng.sample())
        volume = int(rng.sample() * 499000) + 1000
        last = close
        high, low, close = [round(v * 1024) / 1024 for v in (high, low, close)]
        if high == low:
            flow = 0.0
        else:
            buying, selling = close - low, high - close
            balance, span = buying - selling, high - low
            assert Fraction(buying) == Fraction(close) - Fraction(low)
            assert Fraction(selling) == Fraction(high) - Fraction(close)
            assert Fraction(balance) == Fraction(buying) - Fraction(selling)
            assert Fraction(span) == Fraction(high) - Fraction(low)
            product = balance * volume
            if balance and volume:
                fallbacks += Fraction(product) != Fraction(balance) * volume
            flow = product / span
        flows.append(flow)
        total += Fraction(flow)
        numerator = abs(total.numerator)
        width = numerator.bit_length() - ((numerator & -numerator).bit_length() - 1) if numerator else 0
        widest = max(widest, width)
        wide_count += width > 63
    bits = [struct.unpack('<Q', struct.pack('<d', abs(v)))[0] for v in flows if v]
    lowest, highest = min(b >> 52 for b in bits), max(b >> 52 for b in bits)
    significands = 0
    for b in bits: significands |= b | (1 << 52)
    zeros = (significands & -significands).bit_length() - 1
    certificate_width = highest - lowest + 52 - zeros + count.bit_length()
    return dict(bars=count, modeled_product_fallback_calls=fallbacks,
                summary_certificate_width=certificate_width, summary_certifies=certificate_width <= 52,
                widest_exact_cumulative_significand=widest, cumulative_prefixes_wider_than_signed64=wide_count)

result = dict(status='read-only-model-not-runtime-instrumentation', fixtures=[inspect(n) for n in (1000, 10000)])
path = Path(__file__).with_suffix('.json')
path.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
