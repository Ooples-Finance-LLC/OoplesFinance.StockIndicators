using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Notification channel types
/// </summary>
public enum NotificationChannel
{
    Push,
    Email,
    Sms,
    Telegram,
    Discord
}

/// <summary>
/// Notification priority levels
/// </summary>
public enum NotificationPriority
{
    Low,      // Daily summaries, informational
    Normal,   // Trade confirmations, price alerts
    High,     // Strategy signals, risk warnings
    Critical  // Margin calls, circuit breakers, large losses
}

/// <summary>
/// Trading notification model
/// </summary>
public class TradingNotification
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string? Symbol { get; set; }
    public string? StrategyName { get; set; }
    public decimal? Price { get; set; }
    public decimal? PnL { get; set; }
    public string? Action { get; set; } // BUY, SELL, ALERT
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Multi-channel notification service interface
/// </summary>
public interface IMultiChannelNotificationService
{
    Task SendAsync(TradingNotification notification, IEnumerable<NotificationChannel> channels, CancellationToken ct = default);
    Task SendToAllEnabledAsync(TradingNotification notification, CancellationToken ct = default);
    Task<bool> TestChannelAsync(NotificationChannel channel, CancellationToken ct = default);
}

/// <summary>
/// Telegram Bot notification service
/// </summary>
public class TelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;
    private const string TelegramApiUrl = "https://api.telegram.org";

    public TelegramNotificationService(string botToken, string chatId)
    {
        _botToken = botToken;
        _chatId = chatId;
        _httpClient = new HttpClient { BaseAddress = new Uri(TelegramApiUrl) };
    }

    public async Task<bool> SendMessageAsync(TradingNotification notification, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_chatId))
            return false;

        try
        {
            var message = FormatMessage(notification);
            var response = await _httpClient.PostAsJsonAsync(
                $"/bot{_botToken}/sendMessage",
                new
                {
                    chat_id = _chatId,
                    text = message,
                    parse_mode = "HTML",
                    disable_web_page_preview = true
                },
                ct);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendPhotoAsync(string photoUrl, string caption, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_chatId))
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/bot{_botToken}/sendPhoto",
                new
                {
                    chat_id = _chatId,
                    photo = photoUrl,
                    caption = caption,
                    parse_mode = "HTML"
                },
                ct);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/bot{_botToken}/getMe", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string FormatMessage(TradingNotification notification)
    {
        var sb = new StringBuilder();
        var emoji = GetPriorityEmoji(notification.Priority);

        sb.AppendLine($"{emoji} <b>{notification.Title}</b>");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(notification.Symbol))
            sb.AppendLine($"<b>Symbol:</b> {notification.Symbol}");

        if (!string.IsNullOrEmpty(notification.Action))
            sb.AppendLine($"<b>Action:</b> {notification.Action}");

        if (notification.Price.HasValue)
            sb.AppendLine($"<b>Price:</b> ${notification.Price:N2}");

        if (notification.PnL.HasValue)
        {
            var pnlEmoji = notification.PnL >= 0 ? "📈" : "📉";
            sb.AppendLine($"<b>P&L:</b> {pnlEmoji} ${notification.PnL:N2}");
        }

        if (!string.IsNullOrEmpty(notification.StrategyName))
            sb.AppendLine($"<b>Strategy:</b> {notification.StrategyName}");

        sb.AppendLine();
        sb.AppendLine(notification.Message);
        sb.AppendLine();
        sb.AppendLine($"<i>{notification.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</i>");

        return sb.ToString();
    }

    private static string GetPriorityEmoji(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Critical => "🚨",
        NotificationPriority.High => "⚠️",
        NotificationPriority.Normal => "📊",
        NotificationPriority.Low => "ℹ️",
        _ => "📊"
    };
}

/// <summary>
/// Discord webhook notification service
/// </summary>
public class DiscordNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public DiscordNotificationService(string webhookUrl)
    {
        _webhookUrl = webhookUrl;
        _httpClient = new HttpClient();
    }

    public async Task<bool> SendMessageAsync(TradingNotification notification, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return false;

        try
        {
            var embed = CreateEmbed(notification);
            var payload = new
            {
                username = "Ooples Trading Bot",
                avatar_url = "https://ooplesfinance.com/logo.png",
                embeds = new[] { embed }
            };

            var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // Discord webhooks don't have a test endpoint, so we send a test message
            var testNotification = new TradingNotification
            {
                Title = "Connection Test",
                Message = "Discord webhook is connected successfully!",
                Priority = NotificationPriority.Low
            };

            return await SendMessageAsync(testNotification, ct);
        }
        catch
        {
            return false;
        }
    }

    private object CreateEmbed(TradingNotification notification)
    {
        var color = GetPriorityColor(notification.Priority);
        var fields = new List<object>();

        if (!string.IsNullOrEmpty(notification.Symbol))
        {
            fields.Add(new { name = "Symbol", value = notification.Symbol, inline = true });
        }

        if (!string.IsNullOrEmpty(notification.Action))
        {
            fields.Add(new { name = "Action", value = notification.Action, inline = true });
        }

        if (notification.Price.HasValue)
        {
            fields.Add(new { name = "Price", value = $"${notification.Price:N2}", inline = true });
        }

        if (notification.PnL.HasValue)
        {
            var pnlText = notification.PnL >= 0 ? $"+${notification.PnL:N2}" : $"-${Math.Abs(notification.PnL.Value):N2}";
            fields.Add(new { name = "P&L", value = pnlText, inline = true });
        }

        if (!string.IsNullOrEmpty(notification.StrategyName))
        {
            fields.Add(new { name = "Strategy", value = notification.StrategyName, inline = true });
        }

        return new
        {
            title = notification.Title,
            description = notification.Message,
            color = color,
            fields = fields.ToArray(),
            timestamp = notification.Timestamp.ToString("o"),
            footer = new { text = "Ooples Trading Platform" }
        };
    }

    private static int GetPriorityColor(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Critical => 0xFF0000, // Red
        NotificationPriority.High => 0xFFA500,     // Orange
        NotificationPriority.Normal => 0x00FF00,   // Green
        NotificationPriority.Low => 0x808080,      // Gray
        _ => 0x00FF00
    };
}

/// <summary>
/// Twilio SMS notification service
/// </summary>
public class TwilioSmsService
{
    private readonly HttpClient _httpClient;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;

    public TwilioSmsService(string accountSid, string authToken, string fromNumber)
    {
        _accountSid = accountSid;
        _authToken = authToken;
        _fromNumber = fromNumber;
        _httpClient = new HttpClient();

        var authBytes = Encoding.ASCII.GetBytes($"{accountSid}:{authToken}");
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    public async Task<bool> SendSmsAsync(string toNumber, TradingNotification notification, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken))
            return false;

        try
        {
            var message = FormatSmsMessage(notification);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = toNumber,
                ["From"] = _fromNumber,
                ["Body"] = message
            });

            var response = await _httpClient.PostAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json",
                content,
                ct);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}.json",
                ct);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string FormatSmsMessage(TradingNotification notification)
    {
        var sb = new StringBuilder();
        var emoji = notification.Priority == NotificationPriority.Critical ? "🚨" : "📊";

        sb.Append($"{emoji} {notification.Title}");

        if (!string.IsNullOrEmpty(notification.Symbol))
            sb.Append($" | {notification.Symbol}");

        if (!string.IsNullOrEmpty(notification.Action))
            sb.Append($" | {notification.Action}");

        if (notification.Price.HasValue)
            sb.Append($" @ ${notification.Price:N2}");

        if (notification.PnL.HasValue)
            sb.Append($" | P&L: ${notification.PnL:N2}");

        // SMS has 160 char limit for single message
        var result = sb.ToString();
        return result.Length > 160 ? result[..157] + "..." : result;
    }
}

/// <summary>
/// SendGrid email notification service
/// </summary>
public class SendGridEmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailService(string apiKey, string fromEmail, string fromName = "Ooples Trading")
    {
        _apiKey = apiKey;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _httpClient = new HttpClient { BaseAddress = new Uri("https://api.sendgrid.com") };
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<bool> SendEmailAsync(string toEmail, TradingNotification notification, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_fromEmail))
            return false;

        try
        {
            var htmlContent = CreateHtmlEmail(notification);
            var payload = new
            {
                personalizations = new[]
                {
                    new { to = new[] { new { email = toEmail } } }
                },
                from = new { email = _fromEmail, name = _fromName },
                subject = $"{GetPriorityPrefix(notification.Priority)} {notification.Title}",
                content = new[]
                {
                    new { type = "text/html", value = htmlContent }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/v3/mail/send", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/v3/user/profile", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string CreateHtmlEmail(TradingNotification notification)
    {
        var priorityColor = GetPriorityColor(notification.Priority);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .header {{ background: {priorityColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; }}
        .field {{ margin-bottom: 15px; }}
        .field-label {{ font-weight: bold; color: #666; margin-bottom: 5px; }}
        .field-value {{ font-size: 18px; color: #333; }}
        .pnl-positive {{ color: #22c55e; }}
        .pnl-negative {{ color: #ef4444; }}
        .footer {{ background: #f5f5f5; padding: 15px; text-align: center; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin:0;'>{notification.Title}</h1>
        </div>
        <div class='content'>
            {(notification.Symbol is not null ? $"<div class='field'><div class='field-label'>Symbol</div><div class='field-value'>{notification.Symbol}</div></div>" : "")}
            {(notification.Action is not null ? $"<div class='field'><div class='field-label'>Action</div><div class='field-value'>{notification.Action}</div></div>" : "")}
            {(notification.Price.HasValue ? $"<div class='field'><div class='field-label'>Price</div><div class='field-value'>${notification.Price:N2}</div></div>" : "")}
            {(notification.PnL.HasValue ? $"<div class='field'><div class='field-label'>P&L</div><div class='field-value {(notification.PnL >= 0 ? "pnl-positive" : "pnl-negative")}'>${notification.PnL:N2}</div></div>" : "")}
            {(notification.StrategyName is not null ? $"<div class='field'><div class='field-label'>Strategy</div><div class='field-value'>{notification.StrategyName}</div></div>" : "")}
            <div class='field'>
                <div class='field-label'>Details</div>
                <div class='field-value'>{notification.Message}</div>
            </div>
        </div>
        <div class='footer'>
            Ooples Trading Platform | {notification.Timestamp:yyyy-MM-dd HH:mm:ss} UTC
        </div>
    </div>
</body>
</html>";
    }

    private static string GetPriorityColor(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Critical => "#dc2626",
        NotificationPriority.High => "#f97316",
        NotificationPriority.Normal => "#22c55e",
        NotificationPriority.Low => "#6b7280",
        _ => "#22c55e"
    };

    private static string GetPriorityPrefix(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Critical => "[CRITICAL]",
        NotificationPriority.High => "[ALERT]",
        NotificationPriority.Normal => "",
        NotificationPriority.Low => "[INFO]",
        _ => ""
    };
}

/// <summary>
/// Unified multi-channel notification service
/// </summary>
public class MultiChannelNotificationService : IMultiChannelNotificationService
{
    private readonly TelegramNotificationService? _telegram;
    private readonly DiscordNotificationService? _discord;
    private readonly TwilioSmsService? _twilio;
    private readonly SendGridEmailService? _sendGrid;
    private readonly INotificationService _pushNotifications;
    private readonly NotificationSettings _settings;

    public MultiChannelNotificationService(
        NotificationSettings settings,
        INotificationService pushNotifications)
    {
        _settings = settings;
        _pushNotifications = pushNotifications;

        // Initialize services based on configuration
        if (!string.IsNullOrEmpty(settings.TelegramBotToken) && !string.IsNullOrEmpty(settings.TelegramChatId))
            _telegram = new TelegramNotificationService(settings.TelegramBotToken, settings.TelegramChatId);

        if (!string.IsNullOrEmpty(settings.DiscordWebhookUrl))
            _discord = new DiscordNotificationService(settings.DiscordWebhookUrl);

        if (!string.IsNullOrEmpty(settings.TwilioAccountSid) && !string.IsNullOrEmpty(settings.TwilioAuthToken))
            _twilio = new TwilioSmsService(settings.TwilioAccountSid, settings.TwilioAuthToken, settings.TwilioFromNumber);

        if (!string.IsNullOrEmpty(settings.SendGridApiKey))
            _sendGrid = new SendGridEmailService(settings.SendGridApiKey, settings.SendGridFromEmail);
    }

    public async Task SendAsync(TradingNotification notification, IEnumerable<NotificationChannel> channels, CancellationToken ct = default)
    {
        var tasks = new List<Task>();

        foreach (var channel in channels)
        {
            switch (channel)
            {
                case NotificationChannel.Push:
                    tasks.Add(_pushNotifications.ShowLocalNotificationAsync(notification.Title, notification.Message));
                    break;

                case NotificationChannel.Telegram when _telegram is not null:
                    tasks.Add(_telegram.SendMessageAsync(notification, ct));
                    break;

                case NotificationChannel.Discord when _discord is not null:
                    tasks.Add(_discord.SendMessageAsync(notification, ct));
                    break;

                case NotificationChannel.Sms when _twilio is not null && !string.IsNullOrEmpty(_settings.UserPhoneNumber):
                    tasks.Add(_twilio.SendSmsAsync(_settings.UserPhoneNumber, notification, ct));
                    break;

                case NotificationChannel.Email when _sendGrid is not null && !string.IsNullOrEmpty(_settings.UserEmail):
                    tasks.Add(_sendGrid.SendEmailAsync(_settings.UserEmail, notification, ct));
                    break;
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task SendToAllEnabledAsync(TradingNotification notification, CancellationToken ct = default)
    {
        var enabledChannels = new List<NotificationChannel>();

        if (_settings.EnablePush)
            enabledChannels.Add(NotificationChannel.Push);

        if (_settings.EnableTelegram && _telegram is not null)
            enabledChannels.Add(NotificationChannel.Telegram);

        if (_settings.EnableDiscord && _discord is not null)
            enabledChannels.Add(NotificationChannel.Discord);

        // SMS and Email only for high priority notifications
        if (notification.Priority >= NotificationPriority.High)
        {
            if (_settings.EnableSms && _twilio is not null)
                enabledChannels.Add(NotificationChannel.Sms);

            if (_settings.EnableEmail && _sendGrid is not null)
                enabledChannels.Add(NotificationChannel.Email);
        }

        await SendAsync(notification, enabledChannels, ct);
    }

    public async Task<bool> TestChannelAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        return channel switch
        {
            NotificationChannel.Telegram when _telegram is not null => await _telegram.TestConnectionAsync(ct),
            NotificationChannel.Discord when _discord is not null => await _discord.TestConnectionAsync(ct),
            NotificationChannel.Sms when _twilio is not null => await _twilio.TestConnectionAsync(ct),
            NotificationChannel.Email when _sendGrid is not null => await _sendGrid.TestConnectionAsync(ct),
            _ => false
        };
    }
}

/// <summary>
/// Notification settings configuration
/// </summary>
public class NotificationSettings
{
    // Channel enables
    public bool EnablePush { get; set; } = true;
    public bool EnableTelegram { get; set; } = false;
    public bool EnableDiscord { get; set; } = false;
    public bool EnableSms { get; set; } = false;
    public bool EnableEmail { get; set; } = false;

    // Telegram
    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;

    // Discord
    public string DiscordWebhookUrl { get; set; } = string.Empty;

    // Twilio SMS
    public string TwilioAccountSid { get; set; } = string.Empty;
    public string TwilioAuthToken { get; set; } = string.Empty;
    public string TwilioFromNumber { get; set; } = string.Empty;

    // SendGrid Email
    public string SendGridApiKey { get; set; } = string.Empty;
    public string SendGridFromEmail { get; set; } = string.Empty;

    // User contact info
    public string UserEmail { get; set; } = string.Empty;
    public string UserPhoneNumber { get; set; } = string.Empty;
}
