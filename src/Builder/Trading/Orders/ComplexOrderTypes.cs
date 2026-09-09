namespace OoplesFinance.StockIndicators.Builder.Trading.Orders;

/// <summary>
/// Types of complex orders.
/// </summary>
public enum ComplexOrderType
{
    /// <summary>
    /// Bracket order: Entry + StopLoss + TakeProfit as atomic unit.
    /// </summary>
    Bracket,

    /// <summary>
    /// One-Cancels-Other: When one leg fills, the other is cancelled.
    /// </summary>
    OCO,

    /// <summary>
    /// One-Triggers-Other: When the first leg fills, the second is activated.
    /// </summary>
    OTO,

    /// <summary>
    /// Multi-leg order (for options spreads).
    /// </summary>
    MultiLeg
}

/// <summary>
/// State of a complex order.
/// </summary>
public enum ComplexOrderState
{
    /// <summary>Order is pending submission.</summary>
    Pending,

    /// <summary>Order has been submitted and is active.</summary>
    Active,

    /// <summary>Entry leg has been filled.</summary>
    EntryFilled,

    /// <summary>Order is partially filled.</summary>
    PartiallyFilled,

    /// <summary>Order is completely filled.</summary>
    Filled,

    /// <summary>Order was cancelled.</summary>
    Cancelled,

    /// <summary>Order was rejected.</summary>
    Rejected,

    /// <summary>Order has expired.</summary>
    Expired
}

/// <summary>
/// A single leg of a complex order.
/// </summary>
public sealed class OrderLeg
{
    /// <summary>Gets or sets the leg ID.</summary>
    public string LegId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the order side (buy/sell).</summary>
    public OrderSide Side { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the order type.</summary>
    public OrderType OrderType { get; set; } = OrderType.Market;

    /// <summary>Gets or sets the limit price (for limit orders).</summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>Gets or sets the stop price (for stop orders).</summary>
    public decimal? StopPrice { get; set; }

    /// <summary>Gets or sets the time in force.</summary>
    public TimeInForce TimeInForce { get; set; } = TimeInForce.GTC;

    /// <summary>Gets or sets the leg role in complex order.</summary>
    public OrderLegRole Role { get; set; } = OrderLegRole.Primary;

    /// <summary>Gets or sets the broker order ID after submission.</summary>
    public string? BrokerOrderId { get; set; }

    /// <summary>Gets or sets the order status.</summary>
    public BrokerOrderStatus Status { get; set; } = BrokerOrderStatus.PendingNew;

    /// <summary>Gets or sets the filled quantity.</summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>Gets or sets the average fill price.</summary>
    public decimal? AverageFillPrice { get; set; }

    /// <summary>Gets whether this leg is filled.</summary>
    public bool IsFilled => Status == BrokerOrderStatus.Filled;

    /// <summary>Gets whether this leg is still active.</summary>
    public bool IsActive => Status == BrokerOrderStatus.New ||
                           Status == BrokerOrderStatus.PartiallyFilled ||
                           Status == BrokerOrderStatus.PendingNew;
}

/// <summary>
/// Role of an order leg in a complex order.
/// </summary>
public enum OrderLegRole
{
    /// <summary>Primary/entry leg.</summary>
    Primary,

    /// <summary>Stop loss leg.</summary>
    StopLoss,

    /// <summary>Take profit leg.</summary>
    TakeProfit,

    /// <summary>Secondary leg in OCO/OTO.</summary>
    Secondary
}

/// <summary>
/// Order side.
/// </summary>
public enum OrderSide
{
    /// <summary>Buy order.</summary>
    Buy,

    /// <summary>Sell order.</summary>
    Sell
}

/// <summary>
/// Interface for complex orders.
/// </summary>
public interface IComplexOrder
{
    /// <summary>Gets the complex order ID.</summary>
    string OrderId { get; }

    /// <summary>Gets the parent order ID (if any).</summary>
    string? ParentOrderId { get; }

    /// <summary>Gets the order legs.</summary>
    IReadOnlyList<OrderLeg> Legs { get; }

    /// <summary>Gets the complex order type.</summary>
    ComplexOrderType Type { get; }

    /// <summary>Gets the order state.</summary>
    ComplexOrderState State { get; }

    /// <summary>Gets the creation timestamp.</summary>
    DateTime CreatedAt { get; }

    /// <summary>Gets the last update timestamp.</summary>
    DateTime UpdatedAt { get; }
}

/// <summary>
/// Bracket order: Entry order with stop loss and take profit as atomic unit.
/// When entry fills, stop loss and take profit are automatically submitted.
/// </summary>
public sealed class BracketOrder : IComplexOrder
{
    /// <summary>
    /// Creates a new bracket order.
    /// </summary>
    public BracketOrder(
        string symbol,
        OrderSide side,
        decimal quantity,
        OrderType entryOrderType,
        decimal? entryLimitPrice,
        decimal stopLossPrice,
        decimal takeProfitPrice,
        TimeInForce timeInForce = TimeInForce.GTC)
    {
        OrderId = Guid.NewGuid().ToString();
        Symbol = symbol;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        // Entry leg
        EntryOrder = new OrderLeg
        {
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            OrderType = entryOrderType,
            LimitPrice = entryLimitPrice,
            TimeInForce = timeInForce,
            Role = OrderLegRole.Primary
        };

        // Stop loss leg (opposite side)
        var exitSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        StopLossOrder = new OrderLeg
        {
            Symbol = symbol,
            Side = exitSide,
            Quantity = quantity,
            OrderType = OrderType.Stop,
            StopPrice = stopLossPrice,
            TimeInForce = timeInForce,
            Role = OrderLegRole.StopLoss
        };

        // Take profit leg (opposite side)
        TakeProfitOrder = new OrderLeg
        {
            Symbol = symbol,
            Side = exitSide,
            Quantity = quantity,
            OrderType = OrderType.Limit,
            LimitPrice = takeProfitPrice,
            TimeInForce = timeInForce,
            Role = OrderLegRole.TakeProfit
        };
    }

    /// <inheritdoc />
    public string OrderId { get; }

    /// <inheritdoc />
    public string? ParentOrderId { get; set; }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the entry order leg.</summary>
    public OrderLeg EntryOrder { get; }

    /// <summary>Gets the stop loss leg.</summary>
    public OrderLeg StopLossOrder { get; }

    /// <summary>Gets the take profit leg.</summary>
    public OrderLeg TakeProfitOrder { get; }

    /// <inheritdoc />
    public IReadOnlyList<OrderLeg> Legs => new[] { EntryOrder, StopLossOrder, TakeProfitOrder };

    /// <inheritdoc />
    public ComplexOrderType Type => ComplexOrderType.Bracket;

    /// <inheritdoc />
    public ComplexOrderState State { get; set; } = ComplexOrderState.Pending;

    /// <inheritdoc />
    public DateTime CreatedAt { get; }

    /// <inheritdoc />
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets the stop loss price.</summary>
    public decimal StopLossPrice => StopLossOrder.StopPrice ?? 0m;

    /// <summary>Gets the take profit price.</summary>
    public decimal TakeProfitPrice => TakeProfitOrder.LimitPrice ?? 0m;

    /// <summary>Gets the risk amount (entry price - stop loss).</summary>
    public decimal RiskAmount
    {
        get
        {
            var entryPrice = EntryOrder.LimitPrice ?? EntryOrder.AverageFillPrice ?? 0m;
            if (entryPrice <= 0 || StopLossPrice <= 0) return 0m;

            return EntryOrder.Side == OrderSide.Buy
                ? entryPrice - StopLossPrice
                : StopLossPrice - entryPrice;
        }
    }

    /// <summary>Gets the reward amount (take profit - entry price).</summary>
    public decimal RewardAmount
    {
        get
        {
            var entryPrice = EntryOrder.LimitPrice ?? EntryOrder.AverageFillPrice ?? 0m;
            if (entryPrice <= 0 || TakeProfitPrice <= 0) return 0m;

            return EntryOrder.Side == OrderSide.Buy
                ? TakeProfitPrice - entryPrice
                : entryPrice - TakeProfitPrice;
        }
    }

    /// <summary>Gets the risk/reward ratio.</summary>
    public decimal RiskRewardRatio => RiskAmount > 0 ? RewardAmount / RiskAmount : 0m;

    /// <summary>
    /// Creates a bracket order for a long position.
    /// </summary>
    public static BracketOrder Long(
        string symbol,
        decimal quantity,
        decimal? entryLimitPrice,
        decimal stopLossPrice,
        decimal takeProfitPrice,
        OrderType entryType = OrderType.Market,
        TimeInForce timeInForce = TimeInForce.GTC)
    {
        return new BracketOrder(
            symbol,
            OrderSide.Buy,
            quantity,
            entryType,
            entryLimitPrice,
            stopLossPrice,
            takeProfitPrice,
            timeInForce);
    }

    /// <summary>
    /// Creates a bracket order for a short position.
    /// </summary>
    public static BracketOrder Short(
        string symbol,
        decimal quantity,
        decimal? entryLimitPrice,
        decimal stopLossPrice,
        decimal takeProfitPrice,
        OrderType entryType = OrderType.Market,
        TimeInForce timeInForce = TimeInForce.GTC)
    {
        return new BracketOrder(
            symbol,
            OrderSide.Sell,
            quantity,
            entryType,
            entryLimitPrice,
            stopLossPrice,
            takeProfitPrice,
            timeInForce);
    }
}

/// <summary>
/// OCO (One-Cancels-Other) order: Two orders where filling one cancels the other.
/// Commonly used for setting both a stop loss and a take profit on an existing position.
/// </summary>
public sealed class OcoOrder : IComplexOrder
{
    /// <summary>
    /// Creates a new OCO order.
    /// </summary>
    public OcoOrder(
        string symbol,
        OrderSide side,
        decimal quantity,
        decimal stopPrice,
        decimal limitPrice,
        TimeInForce timeInForce = TimeInForce.GTC)
    {
        OrderId = Guid.NewGuid().ToString();
        Symbol = symbol;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        // Stop order leg
        StopOrder = new OrderLeg
        {
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            OrderType = OrderType.Stop,
            StopPrice = stopPrice,
            TimeInForce = timeInForce,
            Role = OrderLegRole.StopLoss
        };

        // Limit order leg
        LimitOrder = new OrderLeg
        {
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            OrderType = OrderType.Limit,
            LimitPrice = limitPrice,
            TimeInForce = timeInForce,
            Role = OrderLegRole.TakeProfit
        };
    }

    /// <inheritdoc />
    public string OrderId { get; }

    /// <inheritdoc />
    public string? ParentOrderId { get; set; }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the stop order leg.</summary>
    public OrderLeg StopOrder { get; }

    /// <summary>Gets the limit order leg.</summary>
    public OrderLeg LimitOrder { get; }

    /// <inheritdoc />
    public IReadOnlyList<OrderLeg> Legs => new[] { StopOrder, LimitOrder };

    /// <inheritdoc />
    public ComplexOrderType Type => ComplexOrderType.OCO;

    /// <inheritdoc />
    public ComplexOrderState State { get; set; } = ComplexOrderState.Pending;

    /// <inheritdoc />
    public DateTime CreatedAt { get; }

    /// <inheritdoc />
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets which leg was filled (if any).</summary>
    public OrderLeg? FilledLeg
    {
        get
        {
            if (StopOrder.IsFilled) return StopOrder;
            if (LimitOrder.IsFilled) return LimitOrder;
            return null;
        }
    }

    /// <summary>Gets which leg was cancelled (if any).</summary>
    public OrderLeg? CancelledLeg
    {
        get
        {
            if (StopOrder.Status == BrokerOrderStatus.Cancelled) return StopOrder;
            if (LimitOrder.Status == BrokerOrderStatus.Cancelled) return LimitOrder;
            return null;
        }
    }

    /// <summary>
    /// Creates an OCO order to close a long position (stop loss below, take profit above).
    /// </summary>
    public static OcoOrder ClosePosition(
        string symbol,
        decimal quantity,
        decimal currentPrice,
        decimal stopLossPercent,
        decimal takeProfitPercent)
    {
        var stopPrice = currentPrice * (1 - stopLossPercent / 100m);
        var limitPrice = currentPrice * (1 + takeProfitPercent / 100m);

        return new OcoOrder(symbol, OrderSide.Sell, quantity, stopPrice, limitPrice);
    }
}

/// <summary>
/// OTO (One-Triggers-Other) order: When the first order fills, the second is automatically submitted.
/// Useful for chaining orders together.
/// </summary>
public sealed class OtoOrder : IComplexOrder
{
    /// <summary>
    /// Creates a new OTO order.
    /// </summary>
    public OtoOrder(OrderLeg primaryOrder, OrderLeg triggeredOrder)
    {
        OrderId = Guid.NewGuid().ToString();
        Symbol = primaryOrder.Symbol;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        primaryOrder.Role = OrderLegRole.Primary;
        triggeredOrder.Role = OrderLegRole.Secondary;

        PrimaryOrder = primaryOrder;
        TriggeredOrder = triggeredOrder;
    }

    /// <inheritdoc />
    public string OrderId { get; }

    /// <inheritdoc />
    public string? ParentOrderId { get; set; }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the primary order (triggers the second).</summary>
    public OrderLeg PrimaryOrder { get; }

    /// <summary>Gets the order that triggers when primary fills.</summary>
    public OrderLeg TriggeredOrder { get; }

    /// <inheritdoc />
    public IReadOnlyList<OrderLeg> Legs => new[] { PrimaryOrder, TriggeredOrder };

    /// <inheritdoc />
    public ComplexOrderType Type => ComplexOrderType.OTO;

    /// <inheritdoc />
    public ComplexOrderState State { get; set; } = ComplexOrderState.Pending;

    /// <inheritdoc />
    public DateTime CreatedAt { get; }

    /// <inheritdoc />
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets whether the primary order has filled.</summary>
    public bool PrimaryFilled => PrimaryOrder.IsFilled;

    /// <summary>Gets whether the triggered order has been activated.</summary>
    public bool TriggeredActivated => TriggeredOrder.Status != BrokerOrderStatus.PendingNew;

    /// <summary>
    /// Creates an OTO order that enters a position and sets a stop loss.
    /// </summary>
    public static OtoOrder EntryWithStopLoss(
        string symbol,
        OrderSide side,
        decimal quantity,
        OrderType entryType,
        decimal? entryLimitPrice,
        decimal stopLossPrice,
        TimeInForce timeInForce = TimeInForce.GTC)
    {
        var exitSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;

        var entryLeg = new OrderLeg
        {
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            OrderType = entryType,
            LimitPrice = entryLimitPrice,
            TimeInForce = timeInForce
        };

        var stopLossLeg = new OrderLeg
        {
            Symbol = symbol,
            Side = exitSide,
            Quantity = quantity,
            OrderType = OrderType.Stop,
            StopPrice = stopLossPrice,
            TimeInForce = timeInForce
        };

        return new OtoOrder(entryLeg, stopLossLeg);
    }
}

/// <summary>
/// Multi-leg order for options strategies (spreads, straddles, etc.).
/// </summary>
public sealed class MultiLegOrder : IComplexOrder
{
    private readonly List<OrderLeg> _legs = new();

    /// <summary>
    /// Creates a new multi-leg order.
    /// </summary>
    public MultiLegOrder(string? strategyName = null)
    {
        OrderId = Guid.NewGuid().ToString();
        StrategyName = strategyName ?? "Custom";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public string OrderId { get; }

    /// <inheritdoc />
    public string? ParentOrderId { get; set; }

    /// <summary>Gets the strategy name.</summary>
    public string StrategyName { get; }

    /// <inheritdoc />
    public IReadOnlyList<OrderLeg> Legs => _legs;

    /// <inheritdoc />
    public ComplexOrderType Type => ComplexOrderType.MultiLeg;

    /// <inheritdoc />
    public ComplexOrderState State { get; set; } = ComplexOrderState.Pending;

    /// <inheritdoc />
    public DateTime CreatedAt { get; }

    /// <inheritdoc />
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Adds a leg to the multi-leg order.
    /// </summary>
    public MultiLegOrder AddLeg(OrderLeg leg)
    {
        _legs.Add(leg);
        UpdatedAt = DateTime.UtcNow;
        return this;
    }

    /// <summary>
    /// Adds a buy leg.
    /// </summary>
    public MultiLegOrder AddBuyLeg(
        string symbol,
        decimal quantity,
        OrderType orderType = OrderType.Limit,
        decimal? limitPrice = null)
    {
        return AddLeg(new OrderLeg
        {
            Symbol = symbol,
            Side = OrderSide.Buy,
            Quantity = quantity,
            OrderType = orderType,
            LimitPrice = limitPrice
        });
    }

    /// <summary>
    /// Adds a sell leg.
    /// </summary>
    public MultiLegOrder AddSellLeg(
        string symbol,
        decimal quantity,
        OrderType orderType = OrderType.Limit,
        decimal? limitPrice = null)
    {
        return AddLeg(new OrderLeg
        {
            Symbol = symbol,
            Side = OrderSide.Sell,
            Quantity = quantity,
            OrderType = orderType,
            LimitPrice = limitPrice
        });
    }

    /// <summary>Gets the net debit/credit of the order.</summary>
    public decimal NetDebitCredit
    {
        get
        {
            var total = 0m;
            foreach (var leg in _legs)
            {
                var price = leg.LimitPrice ?? leg.AverageFillPrice ?? 0m;
                var value = price * leg.Quantity;
                if (leg.Side == OrderSide.Buy)
                {
                    total -= value; // Debit
                }
                else
                {
                    total += value; // Credit
                }
            }
            return total;
        }
    }

    /// <summary>Gets whether this is a debit order (net cost).</summary>
    public bool IsDebit => NetDebitCredit < 0;

    /// <summary>Gets whether this is a credit order (net proceeds).</summary>
    public bool IsCredit => NetDebitCredit > 0;
}

/// <summary>
/// Builder for creating complex orders with fluent API.
/// </summary>
public static class ComplexOrderBuilder
{
    /// <summary>
    /// Creates a bracket order builder.
    /// </summary>
    public static BracketOrderBuilder Bracket(string symbol) => new(symbol);

    /// <summary>
    /// Creates an OCO order builder.
    /// </summary>
    public static OcoOrderBuilder Oco(string symbol) => new(symbol);

    /// <summary>
    /// Creates an OTO order builder.
    /// </summary>
    public static OtoOrderBuilder Oto(string symbol) => new(symbol);

    /// <summary>
    /// Creates a multi-leg order builder.
    /// </summary>
    public static MultiLegOrderBuilder MultiLeg(string? strategyName = null) => new(strategyName);
}

/// <summary>
/// Builder for bracket orders.
/// </summary>
public sealed class BracketOrderBuilder
{
    private readonly string _symbol;
    private OrderSide _side = OrderSide.Buy;
    private decimal _quantity;
    private OrderType _entryType = OrderType.Market;
    private decimal? _entryLimitPrice;
    private decimal _stopLossPrice;
    private decimal _takeProfitPrice;
    private TimeInForce _timeInForce = TimeInForce.GTC;

    internal BracketOrderBuilder(string symbol)
    {
        _symbol = symbol;
    }

    public BracketOrderBuilder Buy() { _side = OrderSide.Buy; return this; }
    public BracketOrderBuilder Sell() { _side = OrderSide.Sell; return this; }
    public BracketOrderBuilder Quantity(decimal qty) { _quantity = qty; return this; }
    public BracketOrderBuilder MarketEntry() { _entryType = OrderType.Market; return this; }
    public BracketOrderBuilder LimitEntry(decimal price) { _entryType = OrderType.Limit; _entryLimitPrice = price; return this; }
    public BracketOrderBuilder StopLoss(decimal price) { _stopLossPrice = price; return this; }
    public BracketOrderBuilder TakeProfit(decimal price) { _takeProfitPrice = price; return this; }
    public BracketOrderBuilder WithTimeInForce(TimeInForce tif) { _timeInForce = tif; return this; }

    public BracketOrder Build()
    {
        if (_quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than 0");
        if (_stopLossPrice <= 0)
            throw new InvalidOperationException("Stop loss price must be greater than 0");
        if (_takeProfitPrice <= 0)
            throw new InvalidOperationException("Take profit price must be greater than 0");

        return new BracketOrder(
            _symbol,
            _side,
            _quantity,
            _entryType,
            _entryLimitPrice,
            _stopLossPrice,
            _takeProfitPrice,
            _timeInForce);
    }
}

/// <summary>
/// Builder for OCO orders.
/// </summary>
public sealed class OcoOrderBuilder
{
    private readonly string _symbol;
    private OrderSide _side = OrderSide.Sell;
    private decimal _quantity;
    private decimal _stopPrice;
    private decimal _limitPrice;
    private TimeInForce _timeInForce = TimeInForce.GTC;

    internal OcoOrderBuilder(string symbol)
    {
        _symbol = symbol;
    }

    public OcoOrderBuilder ToClose() { _side = OrderSide.Sell; return this; }
    public OcoOrderBuilder ToCover() { _side = OrderSide.Buy; return this; }
    public OcoOrderBuilder Quantity(decimal qty) { _quantity = qty; return this; }
    public OcoOrderBuilder StopAt(decimal price) { _stopPrice = price; return this; }
    public OcoOrderBuilder LimitAt(decimal price) { _limitPrice = price; return this; }
    public OcoOrderBuilder WithTimeInForce(TimeInForce tif) { _timeInForce = tif; return this; }

    public OcoOrder Build()
    {
        if (_quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than 0");
        if (_stopPrice <= 0)
            throw new InvalidOperationException("Stop price must be greater than 0");
        if (_limitPrice <= 0)
            throw new InvalidOperationException("Limit price must be greater than 0");

        return new OcoOrder(_symbol, _side, _quantity, _stopPrice, _limitPrice, _timeInForce);
    }
}

/// <summary>
/// Builder for OTO orders.
/// </summary>
public sealed class OtoOrderBuilder
{
    private readonly string _symbol;
    private OrderLeg? _primaryLeg;
    private OrderLeg? _triggeredLeg;

    internal OtoOrderBuilder(string symbol)
    {
        _symbol = symbol;
    }

    public OtoOrderBuilder Primary(Action<OrderLegBuilder> configure)
    {
        var builder = new OrderLegBuilder(_symbol);
        configure(builder);
        _primaryLeg = builder.Build();
        return this;
    }

    public OtoOrderBuilder WhenFilled(Action<OrderLegBuilder> configure)
    {
        var builder = new OrderLegBuilder(_symbol);
        configure(builder);
        _triggeredLeg = builder.Build();
        return this;
    }

    public OtoOrder Build()
    {
        if (_primaryLeg is null)
            throw new InvalidOperationException("Primary order leg must be configured");
        if (_triggeredLeg is null)
            throw new InvalidOperationException("Triggered order leg must be configured");

        return new OtoOrder(_primaryLeg, _triggeredLeg);
    }
}

/// <summary>
/// Builder for multi-leg orders.
/// </summary>
public sealed class MultiLegOrderBuilder
{
    private readonly MultiLegOrder _order;

    internal MultiLegOrderBuilder(string? strategyName)
    {
        _order = new MultiLegOrder(strategyName);
    }

    public MultiLegOrderBuilder AddLeg(Action<OrderLegBuilder> configure)
    {
        var builder = new OrderLegBuilder(string.Empty);
        configure(builder);
        _order.AddLeg(builder.Build());
        return this;
    }

    public MultiLegOrder Build() => _order;
}

/// <summary>
/// Builder for individual order legs.
/// </summary>
public sealed class OrderLegBuilder
{
    private readonly OrderLeg _leg = new();

    internal OrderLegBuilder(string symbol)
    {
        _leg.Symbol = symbol;
    }

    public OrderLegBuilder Symbol(string symbol) { _leg.Symbol = symbol; return this; }
    public OrderLegBuilder Buy() { _leg.Side = OrderSide.Buy; return this; }
    public OrderLegBuilder Sell() { _leg.Side = OrderSide.Sell; return this; }
    public OrderLegBuilder Quantity(decimal qty) { _leg.Quantity = qty; return this; }
    public OrderLegBuilder Market() { _leg.OrderType = OrderType.Market; return this; }
    public OrderLegBuilder Limit(decimal price) { _leg.OrderType = OrderType.Limit; _leg.LimitPrice = price; return this; }
    public OrderLegBuilder Stop(decimal price) { _leg.OrderType = OrderType.Stop; _leg.StopPrice = price; return this; }
    public OrderLegBuilder StopLimit(decimal stopPrice, decimal limitPrice)
    {
        _leg.OrderType = OrderType.StopLimit;
        _leg.StopPrice = stopPrice;
        _leg.LimitPrice = limitPrice;
        return this;
    }
    public OrderLegBuilder TimeInForce(TimeInForce tif) { _leg.TimeInForce = tif; return this; }

    internal OrderLeg Build()
    {
        if (string.IsNullOrEmpty(_leg.Symbol))
            throw new InvalidOperationException("Symbol is required");
        if (_leg.Quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than 0");

        return _leg;
    }
}
