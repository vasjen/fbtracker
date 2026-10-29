using FbTracker.Configuration;
using FbTracker.Domain;

namespace FbTracker.Analysis;

public sealed class SignalAnalyzer(SignalOptions options)
{
    public SignalDecision Evaluate(PlayerCard card, MarketSnapshot snapshot)
    {
        var now = snapshot.ObservedAt;
        var windowStart = now.AddMinutes(-options.WindowMinutes);
        // Do not combine polls: identical auction rows have no stable auction ID and may be real distinct sales.
        var ended = snapshot.Auctions.Where(a => a.EndedAt >= windowStart && a.EndedAt <= now &&
            a.Status is SaleStatus.Sold or SaleStatus.Unsold).ToArray();
        var sold = ended.Where(a => a.Status == SaleStatus.Sold && a.SoldFor > 0).ToArray();
        var bin = sold.Where(a => a.Type == SaleType.BuyNow).OrderBy(a => a.EndedAt).ToArray();
        var liquidity = new Liquidity(ended.Length, sold.Length, bin.Length,
            bin.Length * 60m / options.WindowMinutes, ended.Length == 0 ? null : sold.Length / (decimal)ended.Length,
            bin.Length == 0 ? null : bin[^1].EndedAt, options.WindowMinutes);
        SignalDecision Reject(string reason) => new(reason, liquidity);
        if (card.Key != snapshot.Key.Card) return Reject("identity_mismatch");
        if (card.Tradability != Tradability.Tradeable) return Reject("untradeable_or_unknown");
        if (!snapshot.HistoryAvailable) return Reject("history_unavailable_or_malformed");
        if (snapshot.QuoteUpdatedAt is not { } quoteTime || quoteTime > now || now - quoteTime > TimeSpan.FromMinutes(options.MaxQuoteAgeMinutes))
            return Reject("unknown_or_excessively_old_quote");
        var prices = snapshot.LowestPrices;
        if (prices.Count != 5 || prices[0] is not > 0 || prices[1] is not > 0) return Reject("missing_prices");
        long buy = prices[0]!.Value;
        if (buy < options.MinBuyPrice || buy > options.MaxBuyPrice) return Reject("outside_buy_range");
        var positive = prices.Where(p => p is > 0).Select(p => p!.Value).ToArray();
        if (!positive.SequenceEqual(positive.Order())) return Reject("unordered_prices");
        if (bin.Length < options.MinBuyNowSales || ended.Length < options.MinObservedAuctions ||
            liquidity.ObservedBuyNowPerHour < options.MinObservedBuyNowPerHour ||
            liquidity.ObservedSellThrough < options.MinObservedSellThrough)
            return Reject("insufficient_observed_liquidity");
        if (now - bin[^1].EndedAt > TimeSpan.FromMinutes(options.MaxLastSaleAgeMinutes)) return Reject("sales_too_old");
        // Both quote age and auction dates have minute-level rounding in the rendered UI.
        int afterQuote = bin.Count(a => a.EndedAt > quoteTime.AddMinutes(1));
        if (afterQuote < options.MinSalesAfterQuote) return Reject("insufficient_sales_after_quote");
        decimal median = Median(bin.Select(a => (decimal)a.SoldFor));
        decimal dispersion = Median(bin.Select(a => Math.Abs(a.SoldFor - median))) / median;
        if (dispersion > options.MaxDispersion) return Reject("volatile_sales");
        int half = bin.Length / 2;
        decimal older = Median(bin.Take(half).Select(a => (decimal)a.SoldFor));
        decimal recent = Median(bin.TakeLast(half).Select(a => (decimal)a.SoldFor));
        if (recent < older * (1 - options.MaxDownwardMove)) return Reject("falling_market");
        var alternatives = prices.Skip(1).Where(p => p is > 0).Select(p => (decimal)p!.Value).ToArray();
        if (alternatives.Length < 2) return Reject("insufficient_alternative_asks");
        decimal estimate = Math.Min(median, Median(alternatives));
        long sellPrice = (long)Math.Floor(estimate);
        long profit = (long)Math.Floor(estimate * (1 - options.TaxRate - options.RiskReserveRate) - buy);
        decimal roi = profit / (decimal)buy;
        if (prices[1]!.Value * (1 - options.TaxRate) <= buy || profit < options.MinNetProfit || roi < options.MinNetRoi)
            return Reject("insufficient_net_margin");
        return new("candidate", liquidity, new(card, snapshot.Key, now, buy, sellPrice, profit, roi,
            dispersion, afterQuote, liquidity, now - quoteTime));
    }

    public static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) throw new ArgumentException("Median requires observations.");
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
