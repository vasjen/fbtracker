using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FbTracker.Configuration;
using FbTracker.Domain;

namespace FbTracker.Notifications;

public interface ISignalNotifier
{
    Task SendAsync(TradingSignal signal, CancellationToken ct);
}

public sealed class TelegramNotifier(NotificationOptions options) : ISignalNotifier, IDisposable
{
    // A private client avoids HTTP logging of a Bot API URL containing the token.
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    public async Task SendAsync(TradingSignal signal, CancellationToken ct)
    {
        if (options.DryRun) throw new InvalidOperationException("Notifications are disabled in dry-run mode.");
        using var response = await _http.PostAsJsonAsync($"https://api.telegram.org/bot{options.Token}/sendMessage", new
        {
            chat_id = options.ChatId, text = Format(signal), parse_mode = "HTML", link_preview_options = new { is_disabled = true }
        }, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Telegram rejected notification (HTTP {(int)response.StatusCode}).");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!body.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new InvalidOperationException("Telegram did not acknowledge notification.");
    }

    public static string Format(TradingSignal s)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        static string N(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
        return $"<b>FC {s.Card.Key.Season} · {E(s.Card.Name)} · {s.Card.Rating} · {E(s.Card.Version)}</b>\n" +
            $"{E(s.Card.Position)} · {(s.Key.Platform == Platform.Console ? "Console (PS/Xbox)" : "PC")} · FUTBIN #{s.Card.Key.FutbinId}\n" +
            $"<a href=\"{E(s.Card.Url.AbsoluteUri + "/market")}\">Карточка и рынок</a>\n\n" +
            $"Цена FUTBIN: <b>{N(s.BuyPrice)}</b>\nОценка продажи: {N(s.EstimatedSellPrice)}\n" +
            $"Прибыль после комиссии и резерва: <b>{N(s.ExpectedNetProfit)}</b> ({s.NetRoi.ToString("P1", CultureInfo.InvariantCulture)})\n" +
            $"Возраст цены: {s.QuoteAge.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture)} мин\n" +
            $"Buy Now в выборке за {s.Liquidity.WindowMinutes} мин: {s.Liquidity.BuyNowSales}\n" +
            $"Наблюдаемых Buy Now/ч: {s.Liquidity.ObservedBuyNowPerHour.ToString("F1", CultureInfo.InvariantCulture)}\n" +
            $"Продано среди наблюдаемых завершённых лотов: {s.Liquidity.ObservedSellThrough?.ToString("P0", CultureInfo.InvariantCulture)}\n" +
            $"Продаж после обновления цены: {s.SalesAfterQuote}\n\n" +
            "Кандидат на проверку. FUTBIN показывает выборку рынка; наличие лота по этой цене не подтверждено.";
    }
    public void Dispose() => _http.Dispose();
}
