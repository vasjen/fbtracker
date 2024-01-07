using System.Diagnostics;
using fbtracker.Models;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services
{
    public class ProfitService : IProfitService
    {
        public int ProxyCount = 10;
        private readonly ISalesHistoryService _historyService;
        private readonly IPriceService _priceService;
        private readonly ITelegramService _tgbot;
        private readonly IServiceProvider _services;
        private readonly INotificationService _discord;
        private readonly ILogger<ProfitService> _logger;
        private  const double AFTER_TAX = 0.95;
        private  const double MIN_PROFIT = 1000;

        public ProfitService(
            ISalesHistoryService history, 
            IPriceService priceService,
            ITelegramService tgbot,
            INotificationService discord,
            IServiceProvider services,
            ILogger<ProfitService> logger)
        {
            _historyService = history;
            _priceService = priceService;
            _tgbot = tgbot;
            _services = services;
            _discord = discord;
            _logger = logger;
        }
        public async Task FindingProfitAsync (IAsyncEnumerable<Card> cards)
        {
            Stopwatch timer = new();
             using (IServiceScope scope = _services.CreateScope())
             {
                 IWebService webService = 
                     scope.ServiceProvider
                         .GetRequiredService<IWebService>();
                List<HttpClient> clients = await  webService.CreateHttpClients(webService.CreateHandlers(webService.GetProxyList()));
                int currentIndex = 0;
                _logger.LogInformation("Count of clients: {0} in ProfitService", clients.Count);
                HttpClient getNextClient()
                {
                    HttpClient client = clients[currentIndex];
                    currentIndex = (currentIndex + 1) % clients.Count;
                    return client;
                }
                timer.Start();
                Parallel.ForEach(await cards.ToListAsync(),
                    new ParallelOptions { MaxDegreeOfParallelism = clients.Count }, async p =>
                    {
                        if (p.FbDataId == 0)
                            p.FbDataId = Scraping.GetDataId(p, getNextClient());
                        try
                        {
                            HttpClient client = getNextClient();
                            CheckProfitAsync(p, client).Wait();
                        }
                        catch (Exception ex) { 
                            _logger.LogError(ex.Message);
                            _logger.LogError($"Error with {p.ToString()}");
                        }
                    });
             }
             _logger.LogInformation("Total time checking price for all cards: {0}", timer.Elapsed);
         }
      
        private async Task CheckProfitAsync(Card card, HttpClient client)
        {
            await Task.Delay(1000);
            await _priceService.GetPriceAsync(card, client);
            if (card.Prices.Ps.LCPrice2 != 0 && card.Prices.Ps.LCPrice != 0)
            {   
                IEnumerable<SalesHistory>? history = await _historyService.GetSalesHistoryAsync(card.FbDataId, client);
                IEnumerable<SalesHistory>? lastSales = history?
                    .Where(p => p.status.Contains("closed"))
                    .Where(p => Math.Abs(card.Prices.Ps.Average - p.Price) <= (card.Prices.Ps.Average * 0.15))
                    .Take(10);
                             
                double avgPrice=(lastSales!.Average(p => p.Price));
                if (history is null)
                {
                    _logger.LogInformation($"History is null or incorrect for {card.ShortName}");
                    return;
                }
                _logger.LogInformation($"Check  Card: {card.ShortName}, CurrentPrice: {card.Prices.Ps.LCPrice}, Average: {card.Prices.Ps.Average }");
                _logger.LogInformation($"Average by history: {avgPrice}. Difference profit: {avgPrice * AFTER_TAX - card.Prices.Ps.LCPrice}  ");
           
                int profit = (int)(avgPrice * AFTER_TAX - card.Prices.Ps.LCPrice);
                if (profit > 0 && profit >= MIN_PROFIT && card.Prices.Ps.LCPrice2 * AFTER_TAX > card.Prices.Ps.LCPrice)
                {
                    _logger.LogInformation("\t => !!PROFIT!!!");
                    _logger.LogInformation($"{card}");
                    card.PromoUrl = await Scraping.GetBackgroundImage(Scraping.URL + "/player/" + card.FbId);
                    string link = card.PromoUrl.Substring(card.PromoUrl.LastIndexOf('/') + 1);
                    card.PromoUrlFile = link.Remove(link.IndexOf('?'));
                    _logger.LogInformation("Promo urlNameFile: {0} added to card", card.PromoUrlFile);
                    Profit newProfit = new() 
                    { 
                        CardId = card!.CardId, 
                        Price = card.Prices.Ps.LCPrice, 
                        SellPrice = card.Prices.Ps.LCPrice2 , 
                        ProfitValue = profit, 
                        Percentage = (decimal)card.Prices.Ps.LCPrice / (decimal)avgPrice   
                    };
                    _logger.LogInformation($"Profit: {profit} for {card}");
                    await _tgbot.SendInfo(newProfit,Convert.ToInt32(avgPrice),lastSales, card);
                    await _discord.SendMessage(newProfit,Convert.ToInt32(avgPrice),lastSales, card);
                    
                }
            }
        }
     }
}