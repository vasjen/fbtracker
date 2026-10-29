using FbTracker.Analysis;
using FbTracker.Configuration;
using FbTracker.Domain;

namespace FbTracker.Tests;

public class SignalTests
{
    private readonly SignalAnalyzer _analyzer = new(new SignalOptions());
    private SignalDecision Decide(MarketSnapshot snapshot) => _analyzer.Evaluate(Samples.Card(), snapshot);

    [Fact]
    public void Profitable_stale_quote_requires_recent_executed_sales_and_net_margin()
    {
        var decision = Decide(Samples.Snapshot());
        Assert.Equal("candidate", decision.Reason);
        var signal = Assert.IsType<TradingSignal>(decision.Signal);
        Assert.Equal(15000, signal.EstimatedSellPrice);
        Assert.Equal(3950, signal.ExpectedNetProfit); // 5% tax + 2% risk reserve
        Assert.Equal(0.395m, signal.NetRoi);
        Assert.Equal(8, signal.SalesAfterQuote);
        Assert.Equal(8m, signal.Liquidity.ObservedBuyNowPerHour);
        Assert.Equal(8m / 9, signal.Liquidity.ObservedSellThrough);
    }

    [Fact]
    public void Identity_and_tradability_are_checked_again_before_a_signal()
    {
        Assert.Equal("identity_mismatch", Decide(Samples.Snapshot(99)).Reason);
        Assert.Null(_analyzer.Evaluate(Samples.Card() with { Tradability = Tradability.Untradeable }, Samples.Snapshot()).Signal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Unknown_old_and_future_quote_are_rejected(int kind)
    {
        DateTimeOffset? quote = kind == 0 ? null : Samples.Now.AddMinutes(kind == 1 ? -90 : 1);
        Assert.Equal("unknown_or_excessively_old_quote", Decide(Samples.Snapshot() with { QuoteUpdatedAt = quote }).Reason);
    }

    [Fact]
    public void Bids_unsold_old_and_future_rows_cannot_inflate_BIN_liquidity()
    {
        var snapshot = Samples.Snapshot();
        var rows = new[] {
            new Auction(Samples.Now.AddMinutes(-2), 15000, 15000, SaleStatus.Sold, SaleType.Bid),
            new Auction(Samples.Now.AddMinutes(-2), 15000, 0, SaleStatus.Unsold, SaleType.Unknown),
            new Auction(Samples.Now.AddHours(-2), 15000, 15000, SaleStatus.Sold, SaleType.BuyNow),
            new Auction(Samples.Now.AddMinutes(1), 15000, 15000, SaleStatus.Sold, SaleType.BuyNow) };
        var decision = Decide(snapshot with { Auctions = rows });
        Assert.Null(decision.Signal);
        Assert.Equal(2, decision.Liquidity.ObservedEndedAuctions);
        Assert.Equal(0, decision.Liquidity.BuyNowSales);
        Assert.Equal(0.5m, decision.Liquidity.ObservedSellThrough);
    }

    [Fact]
    public void Minute_rounding_requires_sales_strictly_after_quote_uncertainty()
    {
        var snapshot = Samples.Snapshot() with { QuoteUpdatedAt = Samples.Now.AddMinutes(-3) };
        Assert.Equal("insufficient_sales_after_quote", Decide(snapshot).Reason);
    }

    [Fact]
    public void Missing_history_and_empty_history_do_not_throw_or_signal()
    {
        Assert.Equal("history_unavailable_or_malformed", Decide(Samples.Snapshot() with { HistoryAvailable = false }).Reason);
        Assert.Equal("insufficient_observed_liquidity", Decide(Samples.Snapshot() with { Auctions = [] }).Reason);
    }

    [Fact]
    public void Bad_quote_order_zero_price_and_tax_losses_are_rejected()
    {
        Assert.Equal("missing_prices", Decide(Samples.Snapshot() with { LowestPrices = [null, 15000, 15100, 15200, 15300] }).Reason);
        Assert.Equal("unordered_prices", Decide(Samples.Snapshot() with { LowestPrices = [10000, 15000, 14000, 15200, 15300] }).Reason);
        Assert.Equal("insufficient_net_margin", Decide(Samples.Snapshot() with { LowestPrices = [14500, 15000, 15100, 15200, 15300] }).Reason);
    }

    [Fact]
    public void Large_outliers_do_not_raise_median_target()
    {
        var snapshot = Samples.Snapshot();
        var rows = snapshot.Auctions.ToArray();
        rows[3] = rows[3] with { SoldFor = 1000000 };
        Assert.Equal(15000, Decide(snapshot with { Auctions = rows }).Signal!.EstimatedSellPrice);
    }

    [Fact]
    public void Falling_market_and_wide_dispersion_reject_the_gap()
    {
        var snapshot = Samples.Snapshot();
        var falling = snapshot.Auctions.Select(a => a.Status != SaleStatus.Sold ? a : a with { SoldFor = a.EndedAt > Samples.Now.AddMinutes(-9) ? 14000 : 15500 }).ToArray();
        Assert.Equal("falling_market", Decide(snapshot with { Auctions = falling }).Reason);
        var volatileRows = snapshot.Auctions.Select((a, i) => a.Status != SaleStatus.Sold ? a : a with { SoldFor = i % 2 == 0 ? 10000 : 30000 }).ToArray();
        Assert.Equal("volatile_sales", Decide(snapshot with { Auctions = volatileRows }).Reason);
    }

    [Fact]
    public void Unordered_sales_are_sorted_by_time_and_repeated_rows_are_not_silently_deduplicated()
    {
        var snapshot = Samples.Snapshot();
        Assert.Equal(Decide(snapshot).Signal, Decide(snapshot with { Auctions = snapshot.Auctions.Reverse().ToArray() }).Signal);
        var duplicate = snapshot.Auctions[0];
        Assert.Equal(9, Decide(snapshot with { Auctions = [.. snapshot.Auctions, duplicate] }).Liquidity.BuyNowSales);
    }
}
