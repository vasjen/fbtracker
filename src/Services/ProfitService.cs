using fbtracker.Models;
using fbtracker.Services.Interfaces;
using Newtonsoft.Json;

namespace fbtracker.Services;

public class ProfitService : IProfitService
{
    private readonly IGetingCardData _getingCardData;
    private readonly IEnumerable<INotificationService> _notificationServices;
    private readonly ILogger<ProfitService> _logger;
    private readonly IWebService _webService;
    private readonly IRedisService _redisService;

    private  const double AFTER_TAX = 0.95;
    private  const double MIN_PROFIT = 1000;

    public ProfitService(
        IGetingCardData getingCardData, 
        IEnumerable<INotificationService> notificationServices, 
        ILogger<ProfitService> logger, 
        IWebService webService,
        IRedisService redisService)
    {
        _getingCardData = getingCardData;
        _notificationServices = notificationServices;
        _logger = logger;
        _webService = webService;
        _redisService = redisService;
    }
   


    public async Task FindProfitCards(IAsyncEnumerable<Card> cards)
    {
        Parallel.ForEach(await cards.Where(p => !p.Version.Contains("IF")).ToListAsync(),
            new ParallelOptions { MaxDegreeOfParallelism = _webService.Clients.Count }, ProfitSearchingParallel);
    }

    private async void ProfitSearchingParallel(Card p)
    {
        if (p.FbDataId == 0) p.FbDataId = Scraping.GetDataId(p, _webService.Client);
        try
        {
            HttpClient client = _webService.Client;
            ProfitCard? profitCard = await CheckProfitAsync(p, client);
            if (profitCard != null)
            {
                await PreparingCardToSending(p);
                if (_redisService.IsExist(p.FbId.ToString()))
                {
                    _logger.LogInformation($"Card {p} was already sent");
                    return;
                }
                await SendNotificationAsync(profitCard);
                string serialized = JsonConvert.SerializeObject(p);
                _redisService.AddValueToDb(p.FbId.ToString(), serialized);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error with {p} \n {ex.Message}");
        }
    }

    private async Task PreparingCardToSending(Card p)
    {
        _getingCardData.AddImagesUrlToCard(p);
        await _getingCardData.DownloadImageAsync("Cards",$"{p.FbDataId}.png",new Uri(p.ImageUrl));
        await _getingCardData.DownloadImageAsync("Promo",$"{p.PromoUrlFile}",new Uri(p.PromoUrl));
        await _getingCardData.CombineImages("Cards/" + $"{p.FbDataId}.png","Promo/" + $"{p.PromoUrlFile}",$"{p.FbDataId}.png");
    }

    private async Task<ProfitCard?> CheckProfitAsync(Card card, HttpClient client)
    {
        await Task.Delay(1500);
        await _getingCardData.GetPriceAsync(card, client);
        if (card.Prices.Ps.LCPrice2 != 0 && card.Prices.Ps.LCPrice != 0)
        {
            IEnumerable<SalesHistory>? history = await _getingCardData.GetSalesHistoryAsync(card.FbDataId, client);
            IEnumerable<SalesHistory>? lastSales = history?
                .Where(p => p.status.Contains("closed"))
                .Where(p => Math.Abs(card.Prices.Ps.Average - p.Price) <= (card.Prices.Ps.Average * 0.15))
                .Take(10);
            double avgPrice = (lastSales!.Average(p => p.Price));
            if (history is null)
            {
                _logger.LogInformation($"History is null or incorrect for {card.ShortName}");
            }

            int profit = (int)(avgPrice * AFTER_TAX - card.Prices.Ps.LCPrice);
            if (profit > 0 && profit >= MIN_PROFIT && card.Prices.Ps.LCPrice2 * AFTER_TAX > card.Prices.Ps.LCPrice)
            {
                _logger.LogInformation($"Found profit card: {card}, profit: {profit}");
                return new ProfitCard(card, profit, Convert.ToInt32(avgPrice), lastSales);
            }
        }

        return default;
    }
    
    private async Task SendNotificationAsync(ProfitCard card)
    {
        foreach (INotificationService service in _notificationServices)
        {
            await service.SendMessageAsync(card);
        }
    }
}