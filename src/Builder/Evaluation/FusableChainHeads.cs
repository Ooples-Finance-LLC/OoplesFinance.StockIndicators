using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// The specs a fused chain may begin with: those whose fast arm and streaming state agree exactly.
/// </summary>
/// <remarks>
/// <para>
/// Fusion changes the implementation of exactly one link. Every link after the head is computed by its
/// streaming state whether the chain is fused or not, so those cannot move. The head is different: resolved on
/// its own it reads the bars directly and can be served <c>IndicatorCompute.TryComputeFast</c>, while a fused
/// chain always drives its streaming state. Fusing a head whose two engines disagree would therefore change
/// published values, silently and only for callers who happened to chain something onto it.
/// </para>
/// <para>
/// "Agree" has to mean exactly, not closely. <c>BuilderArmTests</c> holds an arm to its batch indicator and
/// <c>BuilderStreamingArmTests</c> holds a state to the same indicator, but both compare with a tolerance, so
/// neither establishes that an arm and a state produce the same <c>double</c>. The members here are held to
/// that directly by <c>FusedChainTests</c>, at two parameter sets, bit for bit.
/// </para>
/// <para>
/// So this is a short list rather than every spec the streaming factory can build. The averages on it are
/// written to round alike on purpose - the same warm-up bar, the same grouping of the running sum, the same
/// rebuild cadence - which is what makes the substitution safe. Adding to it is not a judgement call: a spec
/// joins when the parity test passes for it, and that test reads this set, so a member that stops agreeing
/// fails rather than quietly moving someone's numbers. See issue #107.
/// </para>
/// </remarks>
internal static class FusableChainHeads
{
    public static readonly HashSet<Type> Types = new()
    {
        typeof(SmaSpecOptions),
        typeof(EmaSpecOptions),
        typeof(WmaSpecOptions),
    };
}
