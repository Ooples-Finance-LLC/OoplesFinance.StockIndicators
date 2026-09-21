using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// A chain of indicators runs in one pass over the bars, and only where the series between them is unread.
/// </summary>
/// <remarks>
/// <para>
/// Resolving a chained indicator used to materialise its input: the upstream node was computed into a
/// <c>double[]</c>, cached, and walked again by the downstream state. An SMA feeding an EMA was two
/// traversals and an array that existed to be read once. <see cref="BatchCompute.ComputeAllChained"/> drives
/// the same states, in the same order, with the same <c>isFinal</c>, over the same bars - so the values are
/// not approximately the old ones, they are the old ones, and these tests hold them to exact equality rather
/// than a tolerance.
/// </para>
/// <para>
/// Fusion is invisible in the results, which is the trap: a test comparing fused and unfused output passes
/// just as happily when nothing was ever fused. <c>FusedChainHits</c> is the arm that separates the two, and
/// every test here asserts it - the positive ones that fusion ran, the negative ones that it did not. See
/// issue #107.
/// </para>
/// </remarks>
public sealed class FusedChainTests : GlobalTestData
{
    private static readonly SeriesHandle Bars = new(0);
    private static readonly SeriesHandle Sma = new(1);
    private static readonly SeriesHandle Ema = new(2);
    private static readonly SeriesHandle Second = new(3);

    /// <summary>
    /// A chain whose intermediate nothing else reads gives the values it gave when it was materialised.
    /// </summary>
    /// <remarks>
    /// The unfused arm is the same graph with the intermediate requested, which is the condition that refuses
    /// fusion. Both arms are asserted on: without the hit counts, a fusion that silently never happened would
    /// leave two identical unfused runs agreeing perfectly.
    /// </remarks>
    [Fact]
    public void AFusedChainComputesWhatTheMaterialisedChainComputed()
    {
        var data = new StockData(StockTestData.ToList());

        using var fusedContext = new ComputeContext();
        var fusedEvaluator = new SeriesEvaluator(data, BuildChain(), fusedContext);
        var fused = fusedEvaluator.Evaluate(new[] { Ema });

        using var plainContext = new ComputeContext();
        var plainEvaluator = new SeriesEvaluator(data, BuildChain(), plainContext);
        var plain = plainEvaluator.Evaluate(new[] { Sma, Ema });

        fusedEvaluator.FusedChainHits.Should().Be(1,
            "the SMA feeding the EMA is read by nothing else and was not asked for, so the chain is fused");
        plainEvaluator.FusedChainHits.Should().Be(0,
            "asking for the SMA itself means it has to be published, so that arm computes it as a series");
        fused[Ema].ToArray().Should().Equal(plain[Ema].ToArray(),
            "the fused chain drives the same states over the same bars, so it returns the same values exactly");
    }

    /// <summary>
    /// A cascade of two exponential averages fuses the same way a simple one feeding an exponential one does.
    /// </summary>
    /// <remarks>
    /// Nothing in the fusion decision names an indicator - a chain fuses when its links have streaming states
    /// and its intermediate is unread - so this pattern is expected to fall out of the same code. Expected is
    /// not measured, and the epic asks for cascaded exponential averages by name, so it is measured here.
    /// </remarks>
    [Fact]
    public void ACascadeOfExponentialAveragesIsFused()
    {
        var data = new StockData(StockTestData.ToList());
        var fast = new IndicatorSpec(IndicatorName.ExponentialMovingAverage, new EmaSpecOptions(12));
        var slow = new IndicatorSpec(IndicatorName.ExponentialMovingAverage, new EmaSpecOptions(26));

        Dictionary<SeriesHandle, SeriesNode> Cascade() => new()
        {
            [Bars] = SeriesNode.Base(default),
            [Sma] = SeriesNode.Indicator(default, Bars, fast),
            [Ema] = SeriesNode.Indicator(default, Sma, slow)
        };

        using var fusedContext = new ComputeContext();
        var fusedEvaluator = new SeriesEvaluator(data, Cascade(), fusedContext);
        var fused = fusedEvaluator.Evaluate(new[] { Ema });

        using var plainContext = new ComputeContext();
        var plainEvaluator = new SeriesEvaluator(data, Cascade(), plainContext);
        var plain = plainEvaluator.Evaluate(new[] { Sma, Ema });

        fusedEvaluator.FusedChainHits.Should().Be(1, "a cascaded pair of exponential averages is a chain too");
        plainEvaluator.FusedChainHits.Should().Be(0, "asking for the faster average means it has to be published");
        fused[Ema].ToArray().Should().Equal(plain[Ema].ToArray(), "the cascade fuses without moving a single value");
    }

    /// <summary>
    /// The bars every indicator reads are turned into a series once, not once per indicator that reads them.
    /// </summary>
    /// <remarks>
    /// This is the epic's third pattern, and it is the one thing it asks for that was already true:
    /// <c>GetBaseInput</c> keeps the materialised default input, so a second consumer gets the array the first
    /// one got rather than another copy of it. Recorded as a test rather than as a claim, so that removing the
    /// cache fails here instead of quietly doubling the reads.
    /// </remarks>
    [Fact]
    public void TheBarsAreMaterialisedOnceHoweverManyIndicatorsReadThem()
    {
        var data = new StockData(StockTestData.ToList());
        var nodes = BuildChain();
        nodes[Second] = SeriesNode.Indicator(default, Bars,
            new IndicatorSpec(IndicatorName.RelativeStrengthIndex, new RsiSpecOptions(14)));

        using var context = new ComputeContext();
        var evaluator = new SeriesEvaluator(data, nodes, context);
        var first = evaluator.Evaluate(new[] { Bars, Ema, Second });
        var second = evaluator.Evaluate(new[] { Bars });

        // ReadOnlyMemory equality is identity - the same underlying object, offset and length - so this still
        // asserts one materialisation handed to both, which BeSameAs asserted when the currency was an array.
        first[Bars].Equals(second[Bars]).Should().BeTrue(
            "the base series is materialised once for the evaluation and handed to everything that reads it");
    }

    /// <summary>
    /// An intermediate two indicators read is computed once as a series, not folded into either of them.
    /// </summary>
    /// <remarks>
    /// Fusing here would compute the SMA twice - once inside each chain - and the point of the intermediate is
    /// that both consumers share it. The second consumer is the only difference from the fused arm above.
    /// </remarks>
    [Fact]
    public void AnIntermediateWithASecondConsumerIsNotFused()
    {
        var data = new StockData(StockTestData.ToList());
        var nodes = BuildChain();
        nodes[Second] = SeriesNode.Indicator(default, Sma,
            new IndicatorSpec(IndicatorName.ExponentialMovingAverage, new EmaSpecOptions(5)));

        using var context = new ComputeContext();
        var evaluator = new SeriesEvaluator(data, nodes, context);
        var values = evaluator.Evaluate(new[] { Ema, Second });

        evaluator.FusedChainHits.Should().Be(0,
            "two indicators read the SMA, so folding it into one of them would compute it twice");
        values[Ema].Should().NotBeEmpty();
        values[Second].Should().NotBeEmpty();
    }

    /// <summary>
    /// A head reading an input series other than the bars' close fuses, and still returns the arm's values.
    /// </summary>
    /// <remarks>
    /// A fast arm reads <c>ChainedValues</c> or <c>InputValues</c>; a state reads the bar's close. Those are
    /// the same series until a caller assigns <c>InputValues</c> - or mutates the list it hands back, which
    /// passes through no setter and so cannot be recorded. Rather than gate fusion on a check a later mutation
    /// would invalidate, <see cref="BatchCompute.ComputeAllChained"/> feeds the head the same expression the
    /// arm reads, which makes the two identical by construction. This holds it to that: a chain over a custom
    /// input series fuses, and agrees exactly with the materialised chain over the same data.
    /// </remarks>
    [Fact]
    public void AHeadReadingAnInputOtherThanTheCloseFusesAndKeepsTheArmsValues()
    {
        var tickers = StockTestData.ToList();
        var midpoints = tickers.Select(t => (t.High + t.Low) / 2).ToList();

        var fusedData = new StockData(tickers) { InputValues = midpoints };
        using var fusedContext = new ComputeContext();
        var fusedEvaluator = new SeriesEvaluator(fusedData, BuildChain(), fusedContext);
        var fused = fusedEvaluator.Evaluate(new[] { Ema });

        var plainData = new StockData(tickers) { InputValues = midpoints };
        using var plainContext = new ComputeContext();
        var plainEvaluator = new SeriesEvaluator(plainData, BuildChain(), plainContext);
        var plain = plainEvaluator.Evaluate(new[] { Sma, Ema });

        fusedEvaluator.FusedChainHits.Should().Be(1,
            "which series the head reads is settled by feeding it that series, not by refusing to fuse");
        plainEvaluator.FusedChainHits.Should().Be(0, "asking for the SMA itself means it has to be published");
        fused[Ema].ToArray().Should().Equal(plain[Ema].ToArray(),
            "the fused head reads the caller's input series, which is what the fast arm it replaces reads");
    }

    /// <summary>
    /// Every spec a chain may begin with returns the same values from its fast arm and its streaming state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the assumption fusion rests on, and the one the rest of the suite cannot see. Fusing a chain
    /// swaps the head from whatever <c>TryComputeFast</c> serves to the head's streaming state; if those two
    /// disagree anywhere, fusion moves published values for every caller who chained something onto that
    /// indicator.
    /// </para>
    /// <para>
    /// Neither existing arm suite establishes this. <c>BuilderArmTests</c> compares an arm to its batch
    /// indicator and <c>BuilderStreamingArmTests</c> compares a state to the same indicator, but both use an
    /// <c>IsClose</c> tolerance, and two things that are each close to a third can still differ from one
    /// another. Compared here as raw bits, which is stricter than <c>==</c> and says exactly what is meant.
    /// </para>
    /// <para>
    /// Driven from <see cref="FusableChainHeads.Types"/> itself, so the set cannot outgrow its evidence: a spec
    /// added to it is proven here or it fails here.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryFusableHeadAgreesExactlyWithTheArmItReplaces()
    {
        var tickers = StockTestData.ToList();
        var failures = new List<string>();
        var comparedTypes = new HashSet<Type>();

        foreach (var type in FusableChainHeads.Types)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                failures.Add($"{type.Name}: names no batch indicator, so nothing says what it computes");
                continue;
            }

            foreach (var alternate in new[] { false, true })
            {
                var options = BuilderArmTests.Create(type, alternate);
                if (options is null)
                {
                    failures.Add($"{type.Name}: no constructor this can build");
                    break;
                }

                var label = $"{type.Name}{(alternate ? " (alternate parameters)" : string.Empty)}";
                var spec = new IndicatorSpec(target.Name, options);

                using var context = new ComputeContext();
                var fast = IndicatorCompute.TryComputeFast(new StockData(tickers), spec, context);
                if (fast is null)
                {
                    // No arm serves this head, so fusing it substitutes nothing.
                    continue;
                }

                using var buffer = fast.Value;
                var armed = buffer.ToArray();
                var streamed = BatchCompute.ComputeAll(new StockData(tickers), StatefulIndicatorFactory.Create(spec));
                comparedTypes.Add(type);

                if (armed.Length != streamed.Length)
                {
                    failures.Add($"{label}: {armed.Length} values from the arm, {streamed.Length} streamed");
                    continue;
                }

                var bar = Enumerable.Range(0, armed.Length).FirstOrDefault(
                    i => BitConverter.DoubleToInt64Bits(armed[i]) != BitConverter.DoubleToInt64Bits(streamed[i]), -1);
                if (bar >= 0)
                {
                    failures.Add($"{label} bar {bar}: arm {armed[bar]:R}, state {streamed[bar]:R}");
                }
            }
        }

        comparedTypes.Should().BeEquivalentTo(FusableChainHeads.Types,
            "the sweep skips a head no arm serves, since fusing that one substitutes nothing - but a member "
            + "skipped is a member nothing here examined, so every one of them has to have been compared");
        failures.Should().BeEmpty(
            $"a fusable head must return the arm's own values, not merely close ones: {string.Join(" | ", failures)}");
    }

    /// <summary>The bars, an SMA over them, and an EMA over that.</summary>
    private static Dictionary<SeriesHandle, SeriesNode> BuildChain()
    {
        return new Dictionary<SeriesHandle, SeriesNode>
        {
            [Bars] = SeriesNode.Base(default),
            [Sma] = SeriesNode.Indicator(default, Bars,
                new IndicatorSpec(IndicatorName.SimpleMovingAverage, new SmaSpecOptions(20))),
            [Ema] = SeriesNode.Indicator(default, Sma,
                new IndicatorSpec(IndicatorName.ExponentialMovingAverage, new EmaSpecOptions(10)))
        };
    }
}
