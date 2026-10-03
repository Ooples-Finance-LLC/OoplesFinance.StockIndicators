"""Independent UMA fixture provenance; standard-library Decimal, no library calls.

Prints the three cases embedded in UltimateFractionalFixtureTests.cs. Agreement
at two precisions is a fixture cross-check, not a general rounding proof.
"""
from decimal import Decimal as D, localcontext
import json
import math


def calculate(precision, scale):
    with localcontext() as context:
        context.prec = precision
        raw = [float(x * scale) for x in [1, 3, 2, 4, 1, 5]]
        volumes = [float(x * scale) for x in [1, 2, 3, 4, 5, 6]]
        prices = [D.from_float(x) for x in raw]
        size = [D.from_float(x) for x in volumes]
        positive, negative, output = [], [], []
        for i, price in enumerate(prices):
            positive.append(price * size[i] if i and price > prices[i - 1] else D(0))
            negative.append(price * size[i] if i and price < prices[i - 1] else D(0))
            up = sum(positive[max(0, i - 2):i + 1])
            down = sum(negative[max(0, i - 2):i + 1])
            balance = D(1) if down == 0 else D(-1) if up == 0 else (up - down) / (up + down)
            power = 1 + 4 * abs(balance)
            weights = [(D(k).ln() * power).exp() for k in [1, 2, 3]]
            center = sum(weights[2 - j] * prices[i - j] for j in range(min(3, i + 1))) / sum(weights)
            history = prices[max(0, i - 2):i + 1]
            mean = sum(history) / len(history)
            deviation = (sum((x - mean) ** 2 for x in history) / 3).sqrt() if i >= 2 else D(0)
            output.append([float(center), float(center + 2 * deviation), float(center - 2 * deviation)])
        return dict(prices=raw, volumes=volumes, expected=output)


if __name__ == "__main__":
    cases = []
    for scale in [1.0, math.ldexp(1.0, 1020), math.ulp(0.0)]:
        case = calculate(240, scale)
        assert case == calculate(400, scale)
        cases.append(case)
    print(json.dumps(dict(period=3, checks=cases), indent=2))
