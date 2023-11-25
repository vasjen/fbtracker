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
        private  const double AFTER_TAX = 0.95;
        private  const double MIN_PROFIT = 500;

        public ProfitService(
            ISalesHistoryService history, 
            IPriceService priceService,
            ITelegramService tgbot, 
            IServiceProvider services)
        {
            _historyService = history;
            _priceService = priceService;
            _tgbot = tgbot;
            _services = services;
        }
        public async Task FindingProfitAsync (IAsyncEnumerable<Card> cards)
        {
             using (IServiceScope scope = _services.CreateScope())
             {
                 IWebService webService = 
                     scope.ServiceProvider
                         .GetRequiredService<IWebService>();
                List<HttpClient> clients = await  webService.CreateHttpClients(webService.CreateHandlers(webService.GetProxyList()));
                int currentIndex = 0;
                Console.WriteLine("Count of clients: {0} in ProfitService", clients.Count);
                HttpClient getNextClient()
                {
                    HttpClient client = clients[currentIndex];
                    currentIndex = (currentIndex + 1) % clients.Count;
                    return client;
                }
                Parallel.ForEach(await cards.ToListAsync(),
                    new ParallelOptions { MaxDegreeOfParallelism = clients.Count }, async p =>
                    {
                        if (p.FbDataId == 0)
                            p.FbDataId = GetDataId(p, getNextClient());
                        try
                        {
                            var client = getNextClient();
                            CheckProfitAsync(p, client).Wait();
                        }
                        catch (Exception ex) { 
                            Console.WriteLine(ex.Message);
                            Console.WriteLine($"Error with {p.FbDataId}");
                        }
                    });
             }
         }
        private int GetDataId(Card card, HttpClient client)
        {
            Task<string[]>? result =  Scraping.GetPageAsStrings(client,$"http://www.futbin.com/player/{card.FbId}");
            int fbdataid = 0;
            for (int i = 0; i < result.Result.Length; i++)
            {
                if (result.Result[i].Contains("data-player-resource")){
                    string id = result.Result[i].Remove(result.Result[i].LastIndexOf('"')).Substring(result.Result[i].LastIndexOf("=\"")+2);
                    card.FbDataId=int.Parse(id);
                    fbdataid =  int.Parse(id);
                }
            }
            return fbdataid;
        }
        private async Task CheckProfitAsync(Card card, HttpClient client)
        {
            await Task.Delay(1000);
            int[] prices = await _priceService.GetPriceAsync(card.FbDataId, client);
            int currentPrice = prices[0];
            int nextPrice = prices[1];
            Console.WriteLine($"Check  id: {card.FbDataId}, CurrentPrice: {currentPrice}, NextPrice: {nextPrice } \n");
            if (nextPrice != 0 && currentPrice != 0)
            {   
            int profit = (int)(nextPrice * AFTER_TAX - currentPrice);
                if (profit > 0 && profit >= MIN_PROFIT){  
                    
                    IEnumerable<SalesHistory>? history = await _historyService.GetSalesHistoryAsync(card.FbDataId, client);
                    if (history is null)
                    {
                        Console.WriteLine($"History is null or incorrect for {card.ShortName}");
                        return;
                    }

                    IEnumerable<SalesHistory>? lastSales = history?
                             .Where(p => p.status.Contains("closed"))
                             .Take(20);
                             
                    double avgPrice=(lastSales!.Average(p => p.Price));
                       // if (NextPrice <= avgPrice)  
                       // {
                            Console.WriteLine("\t => !!PROFIT!!!");
                            Console.WriteLine($"{card.ShortName } {card.Version} {card.Rating} {card.Position} Profit: {profit} for {card.ShortName} {card.Version}");
                            Profit newProfit = new() {
                                CardId=card!.CardId,
                                Price=currentPrice,
                                SellPrice=nextPrice ,
                                ProfitValue=profit,
                                Percentage=(decimal)currentPrice/nextPrice   
                        };
                        Console.WriteLine($"Profit: {profit} for {card.ShortName} {card.Version}");
                        await _tgbot.SendInfo(newProfit,Convert.ToInt32(avgPrice),lastSales, card);
                     // }
                }
                else
               Console.WriteLine($"ID: {card.FbDataId}. No profit"); 
            }
        }
     }
}