using FbTracker.Domain;
using FbTracker.Futbin;

namespace FbTracker.Tests;

public class ParserTests
{
    private readonly FutbinParser _parser = new(Samples.Options());

    [Fact]
    public void Real_filtered_catalog_has_distinct_tradeable_cards()
    {
        var result = _parser.ParseCatalog(Samples.Document(Samples.Fixture("catalog-filtered.html")));
        Assert.Equal(30, result.Cards.Count);
        Assert.Equal(30, result.Cards.Select(c => c.Key).Distinct().Count());
        Assert.All(result.Cards, c => { Assert.Equal(27, c.Key.Season); Assert.Equal(Tradability.Tradeable, c.Tradability); });
    }

    [Fact]
    public void Real_SBC_completion_cost_is_not_a_market_price()
    {
        // Deliberately put an unfiltered response behind a claimed filter to exercise the second defence.
        var result = _parser.ParseCatalog(Samples.Document(Samples.Filter + Samples.Fixture("catalog-olise-unfiltered.html")));
        Assert.DoesNotContain(result.Cards, c => c.Key.FutbinId == 23101);
        Assert.Contains(result.Cards, c => c.Key.FutbinId == 22947);
        Assert.Contains(result.Cards, c => c.Key.FutbinId == 22948);
        Assert.Contains(result.Cards, c => c.Key.FutbinId == 62);
    }

    [Fact]
    public void Same_player_rating_and_zero_prices_do_not_merge_or_exclude_cards()
    {
        var result = _parser.ParseCatalog(Samples.Document(Samples.Catalog(
            Samples.Row(101, version: "TOTW"), Samples.Row(102, version: "HoloTOTW"), Samples.Row(103, price: "-", version: "Promo"))));
        Assert.Equal(new[] { 101, 102, 103 }, result.Cards.Select(c => c.Key.FutbinId));
        Assert.All(result.Cards, c => Assert.Equal(91, c.Rating));
    }

    [Theory]
    [InlineData("table-price-sbc")]
    [InlineData("objective")]
    [InlineData("untradeable")]
    [InlineData("evolution")]
    public void Untradeable_marker_excludes_even_nonzero_price(string marker)
    {
        Assert.Empty(_parser.ParseCatalog(Samples.Document(Samples.Catalog(Samples.Row(1, "500K", marker)))).Cards);
    }

    [Fact]
    public void Missing_filter_confirmation_is_rejected()
    {
        Assert.Throws<SourceFormatException>(() => _parser.ParseCatalog(Samples.Document(Samples.Catalog(Samples.Row(1)).Replace(Samples.Filter, ""))));
        Assert.Throws<SourceFormatException>(() => _parser.ParseCatalog(Samples.Document(Samples.Catalog(Samples.Row(1)), "/27/players")));
    }

    [Fact]
    public void Real_market_keeps_all_five_prices_platforms_and_sale_outcomes_separate()
    {
        var snapshots = _parser.ParseMarket(Samples.Card(), Samples.Document(Samples.Fixture("market-mbappe.html"), "/27/player/8/kylian-mbappe/market"));
        var console = Assert.Single(snapshots, s => s.Key.Platform == Platform.Console);
        var pc = Assert.Single(snapshots, s => s.Key.Platform == Platform.Pc);
        Assert.Equal(new long?[] { 3239000, 3240000, 3258000, 3260000, 3270000 }, console.LowestPrices);
        Assert.Equal(new long?[] { 3429000, 3470000, 3491000, 3499000, 3500000 }, pc.LowestPrices);
        Assert.True(console.HistoryAvailable);
        Assert.True(pc.HistoryAvailable);
        Assert.Equal(Samples.Now.AddSeconds(-3), console.QuoteUpdatedAt);
        Assert.Contains(console.Auctions, a => a.Status == SaleStatus.Sold && a.Type == SaleType.BuyNow && a.SoldFor == 3243000);
        Assert.Contains(console.Auctions, a => a.Status == SaleStatus.Unsold && a.SoldFor == 0);
        Assert.Contains(console.Auctions, a => a.Type == SaleType.Bid);
        Assert.All(console.Auctions, a => Assert.InRange(a.EndedAt, Samples.Now.AddDays(-1), Samples.Now));
    }

    [Fact]
    public void Redirect_or_canonical_for_another_card_rejects_response()
    {
        var page = Samples.Document(Samples.Fixture("market-mbappe.html"), "/27/player/8/kylian-mbappe/market");
        Assert.Throws<SourceFormatException>(() => _parser.ParseMarket(Samples.Card(99), page));
        Assert.Throws<SourceFormatException>(() => _parser.ParseMarket(Samples.Card(), page with { FinalUri = Samples.Card(99).Url }));
        Assert.Throws<SourceFormatException>(() => _parser.ParseMarket(Samples.Card(), page with { Html = "<link rel='canonical' href='/27/player/99/other'>" + Samples.ProfitableMarket(Samples.Now) }));
    }

    [Fact]
    public void A_bad_response_cannot_reuse_previous_history()
    {
        var page = Samples.Document(Samples.ProfitableMarket(Samples.Now), "/27/player/8/kylian-mbappe/market");
        Assert.True(Assert.Single(_parser.ParseMarket(Samples.Card(), page)).HistoryAvailable);
        var next = page with { Html = page.Html.Replace("auctions-table-box", "changed-markup") };
        var snapshot = Assert.Single(_parser.ParseMarket(Samples.Card(), next));
        Assert.False(snapshot.HistoryAvailable);
        Assert.Empty(snapshot.Auctions);
        Assert.Throws<SourceFormatException>(() => _parser.ParseMarket(Samples.Card(), page with { Html = "<h1>Checking your browser</h1>" }));
    }

    [Fact]
    public void Malformed_history_disables_signals_even_with_other_valid_rows()
    {
        var page = Samples.Document(Samples.ProfitableMarket(Samples.Now).Replace("<td>15000</td>", "<td>?</td>"), "/27/player/8/kylian-mbappe/market");
        Assert.False(Assert.Single(_parser.ParseMarket(Samples.Card(), page)).HistoryAvailable);
    }

    [Theory]
    [InlineData("positive-color", "changed-status")]
    [InlineData("Buy Now", "changed-sale-type")]
    public void Unknown_sale_semantics_disable_history(string before, string after)
    {
        var html = Samples.ProfitableMarket(Samples.Now).Replace(before, after);
        var snapshot = Assert.Single(_parser.ParseMarket(Samples.Card(), Samples.Document(html, "/27/player/8/kylian-mbappe/market")));
        Assert.False(snapshot.HistoryAvailable);
    }

    [Theory]
    [InlineData("15,000", 15000L)]
    [InlineData("1.25M", 1250000L)]
    [InlineData("872.9K", 872900L)]
    [InlineData("0", null)]
    [InlineData("-", null)]
    [InlineData("not a price", null)]
    public void Price_formats(string text, long? expected) => Assert.Equal(expected, FutbinParser.Coins(text));

    [Fact]
    public void Missing_price_slots_do_not_shift_other_prices()
    {
        var html = Samples.ProfitableMarket(Samples.Now).Replace("15,100", "0");
        var snapshot = Assert.Single(_parser.ParseMarket(Samples.Card(), Samples.Document(html, "/27/player/8/kylian-mbappe/market")));
        Assert.Equal(new long?[] { 10000, 15000, null, 15200, 15300 }, snapshot.LowestPrices);
    }

    [Fact]
    public void Time_zone_year_rollover_and_ambiguous_DST_are_handled()
    {
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), _parser.ParseSaleTime("Jul 1, 11:00 AM", new(2026, 7, 1, 10, 5, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 0, TimeSpan.Zero), _parser.ParseSaleTime("Dec 31, 11:59 PM", new(2027, 1, 1, 0, 5, 0, TimeSpan.Zero)));
        Assert.Null(_parser.ParseSaleTime("Oct 25, 1:30 AM", Samples.Now));
        Assert.Null(FutbinParser.ParseQuoteTime("Never", Samples.Now));
    }
}
