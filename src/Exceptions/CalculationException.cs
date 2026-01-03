//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Exceptions;

public sealed class CalculationException : Exception
{
    public CalculationException()
    {

    }

    public CalculationException(string message) : base(message)
    {

    }

    public CalculationException(string message, Exception inner) : base(message, inner)
    {

    }

    [Obsolete("Obsolete")]
#pragma warning disable SYSLIB0051 // Type or member is obsolete
    private CalculationException(SerializationInfo info, StreamingContext context) : base(info, context)
#pragma warning restore SYSLIB0051 // Type or member is obsolete
    {

    }
}
