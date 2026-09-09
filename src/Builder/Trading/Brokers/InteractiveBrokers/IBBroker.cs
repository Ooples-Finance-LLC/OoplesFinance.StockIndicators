using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OoplesFinance.StockIndicators.Builder.Trading.Brokers.InteractiveBrokers;

/// <summary>
/// Interactive Brokers TWS/Gateway broker implementation.
/// Provides trading capabilities through the Interactive Brokers API.
/// </summary>
/// <remarks>
/// Requires TWS (Trader Workstation) or IB Gateway to be running.
/// Uses socket-based connection to the TWS API.
/// Supports stocks, options, futures, forex, and crypto trading.
/// </remarks>
public sealed class IBBroker : IBroker, IDisposable
{
    private readonly IBOptions _options;
    private readonly object _lockObj = new();
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private Thread? _readerThread;
    private CancellationTokenSource? _cts;
    private int _nextOrderId;
    private int _nextRequestId = 1;
    private bool _isConnected;
    private bool _disposed;
    private int _serverVersion;

    // Order tracking
    private readonly Dictionary<int, TaskCompletionSource<BrokerOrder>> _orderCompletions = new();
    private readonly Dictionary<int, BrokerOrder> _orders = new();
    private readonly Dictionary<string, BrokerPosition> _positions = new();

    // Request completions
    private readonly Dictionary<int, TaskCompletionSource<object?>> _requestCompletions = new();

    // Account data
    private BrokerAccount? _cachedAccount;
    private TaskCompletionSource<BrokerAccount>? _accountTcs;
    private readonly List<string> _accountValues = new();

    // IB API message types
    private const int REQ_MKT_DATA = 1;
    private const int CANCEL_MKT_DATA = 2;
    private const int PLACE_ORDER = 3;
    private const int CANCEL_ORDER = 4;
    private const int REQ_OPEN_ORDERS = 5;
    private const int REQ_ACCOUNT_DATA = 6;
    private const int REQ_EXECUTIONS = 7;
    private const int REQ_IDS = 8;
    private const int REQ_CONTRACT_DATA = 9;
    private const int REQ_AUTO_OPEN_ORDERS = 15;
    private const int REQ_ALL_OPEN_ORDERS = 16;
    private const int REQ_MANAGED_ACCTS = 17;
    private const int REQ_POSITIONS = 61;
    private const int REQ_ACCOUNT_SUMMARY = 62;
    private const int CANCEL_ACCOUNT_SUMMARY = 63;
    private const int CANCEL_POSITIONS = 64;

    // IB API response message types
    private const int TICK_PRICE = 1;
    private const int TICK_SIZE = 2;
    private const int ORDER_STATUS = 3;
    private const int ERR_MSG = 4;
    private const int OPEN_ORDER = 5;
    private const int ACCT_VALUE = 6;
    private const int PORTFOLIO_VALUE = 7;
    private const int ACCT_UPDATE_TIME = 8;
    private const int NEXT_VALID_ID = 9;
    private const int CONTRACT_DATA = 10;
    private const int EXECUTION_DATA = 11;
    private const int MANAGED_ACCTS = 15;
    private const int POSITION_DATA = 61;
    private const int POSITION_END = 62;
    private const int ACCOUNT_SUMMARY = 63;
    private const int ACCOUNT_SUMMARY_END = 64;

    // Minimum server version requirements
    private const int MIN_SERVER_VER = 100;

    /// <summary>
    /// Creates a new Interactive Brokers broker instance.
    /// </summary>
    /// <param name="options">IB connection options.</param>
    public IBBroker(IBOptions? options = null)
    {
        _options = options ?? new IBOptions();
    }

    /// <summary>
    /// Gets whether the broker is connected to TWS/Gateway.
    /// </summary>
    public bool IsConnected => _isConnected && !_disposed;

    /// <summary>
    /// Connects to TWS/Gateway.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isConnected)
        {
            return;
        }

        try
        {
            _tcpClient = new TcpClient();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.ConnectionTimeout);

            await _tcpClient.ConnectAsync(_options.Host, _options.Port).ConfigureAwait(false);

            _stream = _tcpClient.GetStream();
            _stream.ReadTimeout = (int)_options.ConnectionTimeout.TotalMilliseconds;
            _stream.WriteTimeout = (int)_options.ConnectionTimeout.TotalMilliseconds;

            // Send API version handshake
            await SendHandshakeAsync(cancellationToken).ConfigureAwait(false);

            // Start message reader thread
            _cts = new CancellationTokenSource();
            _readerThread = new Thread(MessageReaderLoop)
            {
                IsBackground = true,
                Name = "IB-MessageReader"
            };
            _readerThread.Start();

            // Wait for next valid order ID
            var nextIdTcs = new TaskCompletionSource<int>();
            lock (_lockObj)
            {
                _requestCompletions[-1] = new TaskCompletionSource<object?>();
            }

            // Request next valid order ID
            await SendMessageAsync(new[] { REQ_IDS.ToString(), "1", "1" }, cancellationToken).ConfigureAwait(false);

            // Wait for next valid ID with timeout
            using var idTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idTimeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                TaskCompletionSource<object?>? tcs;
                lock (_lockObj)
                {
                    _requestCompletions.TryGetValue(-1, out tcs);
                }

                if (tcs is not null)
                {
                    await WaitWithTimeoutAsync(tcs.Task, idTimeoutCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout waiting for next valid ID, use default
                _nextOrderId = 1;
            }

            // Request managed accounts
            await SendMessageAsync(new[] { REQ_MANAGED_ACCTS.ToString(), "1" }, cancellationToken).ConfigureAwait(false);

            // Subscribe to account updates if account ID is specified
            if (!string.IsNullOrEmpty(_options.AccountId))
            {
                await SendMessageAsync(new[] { REQ_ACCOUNT_DATA.ToString(), "2", "1", _options.AccountId ?? string.Empty }, cancellationToken).ConfigureAwait(false);
            }

            _isConnected = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Cleanup();
            throw new InvalidOperationException($"Failed to connect to TWS/Gateway at {_options.Host}:{_options.Port}. " +
                "Ensure TWS or IB Gateway is running and API connections are enabled.", ex);
        }
    }

    private async Task SendHandshakeAsync(CancellationToken cancellationToken)
    {
        if (_stream is null) return;

        // Send client version using v100+ protocol
        var clientVersion = "v100..176";
        var versionBytes = Encoding.ASCII.GetBytes(clientVersion);

        // Send prefix "API\0" followed by length-prefixed client version
        var prefix = Encoding.ASCII.GetBytes("API\0");
        await _stream.WriteAsync(prefix, 0, prefix.Length, cancellationToken).ConfigureAwait(false);

        // Send length-prefixed version string
        var lengthBytes = BitConverter.GetBytes(versionBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }
        await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(versionBytes, 0, versionBytes.Length, cancellationToken).ConfigureAwait(false);

        // Read server version response
        var serverVersionBytes = new byte[4];
        var bytesRead = await _stream.ReadAsync(serverVersionBytes, 0, 4, cancellationToken).ConfigureAwait(false);
        if (bytesRead < 4)
        {
            throw new InvalidOperationException("Failed to read server version");
        }

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(serverVersionBytes);
        }
        var messageLength = BitConverter.ToInt32(serverVersionBytes, 0);

        var messageBytes = new byte[messageLength];
        var totalRead = 0;
        while (totalRead < messageLength)
        {
            var read = await _stream.ReadAsync(messageBytes, totalRead, messageLength - totalRead, cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new InvalidOperationException("Connection closed during handshake");
            totalRead += read;
        }

        var message = Encoding.ASCII.GetString(messageBytes);
        var parts = message.Split('\0');
        if (parts.Length > 0 && int.TryParse(parts[0], out var serverVer))
        {
            _serverVersion = serverVer;
        }

        if (_serverVersion < MIN_SERVER_VER)
        {
            throw new InvalidOperationException($"Server version {_serverVersion} is too old. Minimum required: {MIN_SERVER_VER}");
        }

        // Send start API message
        var startApiMsg = BuildMessage(new[] { "71", "2", _options.ClientId.ToString(), "" });
        await _stream.WriteAsync(startApiMsg, 0, startApiMsg.Length, cancellationToken).ConfigureAwait(false);
    }

    private void MessageReaderLoop()
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();

        try
        {
            while (_cts is not null && !_cts.Token.IsCancellationRequested && _stream is not null)
            {
                try
                {
                    // Read message length (4 bytes, big-endian)
                    var lengthBytes = new byte[4];
                    var read = _stream.Read(lengthBytes, 0, 4);
                    if (read == 0) break;
                    if (read < 4) continue;

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthBytes);
                    }
                    var messageLength = BitConverter.ToInt32(lengthBytes, 0);

                    if (messageLength <= 0 || messageLength > 10_000_000)
                    {
                        continue;
                    }

                    // Read message body
                    var messageBytes = new byte[messageLength];
                    var totalRead = 0;
                    while (totalRead < messageLength)
                    {
                        read = _stream.Read(messageBytes, totalRead, messageLength - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    if (totalRead < messageLength) continue;

                    var message = Encoding.ASCII.GetString(messageBytes);
                    ProcessMessage(message);
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }
        catch
        {
            // Reader thread exiting
        }
        finally
        {
            _isConnected = false;
        }
    }

    private void ProcessMessage(string message)
    {
        var fields = message.Split('\0');
        if (fields.Length < 2) return;

        if (!int.TryParse(fields[0], out var messageType)) return;

        switch (messageType)
        {
            case NEXT_VALID_ID:
                ProcessNextValidId(fields);
                break;
            case ORDER_STATUS:
                ProcessOrderStatus(fields);
                break;
            case OPEN_ORDER:
                ProcessOpenOrder(fields);
                break;
            case ACCT_VALUE:
                ProcessAccountValue(fields);
                break;
            case PORTFOLIO_VALUE:
                ProcessPortfolioValue(fields);
                break;
            case POSITION_DATA:
                ProcessPositionData(fields);
                break;
            case POSITION_END:
                ProcessPositionEnd(fields);
                break;
            case ACCOUNT_SUMMARY:
                ProcessAccountSummary(fields);
                break;
            case ACCOUNT_SUMMARY_END:
                ProcessAccountSummaryEnd(fields);
                break;
            case ERR_MSG:
                ProcessErrorMessage(fields);
                break;
            case EXECUTION_DATA:
                ProcessExecutionData(fields);
                break;
            case MANAGED_ACCTS:
                ProcessManagedAccounts(fields);
                break;
        }
    }

    private void ProcessNextValidId(string[] fields)
    {
        // Fields: msgType, version, orderId
        if (fields.Length >= 3 && int.TryParse(fields[2], out var orderId))
        {
            lock (_lockObj)
            {
                _nextOrderId = orderId;
                if (_requestCompletions.TryGetValue(-1, out var tcs))
                {
                    tcs.TrySetResult(null);
                    _requestCompletions.Remove(-1);
                }
            }
        }
    }

    private void ProcessOrderStatus(string[] fields)
    {
        // Fields: msgType, orderId, status, filled, remaining, avgFillPrice, permId, parentId, lastFillPrice, clientId, whyHeld, mktCapPrice
        if (fields.Length < 6) return;

        if (!int.TryParse(fields[1], out var orderId)) return;

        var status = fields[2];
        decimal.TryParse(fields[3], out var filled);
        decimal.TryParse(fields[5], out var avgFillPrice);

        lock (_lockObj)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                order.FilledQuantity = filled;
                order.AverageFillPrice = avgFillPrice > 0 ? avgFillPrice : null;
                order.Status = MapIBStatus(status);

                if (_orderCompletions.TryGetValue(orderId, out var tcs))
                {
                    if (order.Status == BrokerOrderStatus.Filled ||
                        order.Status == BrokerOrderStatus.Cancelled ||
                        order.Status == BrokerOrderStatus.Rejected)
                    {
                        tcs.TrySetResult(order);
                        _orderCompletions.Remove(orderId);
                    }
                }
            }
        }
    }

    private void ProcessOpenOrder(string[] fields)
    {
        // This contains full order details - update our order tracking
        if (fields.Length < 10) return;

        if (!int.TryParse(fields[1], out var orderId)) return;

        // Extract key fields
        var symbol = fields.Length > 4 ? fields[4] : string.Empty;
        var action = fields.Length > 16 ? fields[16] : "BUY";
        decimal.TryParse(fields.Length > 17 ? fields[17] : "0", out var quantity);
        var orderType = fields.Length > 18 ? fields[18] : "MKT";

        lock (_lockObj)
        {
            if (!_orders.ContainsKey(orderId))
            {
                _orders[orderId] = new BrokerOrder
                {
                    OrderId = orderId.ToString(),
                    Symbol = symbol,
                    Quantity = quantity,
                    Side = action.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? "buy" : "sell",
                    OrderType = MapIBOrderType(orderType),
                    Status = BrokerOrderStatus.New,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }
    }

    private void ProcessAccountValue(string[] fields)
    {
        // Fields: msgType, version, key, value, currency, accountName
        if (fields.Length < 5) return;

        var key = fields[2];
        var value = fields[3];
        var currency = fields[4];
        var accountName = fields.Length > 5 ? fields[5] : string.Empty;

        lock (_lockObj)
        {
            _accountValues.Add($"{key}={value} {currency}");
        }
    }

    private void ProcessPortfolioValue(string[] fields)
    {
        // Fields: msgType, version, conId, symbol, secType, expiry, strike, right, multiplier, primaryExchange, currency, localSymbol, tradingClass, position, marketPrice, marketValue, averageCost, unrealizedPnl, realizedPnl, accountName
        if (fields.Length < 15) return;

        var symbol = fields[3];
        decimal.TryParse(fields[13], out var position);
        decimal.TryParse(fields[14], out var marketPrice);
        decimal.TryParse(fields[15], out var marketValue);
        decimal.TryParse(fields[16], out var averageCost);
        decimal.TryParse(fields[17], out var unrealizedPnl);

        if (position != 0)
        {
            lock (_lockObj)
            {
                _positions[symbol] = new BrokerPosition
                {
                    Symbol = symbol,
                    Quantity = position,
                    AverageEntryPrice = averageCost,
                    CurrentPrice = marketPrice,
                    UnrealizedPnL = unrealizedPnl,
                    CostBasis = averageCost * position
                };
            }
        }
    }

    private void ProcessPositionData(string[] fields)
    {
        // Fields: msgType, account, conId, symbol, secType, expiry, strike, right, multiplier, exchange, currency, localSymbol, tradingClass, position, avgCost
        if (fields.Length < 14) return;

        var symbol = fields[4];
        decimal.TryParse(fields[13], out var position);
        decimal.TryParse(fields[14], out var avgCost);

        if (position != 0)
        {
            lock (_lockObj)
            {
                _positions[symbol] = new BrokerPosition
                {
                    Symbol = symbol,
                    Quantity = position,
                    AverageEntryPrice = avgCost,
                    CurrentPrice = avgCost,
                    UnrealizedPnL = 0,
                    CostBasis = avgCost * position
                };
            }
        }
    }

    private void ProcessPositionEnd(string[] fields)
    {
        // Positions request complete
        lock (_lockObj)
        {
            if (_requestCompletions.TryGetValue(-2, out var tcs))
            {
                tcs.TrySetResult(null);
                _requestCompletions.Remove(-2);
            }
        }
    }

    private void ProcessAccountSummary(string[] fields)
    {
        // Fields: msgType, reqId, account, tag, value, currency
        if (fields.Length < 6) return;

        var tag = fields[3];
        var value = fields[4];

        lock (_lockObj)
        {
            if (_cachedAccount is null)
            {
                _cachedAccount = new BrokerAccount
                {
                    AccountId = fields[2],
                    IsPaper = _options.UsePaperTrading
                };
            }

            switch (tag)
            {
                case "NetLiquidation":
                    if (decimal.TryParse(value, out var equity))
                        _cachedAccount.Equity = equity;
                    break;
                case "TotalCashValue":
                    if (decimal.TryParse(value, out var cash))
                        _cachedAccount.Cash = cash;
                    break;
                case "BuyingPower":
                    if (decimal.TryParse(value, out var bp))
                        _cachedAccount.BuyingPower = bp;
                    break;
            }
        }
    }

    private void ProcessAccountSummaryEnd(string[] fields)
    {
        lock (_lockObj)
        {
            if (_accountTcs is not null && _cachedAccount is not null)
            {
                _accountTcs.TrySetResult(_cachedAccount);
                _accountTcs = null;
            }
        }
    }

    private void ProcessErrorMessage(string[] fields)
    {
        // Fields: msgType, version, id, errorCode, errorMsg, advancedOrderRejectJson
        if (fields.Length < 5) return;

        int.TryParse(fields[2], out var id);
        int.TryParse(fields[3], out var errorCode);
        var errorMsg = fields[4];

        // Check if this is an order error
        if (id > 0)
        {
            lock (_lockObj)
            {
                if (_orders.TryGetValue(id, out var order))
                {
                    order.Status = BrokerOrderStatus.Rejected;

                    if (_orderCompletions.TryGetValue(id, out var tcs))
                    {
                        tcs.TrySetException(new InvalidOperationException($"Order rejected: {errorMsg} (code: {errorCode})"));
                        _orderCompletions.Remove(id);
                    }
                }
            }
        }
    }

    private void ProcessExecutionData(string[] fields)
    {
        // Process execution/fill data
        if (fields.Length < 15) return;

        if (!int.TryParse(fields[2], out var orderId)) return;

        decimal.TryParse(fields[11], out var shares);
        decimal.TryParse(fields[12], out var price);

        lock (_lockObj)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                order.FilledQuantity += shares;
                order.AverageFillPrice = price;
            }
        }
    }

    private void ProcessManagedAccounts(string[] fields)
    {
        // Fields: msgType, accountsList (comma-separated)
        if (fields.Length < 2) return;

        var accounts = fields[1].Split(',');
        if (accounts.Length > 0 && string.IsNullOrEmpty(_options.AccountId))
        {
            _options.AccountId = accounts[0].Trim();
        }
    }

    /// <summary>
    /// Disconnects from TWS/Gateway.
    /// </summary>
    public void Disconnect()
    {
        if (_isConnected)
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        _isConnected = false;
        _cts?.Cancel();

        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }

        _stream = null;
        _tcpClient = null;

        // Complete any pending operations
        lock (_lockObj)
        {
            foreach (var tcs in _orderCompletions.Values)
            {
                tcs.TrySetCanceled();
            }
            _orderCompletions.Clear();

            foreach (var tcs in _requestCompletions.Values)
            {
                tcs.TrySetCanceled();
            }
            _requestCompletions.Clear();

            _accountTcs?.TrySetCanceled();
            _accountTcs = null;
        }
    }

    /// <inheritdoc />
    public async Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        TaskCompletionSource<BrokerAccount> tcs;
        lock (_lockObj)
        {
            _cachedAccount = new BrokerAccount
            {
                AccountId = _options.AccountId ?? "Unknown",
                IsPaper = _options.UsePaperTrading
            };
            _accountTcs = new TaskCompletionSource<BrokerAccount>();
            tcs = _accountTcs;
        }

        // Request account summary
        var reqId = GetNextRequestId();
        await SendMessageAsync(new[]
        {
            REQ_ACCOUNT_SUMMARY.ToString(), "1", reqId.ToString(), "All",
            "NetLiquidation,TotalCashValue,BuyingPower"
        }, cancellationToken).ConfigureAwait(false);

        // Wait for response with timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            return await WaitWithTimeoutAsync(tcs.Task, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout - return cached or default
            lock (_lockObj)
            {
                return _cachedAccount ?? new BrokerAccount
                {
                    AccountId = _options.AccountId ?? "Unknown",
                    Equity = 0,
                    Cash = 0,
                    BuyingPower = 0,
                    IsPaper = _options.UsePaperTrading
                };
            }
        }
        finally
        {
            // Cancel account summary subscription
            await SendMessageAsync(new[] { CANCEL_ACCOUNT_SUMMARY.ToString(), "1", reqId.ToString() }, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        TaskCompletionSource<object?> tcs;
        lock (_lockObj)
        {
            _positions.Clear();
            tcs = new TaskCompletionSource<object?>();
            _requestCompletions[-2] = tcs;
        }

        // Request positions
        await SendMessageAsync(new[] { REQ_POSITIONS.ToString(), "1" }, cancellationToken).ConfigureAwait(false);

        // Wait for position end with timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await WaitWithTimeoutAsync(tcs.Task, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout - return what we have
        }
        finally
        {
            // Cancel positions subscription
            await SendMessageAsync(new[] { CANCEL_POSITIONS.ToString(), "1" }, CancellationToken.None).ConfigureAwait(false);
        }

        lock (_lockObj)
        {
            return _positions.Values.ToList();
        }
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        int orderId;
        lock (_lockObj)
        {
            orderId = _nextOrderId++;
        }

        var order = new BrokerOrder
        {
            OrderId = orderId.ToString(),
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            Side = request.Action == TradeAction.MarketBuy ? "buy" : "sell",
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            Status = BrokerOrderStatus.PendingNew,
            CreatedAt = DateTime.UtcNow
        };

        var tcs = new TaskCompletionSource<BrokerOrder>();
        lock (_lockObj)
        {
            _orders[orderId] = order;
            _orderCompletions[orderId] = tcs;
        }

        // Build place order message
        // IB PLACE_ORDER message format is complex - this is a simplified version
        var fields = new List<string>
        {
            PLACE_ORDER.ToString(),
            orderId.ToString(),
            // Contract fields
            "0", // conId
            request.Symbol,
            GetSecurityType(request.Symbol),
            "", // lastTradeDateOrContractMonth
            "0", // strike
            "", // right
            "", // multiplier
            "SMART", // exchange
            "", // primaryExchange
            "USD", // currency
            "", // localSymbol
            "", // tradingClass
            "0", // includeExpired
            // Order fields
            request.Action == TradeAction.MarketBuy ? "BUY" : "SELL",
            ((int)request.Quantity).ToString(),
            MapOrderTypeToIB(request.OrderType),
            request.LimitPrice?.ToString() ?? "",
            request.StopPrice?.ToString() ?? "",
            MapTimeInForceToIB(request.TimeInForce),
            "", // ocaGroup
            _options.AccountId ?? "",
            "", // openClose
            "0", // origin
            "", // orderRef
            "1", // transmit
            "0", // parentId
            "0", // blockOrder
            "0", // sweepToFill
            "0", // displaySize
            "0", // triggerMethod
            "0", // outsideRth
            "0", // hidden
        };

        await SendMessageAsync(fields.ToArray(), cancellationToken).ConfigureAwait(false);

        // Wait for order confirmation with timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            return await WaitWithTimeoutAsync(tcs.Task, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout - return current order state
            lock (_lockObj)
            {
                return _orders.TryGetValue(orderId, out var currentOrder) ? currentOrder : order;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        if (!int.TryParse(orderId, out var ibOrderId))
        {
            return false;
        }

        lock (_lockObj)
        {
            if (_orders.TryGetValue(ibOrderId, out var order))
            {
                order.Status = BrokerOrderStatus.PendingCancel;
            }
        }

        // Send cancel order message
        await SendMessageAsync(new[]
        {
            CANCEL_ORDER.ToString(),
            "1", // version
            orderId,
            "" // manualOrderCancelTime
        }, cancellationToken).ConfigureAwait(false);

        // Wait briefly for confirmation
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        lock (_lockObj)
        {
            if (_orders.TryGetValue(ibOrderId, out var order))
            {
                if (order.Status == BrokerOrderStatus.PendingCancel)
                {
                    order.Status = BrokerOrderStatus.Cancelled;
                }
                return order.Status == BrokerOrderStatus.Cancelled;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        if (!int.TryParse(orderId, out var ibOrderId))
        {
            throw new ArgumentException($"Invalid order ID: {orderId}", nameof(orderId));
        }

        lock (_lockObj)
        {
            if (_orders.TryGetValue(ibOrderId, out var order))
            {
                return order;
            }
        }

        // Request open orders to refresh
        await SendMessageAsync(new[] { REQ_OPEN_ORDERS.ToString(), "1" }, cancellationToken).ConfigureAwait(false);
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        lock (_lockObj)
        {
            if (_orders.TryGetValue(ibOrderId, out var order))
            {
                return order;
            }
        }

        throw new InvalidOperationException($"Order not found: {orderId}");
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> ClosePositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var positions = await GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var position = positions.FirstOrDefault(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        if (position is null || position.Quantity == 0)
        {
            throw new InvalidOperationException($"No position found for symbol: {symbol}");
        }

        var closeRequest = new ExtendedTradeRequest
        {
            Symbol = symbol,
            Action = position.Quantity > 0 ? TradeAction.MarketSell : TradeAction.MarketBuy,
            Quantity = Math.Abs(position.Quantity),
            OrderType = OrderType.Market,
            TimeInForce = TimeInForce.Day
        };

        return await SubmitOrderAsync(closeRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerOrder>> CloseAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var positions = await GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var orders = new List<BrokerOrder>();

        foreach (var position in positions)
        {
            if (position.Quantity != 0)
            {
                var order = await ClosePositionAsync(position.Symbol, cancellationToken).ConfigureAwait(false);
                orders.Add(order);
            }
        }

        return orders;
    }

    private async Task SendMessageAsync(string[] fields, CancellationToken cancellationToken)
    {
        if (_stream is null) return;

        var message = BuildMessage(fields);
        await _stream.WriteAsync(message, 0, message.Length, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildMessage(string[] fields)
    {
        var sb = new StringBuilder();
        foreach (var field in fields)
        {
            sb.Append(field);
            sb.Append('\0');
        }

        var bodyBytes = Encoding.ASCII.GetBytes(sb.ToString());
        var lengthBytes = BitConverter.GetBytes(bodyBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }

        var result = new byte[4 + bodyBytes.Length];
        Buffer.BlockCopy(lengthBytes, 0, result, 0, 4);
        Buffer.BlockCopy(bodyBytes, 0, result, 4, bodyBytes.Length);
        return result;
    }

    private int GetNextRequestId()
    {
        lock (_lockObj)
        {
            return _nextRequestId++;
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_isConnected)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetSecurityType(string symbol)
    {
        // Determine security type from symbol format
        if (symbol.Contains("/")) return "CASH"; // Forex
        if (symbol.Length > 10 && (symbol.Contains("C") || symbol.Contains("P"))) return "OPT"; // Options
        if (symbol.EndsWith("USD") || symbol.EndsWith("BTC") || symbol.EndsWith("ETH")) return "CRYPTO";
        return "STK"; // Default to stock
    }

    private static string MapOrderTypeToIB(OrderType orderType) => orderType switch
    {
        OrderType.Market => "MKT",
        OrderType.Limit => "LMT",
        OrderType.Stop => "STP",
        OrderType.StopLimit => "STP LMT",
        OrderType.TrailingStop => "TRAIL",
        _ => "MKT"
    };

    private static string MapTimeInForceToIB(TimeInForce tif) => tif switch
    {
        TimeInForce.Day => "DAY",
        TimeInForce.GTC => "GTC",
        TimeInForce.IOC => "IOC",
        TimeInForce.FOK => "FOK",
        _ => "DAY"
    };

    private static BrokerOrderStatus MapIBStatus(string status) => status.ToUpperInvariant() switch
    {
        "PENDINGSUBMIT" => BrokerOrderStatus.PendingNew,
        "PENDINGCANCEL" => BrokerOrderStatus.PendingCancel,
        "PRESUBMITTED" => BrokerOrderStatus.PendingNew,
        "SUBMITTED" => BrokerOrderStatus.New,
        "CANCELLED" => BrokerOrderStatus.Cancelled,
        "FILLED" => BrokerOrderStatus.Filled,
        "INACTIVE" => BrokerOrderStatus.Rejected,
        "APIPENDING" => BrokerOrderStatus.PendingNew,
        "APICANCEL" => BrokerOrderStatus.Cancelled,
        _ => BrokerOrderStatus.New
    };

    private static OrderType MapIBOrderType(string orderType) => orderType.ToUpperInvariant() switch
    {
        "MKT" => OrderType.Market,
        "LMT" => OrderType.Limit,
        "STP" => OrderType.Stop,
        "STP LMT" => OrderType.StopLimit,
        "TRAIL" => OrderType.TrailingStop,
        _ => OrderType.Market
    };

    /// <summary>
    /// Waits for a task with timeout, compatible with .NET Framework 4.6.1.
    /// </summary>
    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (completedTask == task)
        {
            return await task.ConfigureAwait(false);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    /// <summary>
    /// Waits for a task with timeout, compatible with .NET Framework 4.6.1 (void return).
    /// </summary>
    private static async Task WaitWithTimeoutAsync(Task task, CancellationToken cancellationToken)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (completedTask == task)
        {
            await task.ConfigureAwait(false);
            return;
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IBBroker));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for Interactive Brokers connection.
/// </summary>
public sealed class IBOptions
{
    /// <summary>
    /// Gets or sets the TWS/Gateway host address.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the TWS/Gateway port.
    /// Default is 7497 for paper trading TWS, 7496 for live TWS,
    /// 4002 for paper Gateway, 4001 for live Gateway.
    /// </summary>
    public int Port { get; set; } = 7497;

    /// <summary>
    /// Gets or sets the client ID.
    /// Must be unique if multiple API connections are used.
    /// </summary>
    public int ClientId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the account ID.
    /// Required for multi-account setups.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Gets or sets whether to use paper trading.
    /// </summary>
    public bool UsePaperTrading { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets whether to receive market data from API instead of TWS.
    /// </summary>
    public bool UseApiMarketData { get; set; } = false;
}
