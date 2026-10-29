using FbTracker.Domain;

namespace FbTracker.Configuration;

public sealed class TrackerOptions
{
    public int Season { get; set; } = 27;
    public int MinRating { get; set; } = 80;
    public int MaxRating { get; set; } = 99;
    public int MaxCatalogPages { get; set; } = 1000;
    public int CatalogRefreshMinutes { get; set; } = 360;
    public int ScanIntervalSeconds { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 2;
    public int CardLimit { get; set; } = 0;
    public Platform[] Platforms { get; set; } = [Platform.Console];
    public string SourceTimeZone { get; set; } = "Europe/London";
    public TransportOptions Transport { get; set; } = new();
    public SignalOptions Signals { get; set; } = new();
    public NotificationOptions Notifications { get; set; } = new();
    public string ObservationDirectory { get; set; } = "data/observations";

    public void Validate()
    {
        if (Season != 27) throw new ArgumentException("This adapter is validated for FC 27 only.");
        if (MinRating < 1 || MaxRating > 99 || MinRating > MaxRating || MaxCatalogPages < 1 ||
            CatalogRefreshMinutes < 1 || ScanIntervalSeconds < 1 || MaxConcurrency is < 1 or > 20 || CardLimit < 0)
            throw new ArgumentException("Invalid catalog or scheduler settings.");
        if (Platforms.Length == 0 || Platforms.Any(p => !Enum.IsDefined(p)))
            throw new ArgumentException("Select Console and/or Pc platforms.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(SourceTimeZone);
        if (Transport.Mode is not ("Browser" or "Http") || Transport.RequestTimeoutSeconds < 1 ||
            Transport.GlobalIntervalMilliseconds < 1 || Transport.SessionIntervalMilliseconds < 1 ||
            Transport.BlockCooldownSeconds < 1 || Transport.RetryCount is < 0 or > 5)
            throw new ArgumentException("Invalid transport settings.");
        if (!Notifications.DryRun && (string.IsNullOrWhiteSpace(Notifications.Token) ||
            string.IsNullOrWhiteSpace(Notifications.ChatId)))
            throw new ArgumentException("Telegram Token and ChatId are required when DryRun is false.");
        if (Notifications.CooldownMinutes < 1) throw new ArgumentException("Cooldown must be positive.");
        Signals.Validate();
    }
}

public sealed class TransportOptions
{
    public string Mode { get; set; } = "Browser";
    public bool Headless { get; set; } = true;
    public string? BrowserChannel { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int GlobalIntervalMilliseconds { get; set; } = 1500;
    public int SessionIntervalMilliseconds { get; set; } = 2000;
    public int BlockCooldownSeconds { get; set; } = 120;
    public int RetryCount { get; set; } = 2;
    public ProxyOptions[] Proxies { get; set; } = [];
    public string? ProxyProviderUrl { get; set; }
    public string? ProxyProviderApiKey { get; set; }
}

public sealed class ProxyOptions
{
    public string Server { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class NotificationOptions
{
    public bool DryRun { get; set; } = true;
    public string? Token { get; set; }
    public string? ChatId { get; set; }
    public string? RedisConnection { get; set; }
    public int CooldownMinutes { get; set; } = 20;
}

public sealed class SignalOptions
{
    public int WindowMinutes { get; set; } = 60;
    public int MinBuyNowSales { get; set; } = 5;
    public int MinObservedAuctions { get; set; } = 8;
    public decimal MinObservedBuyNowPerHour { get; set; } = 5;
    public decimal MinObservedSellThrough { get; set; } = 0.3m;
    public int MaxLastSaleAgeMinutes { get; set; } = 10;
    public int MaxQuoteAgeMinutes { get; set; } = 30;
    public int MinSalesAfterQuote { get; set; } = 2;
    public decimal MaxDispersion { get; set; } = 0.1m;
    public decimal MaxDownwardMove { get; set; } = 0.08m;
    public decimal TaxRate { get; set; } = 0.05m;
    public decimal RiskReserveRate { get; set; } = 0.02m;
    public long MinNetProfit { get; set; } = 1000;
    public decimal MinNetRoi { get; set; } = 0.03m;
    public long MinBuyPrice { get; set; } = 10000;
    public long MaxBuyPrice { get; set; } = 15000000;

    public void Validate()
    {
        if (WindowMinutes < 1 || MinBuyNowSales < 3 || MinObservedAuctions < 1 ||
            MinObservedBuyNowPerHour < 0 || MinSalesAfterQuote < 1 ||
            MaxLastSaleAgeMinutes < 1 || MaxQuoteAgeMinutes < 1 || MinNetProfit < 0 ||
            MinBuyPrice < 1 || MaxBuyPrice < MinBuyPrice ||
            new[] { MinObservedSellThrough, MaxDispersion, MaxDownwardMove, TaxRate,
                RiskReserveRate, MinNetRoi }.Any(x => x < 0 || x >= 1) || TaxRate + RiskReserveRate >= 1)
            throw new ArgumentException("Invalid signal thresholds.");
    }
}
