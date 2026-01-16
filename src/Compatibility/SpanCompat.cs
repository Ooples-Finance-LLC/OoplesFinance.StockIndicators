using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace OoplesFinance.StockIndicators.Compatibility;

internal static class SpanCompat
{
    internal static ReadOnlySpan<double> AsReadOnlySpan(List<double> list)
    {
#if NET8_0_OR_GREATER
        return CollectionsMarshal.AsSpan(list);
#else
        return list.ToArray();
#endif
    }

    internal static OutputSpanBuffer CreateOutputBuffer(int count)
    {
        return new OutputSpanBuffer(count);
    }
}

internal sealed class OutputSpanBuffer
{
#if NET8_0_OR_GREATER
    private readonly List<double> _list;
#else
    private readonly double[] _array;
#endif

    internal OutputSpanBuffer(int count)
    {
#if NET8_0_OR_GREATER
        _list = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            _list.Add(0d);
        }
#else
        _array = new double[count];
#endif
    }

    internal Span<double> Span
    {
        get
        {
#if NET8_0_OR_GREATER
            return CollectionsMarshal.AsSpan(_list);
#else
            return _array;
#endif
        }
    }

    internal List<double> ToList()
    {
#if NET8_0_OR_GREATER
        return _list;
#else
        return new List<double>(_array);
#endif
    }
}
