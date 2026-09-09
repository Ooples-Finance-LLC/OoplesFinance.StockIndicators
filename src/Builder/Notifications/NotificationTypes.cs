namespace OoplesFinance.StockIndicators.Builder.Notifications;

/// <summary>
/// Interface for notification channels.
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Sends a notification asynchronously.
    /// </summary>
    Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification event data.
/// </summary>
public sealed class NotificationEvent
{
    /// <summary>
    /// Creates a new notification event.
    /// </summary>
    public NotificationEvent(SignalHandle signal, string name, double value, DateTime timestamp)
    {
        Signal = signal;
        Name = name;
        Value = value;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Signal { get; }

    /// <summary>
    /// Gets the signal name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the trigger value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Email notification options.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>
    /// Gets or sets the SMTP host. Defaults from SMTP_HOST environment variable.
    /// </summary>
    public string? SmtpHost { get; set; }

    /// <summary>
    /// Gets or sets the SMTP port. Defaults to 587.
    /// </summary>
    public int? SmtpPort { get; set; }

    /// <summary>
    /// Gets or sets the username. Defaults from SMTP_USERNAME environment variable.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password. Defaults from SMTP_PASSWORD environment variable.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the from address.
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// Gets or sets the to address.
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    /// Gets or sets the email subject template.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets whether to use SSL. Defaults to true.
    /// </summary>
    public bool? UseSsl { get; set; }
}

/// <summary>
/// SMS notification options.
/// </summary>
public sealed class SmsOptions
{
    /// <summary>
    /// Gets or sets the Twilio account SID. Defaults from TWILIO_SID environment variable.
    /// </summary>
    public string? AccountSid { get; set; }

    /// <summary>
    /// Gets or sets the Twilio auth token. Defaults from TWILIO_TOKEN environment variable.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Gets or sets the from phone number.
    /// </summary>
    public string? FromNumber { get; set; }

    /// <summary>
    /// Gets or sets the to phone number.
    /// </summary>
    public string? ToNumber { get; set; }
}

/// <summary>
/// Webhook notification options.
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>
    /// Gets or sets the webhook URL.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets custom headers.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method. Defaults to POST.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Gets or sets the content type. Defaults to application/json.
    /// </summary>
    public string? ContentType { get; set; }
}

/// <summary>
/// Telegram notification options.
/// </summary>
public sealed class TelegramOptions
{
    /// <summary>
    /// Gets or sets the bot token. Defaults from TELEGRAM_BOT_TOKEN environment variable.
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Gets or sets the chat ID.
    /// </summary>
    public string? ChatId { get; set; }
}

/// <summary>
/// Discord notification options.
/// </summary>
public sealed class DiscordOptions
{
    /// <summary>
    /// Gets or sets the webhook URL. Defaults from DISCORD_WEBHOOK_URL environment variable.
    /// </summary>
    public string? WebhookUrl { get; set; }
}

/// <summary>
/// Console notification channel.
/// </summary>
public sealed class ConsoleNotificationChannel : INotificationChannel
{
    /// <inheritdoc />
    public Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        System.Console.WriteLine($"[Signal] {notification.Name}: value={notification.Value:F4} at {notification.Timestamp:yyyy-MM-dd HH:mm:ss}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Email notification channel using SMTP.
/// </summary>
public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly EmailOptions _options;

    /// <summary>
    /// Creates a new email notification channel.
    /// </summary>
    public EmailNotificationChannel(EmailOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        var host = _options.SmtpHost ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var port = _options.SmtpPort ?? 587;
        var username = _options.Username ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = _options.Password ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var from = _options.From ?? username;
        var to = _options.To;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(to))
        {
            System.Console.WriteLine($"[Email] Configuration incomplete - Host: {host ?? "missing"}, To: {to ?? "missing"}");
            return;
        }

        try
        {
            var subject = _options.Subject ?? $"Signal Alert: {notification.Name}";
            var body = $"Signal: {notification.Name}\nValue: {notification.Value:F4}\nTime: {notification.Timestamp:yyyy-MM-dd HH:mm:ss}";

            using var client = new System.Net.Mail.SmtpClient(host, port);
            // Default to SSL unless explicitly disabled via options
            client.EnableSsl = _options.UseSsl ?? true;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                client.Credentials = new System.Net.NetworkCredential(username, password);
            }

            using var message = new System.Net.Mail.MailMessage(from ?? "noreply@example.com", to, subject, body);
            await client.SendMailAsync(message).ConfigureAwait(false);
            System.Console.WriteLine($"[Email] Sent to {to}: {notification.Name}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Email] Failed to send to {to}: {ex.Message}");
        }
    }
}

/// <summary>
/// SMS notification channel using Twilio API.
/// </summary>
public sealed class SmsNotificationChannel : INotificationChannel
{
    private readonly SmsOptions _options;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    /// <summary>
    /// Creates a new SMS notification channel.
    /// </summary>
    public SmsNotificationChannel(SmsOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        var accountSid = _options.AccountSid ?? Environment.GetEnvironmentVariable("TWILIO_SID");
        var authToken = _options.AuthToken ?? Environment.GetEnvironmentVariable("TWILIO_TOKEN");
        var fromNumber = _options.FromNumber ?? Environment.GetEnvironmentVariable("TWILIO_FROM_NUMBER");
        var toNumber = _options.ToNumber;

        if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) ||
            string.IsNullOrEmpty(fromNumber) || string.IsNullOrEmpty(toNumber))
        {
            System.Console.WriteLine($"[SMS] Configuration incomplete - AccountSid: {(string.IsNullOrEmpty(accountSid) ? "missing" : "set")}, To: {toNumber ?? "missing"}");
            return;
        }

        try
        {
            var message = $"Signal Alert: {notification.Name} = {notification.Value:F4} at {notification.Timestamp:HH:mm:ss}";
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";

            var content = new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = toNumber ?? string.Empty,
                ["From"] = fromNumber ?? string.Empty,
                ["Body"] = message
            });

            var authBytes = System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}");
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            request.Content = content;

            var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"[SMS] Sent to {toNumber}: {notification.Name}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                System.Console.WriteLine($"[SMS] Failed to send to {toNumber}: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SMS] Failed to send to {toNumber}: {ex.Message}");
        }
    }
}

/// <summary>
/// Webhook notification channel using HTTP.
/// </summary>
public sealed class WebhookNotificationChannel : INotificationChannel
{
    private readonly WebhookOptions _options;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    /// <summary>
    /// Creates a new webhook notification channel.
    /// </summary>
    public WebhookNotificationChannel(WebhookOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        var url = _options.Url;
        if (string.IsNullOrEmpty(url))
        {
            System.Console.WriteLine("[Webhook] Configuration incomplete - URL missing");
            return;
        }

        try
        {
            var method = _options.Method?.ToUpperInvariant() switch
            {
                "GET" => System.Net.Http.HttpMethod.Get,
                "PUT" => System.Net.Http.HttpMethod.Put,
                "DELETE" => System.Net.Http.HttpMethod.Delete,
                _ => System.Net.Http.HttpMethod.Post
            };

            using var request = new System.Net.Http.HttpRequestMessage(method, url);

            // Add custom headers
            if (_options.Headers is not null)
            {
                foreach (var header in _options.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Create JSON payload
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                signal = notification.Name,
                value = notification.Value,
                timestamp = notification.Timestamp.ToString("o"),
                signalId = notification.Signal.Id
            });

            var contentType = _options.ContentType ?? "application/json";
            request.Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, contentType);

            var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"[Webhook] Posted to {url}: {notification.Name}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                System.Console.WriteLine($"[Webhook] Failed to post to {url}: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Webhook] Failed to post to {url}: {ex.Message}");
        }
    }
}

/// <summary>
/// Telegram notification channel using Bot API.
/// </summary>
public sealed class TelegramNotificationChannel : INotificationChannel
{
    private readonly TelegramOptions _options;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    /// <summary>
    /// Creates a new Telegram notification channel.
    /// </summary>
    public TelegramNotificationChannel(TelegramOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        var botToken = _options.BotToken ?? Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var chatId = _options.ChatId ?? Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

        if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
        {
            System.Console.WriteLine($"[Telegram] Configuration incomplete - BotToken: {(string.IsNullOrEmpty(botToken) ? "missing" : "set")}, ChatId: {chatId ?? "missing"}");
            return;
        }

        try
        {
            var message = $"Signal Alert\n\n" +
                         $"Signal: {EscapeMarkdown(notification.Name)}\n" +
                         $"Value: {notification.Value:F4}\n" +
                         $"Time: {notification.Timestamp:yyyy-MM-dd HH:mm:ss}";

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown"
            });

            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var response = await HttpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"[Telegram] Sent to chat {chatId}: {notification.Name}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                System.Console.WriteLine($"[Telegram] Failed to send to chat {chatId}: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Telegram] Failed to send to chat {chatId}: {ex.Message}");
        }
    }

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`");
    }
}

/// <summary>
/// Discord notification channel using webhooks.
/// </summary>
public sealed class DiscordNotificationChannel : INotificationChannel
{
    private readonly DiscordOptions _options;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    /// <summary>
    /// Creates a new Discord notification channel.
    /// </summary>
    public DiscordNotificationChannel(DiscordOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        var webhookUrl = _options.WebhookUrl ?? Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");

        if (string.IsNullOrEmpty(webhookUrl))
        {
            System.Console.WriteLine("[Discord] Configuration incomplete - WebhookUrl missing");
            return;
        }

        try
        {
            // Discord webhook payload with embed for better formatting
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "Signal Alert",
                        color = 3447003, // Blue color
                        fields = new[]
                        {
                            new { name = "Signal", value = notification.Name, inline = true },
                            new { name = "Value", value = notification.Value.ToString("F4"), inline = true },
                            new { name = "Time", value = notification.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), inline = false }
                        },
                        timestamp = notification.Timestamp.ToString("o")
                    }
                }
            });

            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var response = await HttpClient.PostAsync(webhookUrl, content, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"[Discord] Posted webhook: {notification.Name}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                System.Console.WriteLine($"[Discord] Failed to post webhook: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Discord] Failed to post webhook: {ex.Message}");
        }
    }
}

/// <summary>
/// WebSocket notification options.
/// </summary>
public sealed class WebSocketOptions
{
    /// <summary>
    /// Gets or sets the WebSocket server URI.
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Gets or sets the reconnect interval in milliseconds. Defaults to 5000ms.
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to auto-reconnect on disconnect. Defaults to true.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum reconnect attempts. Defaults to 10.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>
    /// Gets or sets custom headers for the WebSocket connection.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// WebSocket notification channel for real-time signal push.
/// </summary>
public sealed class WebSocketNotificationChannel : INotificationChannel, IDisposable
{
    private readonly WebSocketOptions _options;
    private System.Net.WebSockets.ClientWebSocket? _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _reconnectAttempts;
    private bool _disposed;

    /// <summary>
    /// Creates a new WebSocket notification channel.
    /// </summary>
    public WebSocketNotificationChannel(WebSocketOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.Uri))
        {
            System.Console.WriteLine("[WebSocket] Configuration incomplete - URI missing");
            return;
        }

        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            if (_webSocket is null || _webSocket.State != System.Net.WebSockets.WebSocketState.Open)
            {
                System.Console.WriteLine("[WebSocket] Not connected, message dropped");
                return;
            }

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "signal",
                signal = notification.Name,
                value = notification.Value,
                timestamp = notification.Timestamp.ToString("o"),
                signalId = notification.Signal.Id
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
            var segment = new ArraySegment<byte>(bytes);

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _webSocket.SendAsync(segment, System.Net.WebSockets.WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                System.Console.WriteLine($"[WebSocket] Sent: {notification.Name}");
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WebSocket] Failed to send: {ex.Message}");
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_webSocket is not null && _webSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            return;
        }

        // Clean up existing WebSocket if in a bad state
        if (_webSocket is not null)
        {
            try { _webSocket.Dispose(); } catch { /* Ignore disposal errors */ }
            _webSocket = null;
        }

        if (!_options.AutoReconnect && _reconnectAttempts > 0)
        {
            return;
        }

        if (_reconnectAttempts >= _options.MaxReconnectAttempts)
        {
            System.Console.WriteLine($"[WebSocket] Max reconnect attempts ({_options.MaxReconnectAttempts}) reached");
            return;
        }

        _reconnectAttempts++;
        _webSocket = new System.Net.WebSockets.ClientWebSocket();

        // Add custom headers
        if (_options.Headers is not null)
        {
            foreach (var header in _options.Headers)
            {
                _webSocket.Options.SetRequestHeader(header.Key, header.Value);
            }
        }

        try
        {
            var uri = new Uri(_options.Uri ?? throw new InvalidOperationException("WebSocket URI not set"));
            await _webSocket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            _reconnectAttempts = 0; // Reset on successful connection
            System.Console.WriteLine($"[WebSocket] Connected to {_options.Uri}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[WebSocket] Connection failed: {ex.Message}");
            try { _webSocket.Dispose(); } catch { /* Ignore */ }
            _webSocket = null;

            if (_options.AutoReconnect && _reconnectAttempts < _options.MaxReconnectAttempts)
            {
                await Task.Delay(_options.ReconnectIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Disconnects the WebSocket.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket is not null && _webSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken).ConfigureAwait(false);
                System.Console.WriteLine("[WebSocket] Disconnected");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[WebSocket] Error during disconnect: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _sendLock.Dispose();
        if (_webSocket is not null)
        {
            try { _webSocket.Dispose(); } catch { /* Ignore */ }
            _webSocket = null;
        }
    }
}

/// <summary>
/// Notification routing rule that maps signals to specific channels.
/// </summary>
public sealed class NotificationRoute
{
    /// <summary>
    /// Gets or sets the signal name pattern (supports wildcards: * matches any characters).
    /// </summary>
    public string? SignalPattern { get; set; }

    /// <summary>
    /// Gets or sets specific signal handles to match.
    /// </summary>
    public List<SignalHandle>? SignalHandles { get; set; }

    /// <summary>
    /// Gets or sets the notification channels for this route.
    /// </summary>
    public List<INotificationChannel> Channels { get; set; } = new();

    /// <summary>
    /// Checks if this route matches the given notification.
    /// </summary>
    public bool Matches(NotificationEvent notification)
    {
        // Match by handle
        if (SignalHandles is not null && SignalHandles.Count > 0)
        {
            if (SignalHandles.Contains(notification.Signal))
            {
                return true;
            }
        }

        // Match by pattern
        return SignalPatternMatcher.IsMatch(SignalPattern, notification.Name);
    }
}

/// <summary>
/// Builder for notification routes.
/// </summary>
public sealed class NotificationRouteBuilder
{
    private readonly NotificationRoute _route = new();
    private readonly Action<NotificationRoute> _onComplete;

    internal NotificationRouteBuilder(string signalPattern, Action<NotificationRoute> onComplete)
    {
        _route.SignalPattern = signalPattern;
        _onComplete = onComplete;
    }

    internal NotificationRouteBuilder(SignalHandle handle, Action<NotificationRoute> onComplete)
    {
        _route.SignalHandles = new List<SignalHandle> { handle };
        _onComplete = onComplete;
    }

    /// <summary>
    /// Sends notifications to a WebSocket endpoint.
    /// </summary>
    public NotificationRouteBuilder SendWebSocket(WebSocketOptions options)
    {
        _route.Channels.Add(new WebSocketNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Sends notifications to a WebSocket endpoint.
    /// </summary>
    public NotificationRouteBuilder SendWebSocket(string uri)
    {
        return SendWebSocket(new WebSocketOptions { Uri = uri });
    }

    /// <summary>
    /// Sends notifications via email.
    /// </summary>
    public NotificationRouteBuilder SendEmail(EmailOptions options)
    {
        _route.Channels.Add(new EmailNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Sends notifications to a webhook.
    /// </summary>
    public NotificationRouteBuilder SendWebhook(WebhookOptions options)
    {
        _route.Channels.Add(new WebhookNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Sends notifications to a webhook URL.
    /// </summary>
    public NotificationRouteBuilder SendWebhook(string url)
    {
        return SendWebhook(new WebhookOptions { Url = url });
    }

    /// <summary>
    /// Sends notifications via SMS.
    /// </summary>
    public NotificationRouteBuilder SendSms(SmsOptions options)
    {
        _route.Channels.Add(new SmsNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Sends notifications to Telegram.
    /// </summary>
    public NotificationRouteBuilder SendTelegram(TelegramOptions? options = null)
    {
        _route.Channels.Add(new TelegramNotificationChannel(options ?? new TelegramOptions()));
        return this;
    }

    /// <summary>
    /// Sends notifications to Discord.
    /// </summary>
    public NotificationRouteBuilder SendDiscord(DiscordOptions? options = null)
    {
        _route.Channels.Add(new DiscordNotificationChannel(options ?? new DiscordOptions()));
        return this;
    }

    /// <summary>
    /// Sends notifications to the console.
    /// </summary>
    public NotificationRouteBuilder SendConsole()
    {
        _route.Channels.Add(new ConsoleNotificationChannel());
        return this;
    }

    /// <summary>
    /// Sends notifications to a custom channel.
    /// </summary>
    public NotificationRouteBuilder SendTo(INotificationChannel channel)
    {
        _route.Channels.Add(channel);
        return this;
    }

    /// <summary>
    /// Completes the route configuration and registers it.
    /// </summary>
    internal void Complete()
    {
        _onComplete(_route);
    }
}
