namespace FbTracker.Domain;

// Names, ratings, promo labels and EA base-player IDs are never identity keys.
public readonly record struct CardKey(int Season, int FutbinId)
{
    public override string ToString() => $"fc{Season}:card:{FutbinId}";
}

public enum Platform { Console, Pc }
public enum Tradability { Unknown, Tradeable, Untradeable }
public enum SaleType { Unknown, BuyNow, Bid }
public enum SaleStatus { Unknown, Sold, Unsold }

public readonly record struct MarketKey(CardKey Card, Platform Platform)
{
    public override string ToString() => $"{Card}:{(Platform == Platform.Console ? "ps" : "pc")}";
}

public sealed record PlayerCard(CardKey Key, string Name, int Rating, string Version,
    string Position, Uri Url, Tradability Tradability);

public sealed record Auction(DateTimeOffset EndedAt, long ListedFor, long SoldFor,
    SaleStatus Status, SaleType Type);

public sealed record MarketSnapshot(MarketKey Key, DateTimeOffset ObservedAt,
    DateTimeOffset? QuoteUpdatedAt, IReadOnlyList<long?> LowestPrices,
    IReadOnlyList<Auction> Auctions, bool HistoryAvailable, Uri SourceUrl);

// These are observations in FUTBIN's sample, not the volume of the entire EA market.
public sealed record Liquidity(int ObservedEndedAuctions, int ObservedSales, int BuyNowSales,
    decimal ObservedBuyNowPerHour, decimal? ObservedSellThrough,
    DateTimeOffset? LatestBuyNowAt, int WindowMinutes);

public sealed record TradingSignal(PlayerCard Card, MarketKey Key, DateTimeOffset ObservedAt,
    long BuyPrice, long EstimatedSellPrice, long ExpectedNetProfit, decimal NetRoi,
    decimal PriceDispersion, int SalesAfterQuote, Liquidity Liquidity, TimeSpan QuoteAge);

public sealed record SignalDecision(string Reason, Liquidity Liquidity, TradingSignal? Signal = null);
public sealed record CatalogPage(IReadOnlyList<PlayerCard> Cards, int LastPage);
public sealed record PageDocument(Uri RequestedUri, Uri FinalUri, string Html, DateTimeOffset FetchedAt);
