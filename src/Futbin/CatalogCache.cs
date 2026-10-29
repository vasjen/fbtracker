using FbTracker.Configuration;
using FbTracker.Domain;

namespace FbTracker.Futbin;

public sealed class CatalogCache(TrackerOptions options, FutbinParser parser, ILogger<CatalogCache> logger)
{
    private IReadOnlyList<PlayerCard>? _cards;
    private DateTimeOffset _expires;

    public async Task<IReadOnlyList<PlayerCard>> GetAsync(IPageSession session, CancellationToken ct)
    {
        if (_cards is not null && DateTimeOffset.UtcNow < _expires) return _cards;
        var cards = new Dictionary<CardKey, PlayerCard>();
        var seenPages = new HashSet<string>();
        int last = 1;
        for (int page = 1; page <= Math.Min(last, options.MaxCatalogPages); page++)
        {
            var uri = new Uri($"https://www.futbin.com/{options.Season}/players?eUnt=1&player_rating={options.MinRating}-{options.MaxRating}&page={page}");
            var result = parser.ParseCatalog(await session.GetAsync(uri, ct));
            var fingerprint = string.Join(',', result.Cards.Select(c => c.Key.FutbinId).Order());
            if (result.Cards.Count > 0 && !seenPages.Add(fingerprint))
                throw new SourceFormatException("FUTBIN repeated a catalog page; pagination may have changed.");
            last = result.LastPage;
            foreach (var card in result.Cards) cards[card.Key] = card;
            logger.LogInformation("Catalog page {Page}/{Last}; {Cards} distinct tradeable cards", page, last, cards.Count);
        }
        if (cards.Count == 0) throw new SourceFormatException("Catalog contained no eligible tradeable cards.");
        if (last > options.MaxCatalogPages) logger.LogWarning("Catalog deliberately limited to {Pages} pages", options.MaxCatalogPages);
        // Commit atomically after all requested pages succeed. Failed refreshes never publish a partial catalog.
        _cards = cards.Values.OrderBy(c => c.Key.FutbinId).ToArray();
        _expires = DateTimeOffset.UtcNow.AddMinutes(options.CatalogRefreshMinutes);
        return _cards;
    }
}
