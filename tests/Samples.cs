using System.Globalization;
using FbTracker.Configuration;
using FbTracker.Domain;

namespace FbTracker.Tests;

internal static class Samples
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 29, 11, 0, 0, TimeSpan.Zero);
    internal static PlayerCard Card(int id = 8) => new(new(27, id), "Kylian Mbappé", 91, "Normal", "ST",
        new Uri($"https://www.futbin.com/27/player/{id}/kylian-mbappe"), Tradability.Tradeable);
    internal static TrackerOptions Options() => new() { Platforms = [Platform.Console, Platform.Pc] };
    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
    internal static PageDocument Document(string html, string path = "/27/players?eUnt=1") =>
        new(new Uri("https://www.futbin.com" + path), new Uri("https://www.futbin.com" + path), html, Now);
    internal static MarketSnapshot Snapshot(int id = 8) => new(new(new(27, id), Platform.Console), Now,
        Now.AddMinutes(-20), [10000, 15000, 15100, 15200, 15300],
        [.. Enumerable.Range(0, 8).Select(i => new Auction(Now.AddMinutes(-2 - i * 2), 15000, 15000, SaleStatus.Sold, SaleType.BuyNow)),
            new(Now.AddMinutes(-4), 16000, 0, SaleStatus.Unsold, SaleType.Unknown)], true,
        new Uri($"https://www.futbin.com/27/player/{id}/kylian-mbappe/market"));
    internal static string Row(int id, string price = "0", string marker = "", string version = "TOTW") => $"""
        <tr><td class="table-rating">91</td><td><a class="table-player-name" href="/27/player/{id}/kylian-mbappe">Kylian Mbappé</a>
        <span class="table-player-revision">{version}</span><span class="table-pos">ST</span></td>
        <td class="table-price platform-ps-only"><div class="price {marker}">{price}</div></td></tr>
        """;
    internal const string Filter = "<div class='selected-filters-wrapper'>Exclude untradeables</div>";
    internal static string Catalog(params string[] rows) => Filter + "<table class='players-table'><tbody>" + string.Concat(rows) + "</tbody></table>";
    internal static string ProfitableMarket(DateTimeOffset now) => """
        <div class="price-box platform-ps-only price-box-original-player">
        <div class="lowest-price-1">10,000</div><div class="lowest-price">15,000</div>
        <div class="lowest-price">15,100</div><div class="lowest-price">15,200</div><div class="lowest-price">15,300</div>
        <div class="prices-updated">Price Updated: 20 mins ago</div></div>
        <div class="auctions-table-box platform-ps-only"><table class="auctions-table"><tbody>
        """ + string.Concat(Enumerable.Range(0, 8).Select(i => $"""
        <tr><td><i class="positive-color"></i><span>{now.AddMinutes(-2-i*2).ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture)}</span></td>
        <td>15000</td><td>15000</td><td>750</td><td>14250</td><td>Buy Now</td></tr>
        """)) + "</tbody></table></div>";
}
