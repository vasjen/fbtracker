using System.Globalization;
using System.Text.RegularExpressions;
using FbTracker.Configuration;
using FbTracker.Domain;
using HtmlAgilityPack;

namespace FbTracker.Futbin;

public sealed class SourceFormatException(string message) : Exception(message);

public sealed class FutbinParser(TrackerOptions options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.SourceTimeZone);
    private static string C(string name) => $"contains(concat(' ',normalize-space(@class),' '),' {name} ')";
    private static string Text(HtmlNode? node) => HtmlEntity.DeEntitize(node?.InnerText ?? "").Trim();
    private static IEnumerable<HtmlNode> Nodes(HtmlNode node, string xpath) =>
        node.SelectNodes(xpath)?.AsEnumerable() ?? [];
    private static HtmlDocument Document(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    public static bool IsFutbinUri(Uri uri) => uri.Scheme == "https" &&
        uri.Host is "www.futbin.com" or "futbin.com" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);

    public static CardKey? CardFromUri(Uri uri)
    {
        if (!IsFutbinUri(uri)) return null;
        var match = Regex.Match(uri.AbsolutePath, @"^/(\d+)/player/(\d+)(?:/|$)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var season) &&
            int.TryParse(match.Groups[2].Value, out var id) && id > 0 ? new(season, id) : null;
    }

    public CatalogPage ParseCatalog(PageDocument page)
    {
        if (!IsFutbinUri(page.FinalUri) || page.FinalUri.AbsolutePath != $"/{options.Season}/players" ||
            !Regex.IsMatch(page.FinalUri.Query, @"(?:[?&])eUnt=1(?:&|$)"))
            throw new SourceFormatException("Catalog was redirected or lost the untradeables filter.");
        var root = Document(page.Html).DocumentNode;
        var selected = root.SelectSingleNode($"//*[{C("selected-filters-wrapper") }]");
        if (selected is null || !Text(selected).Contains("Exclude untradeables", StringComparison.OrdinalIgnoreCase))
            throw new SourceFormatException("FUTBIN did not confirm Exclude untradeables. Refusing to schedule cards.");
        var table = root.SelectSingleNode($"//table[{C("players-table")}]") ??
            throw new SourceFormatException("FC 27 catalog table is missing (blocked response or changed markup).");
        var cards = new Dictionary<CardKey, PlayerCard>();
        foreach (var row in Nodes(table, ".//tbody/tr"))
        {
            var link = row.SelectSingleNode($".//a[{C("table-player-name")}]");
            if (link is null) continue; // Ads and table decorations are not cards.
            if (!Uri.TryCreate(page.FinalUri, HtmlEntity.DeEntitize(link.GetAttributeValue("href", "")), out var url) ||
                CardFromUri(url) is not { } key || key.Season != options.Season)
                throw new SourceFormatException("Invalid or cross-season card link in catalog.");
            if (!int.TryParse(Text(row.SelectSingleNode($".//*[{C("table-rating")}]")), out var rating))
                throw new SourceFormatException($"Missing rating for {key}.");
            var name = Text(link);
            var version = Text(row.SelectSingleNode($".//*[{C("table-player-revision")}]"));
            if (name.Length == 0 || version.Length == 0)
                throw new SourceFormatException($"Missing name or version for {key}.");
            var priceCells = Nodes(row, $".//*[{C("table-price")}]").ToArray();
            // SBC prices are costs to complete a challenge, never a purchase price.
            bool untradeable = priceCells.Any(HasUntradeableMarker) || HasExplicitUntradeableFlag(row);
            var card = new PlayerCard(key, name, rating, version,
                Text(row.SelectSingleNode($".//*[{C("table-pos")}]")), url,
                untradeable ? Tradability.Untradeable : priceCells.Length > 0 ? Tradability.Tradeable : Tradability.Unknown);
            // The verified eUnt filter is the evidence of tradability; price may legitimately be zero/missing.
            if (card.Tradability == Tradability.Tradeable && rating >= options.MinRating && rating <= options.MaxRating)
                cards[key] = card;
        }
        int lastPage = 1;
        foreach (var link in Nodes(root, "//a[@href]"))
        {
            if (!Uri.TryCreate(page.FinalUri, HtmlEntity.DeEntitize(link.GetAttributeValue("href", "")), out var uri) ||
                !IsFutbinUri(uri) || uri.AbsolutePath != $"/{options.Season}/players") continue;
            var match = Regex.Match(uri.Query, @"(?:[?&])page=(\d+)(?:&|$)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n)) lastPage = Math.Max(lastPage, n);
        }
        return new(cards.Values.ToArray(), lastPage);
    }

    private static bool HasExplicitUntradeableFlag(HtmlNode node) =>
        node.GetAttributeValue("data-tradeable", "").Equals("false", StringComparison.OrdinalIgnoreCase) ||
        node.GetAttributeValue("data-tradable", "").Equals("false", StringComparison.OrdinalIgnoreCase);

    private static bool HasUntradeableMarker(HtmlNode node) => Nodes(node, ".//*").Prepend(node).Any(n =>
    {
        string evidence = $"{n.GetAttributeValue("class", "")} {n.GetAttributeValue("alt", "")} {n.GetAttributeValue("title", "")}";
        return Regex.IsMatch(evidence, @"sbc|objective|untradeable|untradable|reward|evolution", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(n.GetAttributeValue("href", ""), @"/(squad-building-challenges|objectives|evolutions)(/|$|\?)");
    }) || Regex.IsMatch(Text(node), @"\b(SBC|Objectives?|Untradeable|Untradable|Reward)\b", RegexOptions.IgnoreCase);

    public IReadOnlyList<MarketSnapshot> ParseMarket(PlayerCard card, PageDocument page)
    {
        if (card.Tradability != Tradability.Tradeable) return [];
        if (CardFromUri(page.FinalUri) != card.Key || CardFromUri(page.RequestedUri) != card.Key)
            throw new SourceFormatException("Market response belongs to a different card or season.");
        var root = Document(page.Html).DocumentNode;
        var canonical = root.SelectSingleNode("//link[@rel='canonical']")?.GetAttributeValue("href", "");
        if (canonical is not null && (!Uri.TryCreate(page.FinalUri, canonical, out var canonicalUri) || CardFromUri(canonicalUri) != card.Key))
            throw new SourceFormatException("Canonical identity disagrees with requested card.");
        var results = new List<MarketSnapshot>();
        foreach (var platform in options.Platforms.Distinct())
        {
            var platformClass = platform == Platform.Console ? "platform-ps-only" : "platform-pc-only";
            var priceBox = root.SelectSingleNode($"//*[{C("price-box")} and {C(platformClass)} and {C("price-box-original-player")}]");
            if (priceBox is null) continue;
            if (HasUntradeableMarker(priceBox)) continue;
            var first = priceBox.SelectSingleNode($".//*[{C("lowest-price-1")}]");
            if (first is null) throw new SourceFormatException("Price box has no lowest-price slot.");
            var priceNodes = Nodes(priceBox, $".//*[{C("lowest-price")}]").ToArray();
            if (priceNodes.Length != 4) throw new SourceFormatException("Expected four secondary price slots.");
            long?[] prices = [Coins(Text(first)), .. priceNodes.Select(n => Coins(Text(n)))];
            var updated = ParseQuoteTime(Text(priceBox.SelectSingleNode($".//*[{C("prices-updated")}]")), page.FetchedAt);
            var historyBox = root.SelectSingleNode($"//*[{C("auctions-table-box")} and {C(platformClass)}]");
            var table = historyBox?.SelectSingleNode($".//table[{C("auctions-table")}]");
            var auctions = new List<Auction>();
            bool validHistory = table is not null;
            if (table is not null)
            {
                foreach (var row in Nodes(table, ".//tbody/tr"))
                {
                    var cells = Nodes(row, "./td").ToArray();
                    if (cells.Length != 6) { validHistory = false; continue; }
                    var date = ParseSaleTime(Text(cells[0]), page.FetchedAt);
                    if (date is null || !TryCoins(Text(cells[1]), out var listed) ||
                        !TryCoins(Text(cells[2]), out var sold)) { validHistory = false; continue; }
                    var status = cells[0].SelectSingleNode($".//*[{C("positive-color")}]") is not null ? SaleStatus.Sold :
                        cells[0].SelectSingleNode($".//*[{C("negative-color")}]") is not null ? SaleStatus.Unsold : SaleStatus.Unknown;
                    var typeText = Text(cells[5]);
                    var type = typeText.Equals("Buy Now", StringComparison.OrdinalIgnoreCase) ? SaleType.BuyNow :
                        typeText.Equals("Bid", StringComparison.OrdinalIgnoreCase) ? SaleType.Bid : SaleType.Unknown;
                    if ((status == SaleStatus.Sold && sold <= 0) || (status == SaleStatus.Unsold && sold > 0))
                        validHistory = false;
                    if (status == SaleStatus.Unknown || (status == SaleStatus.Sold && type == SaleType.Unknown))
                        validHistory = false;
                    auctions.Add(new(date.Value, listed, sold, status, type));
                }
            }
            results.Add(new(new(card.Key, platform), page.FetchedAt, updated, prices,
                auctions, validHistory, page.FinalUri));
        }
        if (results.Count == 0) throw new SourceFormatException("No market price boxes for requested platforms; no signal can be produced.");
        return results;
    }

    public static long? Coins(string text) => TryCoins(text, out var value) && value > 0 ? value : null;

    public static bool TryCoins(string text, out long value)
    {
        value = 0;
        var cleaned = HtmlEntity.DeEntitize(text).Replace(",", "").Replace("\u00a0", "").Trim();
        var match = Regex.Match(cleaned, @"^(\d+(?:\.\d+)?)([KM]?)$", RegexOptions.IgnoreCase);
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number)) return false;
        number *= match.Groups[2].Value.ToUpperInvariant() switch { "K" => 1000, "M" => 1000000, _ => 1 };
        if (number > long.MaxValue || decimal.Truncate(number) != number) return false;
        value = (long)number;
        return true;
    }

    public static DateTimeOffset? ParseQuoteTime(string text, DateTimeOffset now)
    {
        var match = Regex.Match(text, @"(?:Price Updated:\s*)?(\d+)\s*(sec(?:ond)?s?|mins?|minutes?|hours?|days?)\s+ago", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var n) || n > 366 * 86400) return null;
        string unit = match.Groups[2].Value.ToLowerInvariant();
        double seconds = n * (unit.StartsWith("sec") ? 1d : unit.StartsWith("min") ? 60d : unit.StartsWith("hour") ? 3600d : 86400d);
        if (seconds > TimeSpan.FromDays(366).TotalSeconds) return null;
        return now.AddSeconds(-seconds);
    }

    public DateTimeOffset? ParseSaleTime(string text, DateTimeOffset now)
    {
        // Rendered FUTBIN dates omit the year. Never guess an ambiguous DST hour.
        var localNow = TimeZoneInfo.ConvertTime(now, _zone);
        foreach (int year in new[] { localNow.Year, localNow.Year - 1 })
        {
            if (!DateTime.TryParseExact($"{text.Trim()} {year}", ["MMM d, h:mm tt yyyy", "MMM d, HH:mm yyyy"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var local)) continue;
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            if (_zone.IsInvalidTime(local) || _zone.IsAmbiguousTime(local)) return null;
            var time = new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
            if (time <= now && time >= now.AddDays(-366)) return time;
        }
        return null;
    }
}
