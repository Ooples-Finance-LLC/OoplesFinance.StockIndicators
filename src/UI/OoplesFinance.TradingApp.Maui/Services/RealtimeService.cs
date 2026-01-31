using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Real-time subscription service using Supabase Realtime.
/// Provides live updates for positions, orders, alerts, and prices.
/// </summary>
public sealed class RealtimeService : IDisposable
{
    private readonly string _supabaseUrl;
    private readonly string _anonKey;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private readonly Dictionary<string, List<Action<RealtimePayload>>> _subscriptions = new();
    private readonly object _lock = new();
    private int _messageRef = 0;
    private bool _disposed;

    /// <summary>
    /// Event raised when connection status changes.
    /// </summary>
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Event raised when a real-time message is received.
    /// </summary>
    public event EventHandler<RealtimeMessageEventArgs>? MessageReceived;

    /// <summary>
    /// Gets whether the WebSocket is connected.
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public RealtimeService(string supabaseUrl, string anonKey)
    {
        _supabaseUrl = supabaseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(supabaseUrl));
        _anonKey = anonKey ?? throw new ArgumentNullException(nameof(anonKey));
    }

    /// <summary>
    /// Connects to Supabase Realtime.
    /// </summary>
    public async Task ConnectAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _webSocket = new ClientWebSocket();

        // Build WebSocket URL
        var wsUrl = _supabaseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
        var uri = new Uri($"{wsUrl}/realtime/v1/websocket?apikey={_anonKey}&vsn=1.0.0");

        try
        {
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token).ConfigureAwait(false);
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(true));

            // Start receiving messages
            _receiveTask = ReceiveMessagesAsync(_cancellationTokenSource.Token);

            // Send heartbeat to keep connection alive
            _ = HeartbeatAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false, ex.Message));
            throw;
        }
    }

    /// <summary>
    /// Disconnects from Supabase Realtime.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_webSocket is null)
            return;

        _cancellationTokenSource?.Cancel();

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore close errors
            }
        }

        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false));
    }

    /// <summary>
    /// Subscribes to changes on a table.
    /// </summary>
    public async Task<string> SubscribeAsync(
        string table,
        string schema = "public",
        string? filter = null,
        Action<RealtimePayload>? onInsert = null,
        Action<RealtimePayload>? onUpdate = null,
        Action<RealtimePayload>? onDelete = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Realtime. Call ConnectAsync first.");

        var topic = $"realtime:{schema}:{table}";
        if (!string.IsNullOrEmpty(filter))
            topic += $":{filter}";

        var channelId = Guid.NewGuid().ToString("N")[..8];

        // Register callbacks
        lock (_lock)
        {
            if (!_subscriptions.ContainsKey(topic))
                _subscriptions[topic] = new List<Action<RealtimePayload>>();

            if (onInsert is not null)
                _subscriptions[topic].Add(p => { if (p.EventType == "INSERT") onInsert(p); });
            if (onUpdate is not null)
                _subscriptions[topic].Add(p => { if (p.EventType == "UPDATE") onUpdate(p); });
            if (onDelete is not null)
                _subscriptions[topic].Add(p => { if (p.EventType == "DELETE") onDelete(p); });
        }

        // Send join message
        var joinMessage = new
        {
            topic,
            @event = "phx_join",
            payload = new
            {
                config = new
                {
                    broadcast = new { self = false },
                    presence = new { key = string.Empty },
                    postgres_changes = new[]
                    {
                        new
                        {
                            @event = "*",
                            schema,
                            table,
                            filter = filter ?? string.Empty
                        }
                    }
                }
            },
            @ref = Interlocked.Increment(ref _messageRef).ToString()
        };

        await SendMessageAsync(joinMessage, cancellationToken).ConfigureAwait(false);

        return channelId;
    }

    /// <summary>
    /// Subscribes to position changes for the current user.
    /// </summary>
    public Task<string> SubscribeToPositionsAsync(
        string userId,
        Action<RealtimePayload> onPositionChanged,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(
            table: "positions",
            filter: $"user_id=eq.{userId}",
            onInsert: onPositionChanged,
            onUpdate: onPositionChanged,
            onDelete: onPositionChanged,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Subscribes to order changes for the current user.
    /// </summary>
    public Task<string> SubscribeToOrdersAsync(
        string userId,
        Action<RealtimePayload> onOrderChanged,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(
            table: "orders",
            filter: $"user_id=eq.{userId}",
            onInsert: onOrderChanged,
            onUpdate: onOrderChanged,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Subscribes to triggered alerts for the current user.
    /// </summary>
    public Task<string> SubscribeToAlertsAsync(
        string userId,
        Action<RealtimePayload> onAlertTriggered,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(
            table: "price_alerts",
            filter: $"user_id=eq.{userId}",
            onUpdate: payload =>
            {
                // Only notify when alert is triggered
                if (payload.NewRecord?.TryGetValue("is_triggered", out var triggered) == true &&
                    triggered?.ToString() == "true")
                {
                    onAlertTriggered(payload);
                }
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from a channel.
    /// </summary>
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return;

        lock (_lock)
        {
            _subscriptions.Remove(topic);
        }

        var leaveMessage = new
        {
            topic,
            @event = "phx_leave",
            payload = new { },
            @ref = Interlocked.Increment(ref _messageRef).ToString()
        };

        await SendMessageAsync(leaveMessage, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(json);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Realtime receive error: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false, ex.Message));
                break;
            }
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<RealtimeMessage>(json);
            if (message is null)
                return;

            // Handle postgres_changes events
            if (message.Event == "postgres_changes" && message.Payload is not null)
            {
                var payload = new RealtimePayload
                {
                    Topic = message.Topic ?? string.Empty,
                    EventType = message.Payload.Type ?? string.Empty,
                    Table = message.Payload.Table ?? string.Empty,
                    Schema = message.Payload.Schema ?? string.Empty,
                    OldRecord = message.Payload.OldRecord,
                    NewRecord = message.Payload.Record,
                    Timestamp = DateTime.UtcNow
                };

                // Notify subscribers
                lock (_lock)
                {
                    if (_subscriptions.TryGetValue(message.Topic ?? string.Empty, out var callbacks))
                    {
                        foreach (var callback in callbacks)
                        {
                            try
                            {
                                callback(payload);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Subscription callback error: {ex.Message}");
                            }
                        }
                    }
                }

                MessageReceived?.Invoke(this, new RealtimeMessageEventArgs(payload));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing realtime message: {ex.Message}");
        }
    }

    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

                var heartbeat = new
                {
                    topic = "phoenix",
                    @event = "heartbeat",
                    payload = new { },
                    @ref = Interlocked.Increment(ref _messageRef).ToString()
                };

                await SendMessageAsync(heartbeat, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Heartbeat error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

#region Data Types

/// <summary>
/// Payload received from Supabase Realtime.
/// </summary>
public sealed class RealtimePayload
{
    public string Topic { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public Dictionary<string, object?>? OldRecord { get; set; }
    public Dictionary<string, object?>? NewRecord { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets a value from the new record.
    /// </summary>
    public T? GetValue<T>(string key)
    {
        if (NewRecord?.TryGetValue(key, out var value) == true && value is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }
}

/// <summary>
/// Event args for connection status changes.
/// </summary>
public sealed class ConnectionStatusChangedEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public string? ErrorMessage { get; }

    public ConnectionStatusChangedEventArgs(bool isConnected, string? errorMessage = null)
    {
        IsConnected = isConnected;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event args for realtime messages.
/// </summary>
public sealed class RealtimeMessageEventArgs : EventArgs
{
    public RealtimePayload Payload { get; }

    public RealtimeMessageEventArgs(RealtimePayload payload)
    {
        Payload = payload;
    }
}

internal sealed class RealtimeMessage
{
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("payload")]
    public RealtimeMessagePayload? Payload { get; set; }

    [JsonPropertyName("ref")]
    public string? Ref { get; set; }
}

internal sealed class RealtimeMessagePayload
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("table")]
    public string? Table { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("record")]
    public Dictionary<string, object?>? Record { get; set; }

    [JsonPropertyName("old_record")]
    public Dictionary<string, object?>? OldRecord { get; set; }
}

#endregion
