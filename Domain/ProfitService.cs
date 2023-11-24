using fbtracker.Services;
using fbtracker.Services.Interfaces;

namespace fbtracker.Domain
{
    public class ProfitService(
            ISalesHistoryService _history, 
            IPriceService priceService,
            ITelegramService tggbot, 
            IServiceProvider _services)
        : IProfitService
    {
        public int ProxyCount = 10;
        private  const double AFTER_TAX = 0.95;
        private  const double MIN_PROFIT = 500;


        public async Task FindingProfitAsync (IAsyncEnumerable<Card> Cards)
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
                Parallel.ForEach(await Cards.ToListAsync(),
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
        private int GetDataId(Card Card, HttpClient client)
        {
            Task<string[]>? result =  Scraping.GetPageAsStrings(client,$"http://www.futbin.com/player/{Card.FbId}");
            int fbdataid = 0;
            for (int i=0; i<result.Result.Length;i++)
            {
                if (result.Result[i].Contains("data-player-resource")){
                    string id = result.Result[i].Remove(result.Result[i].LastIndexOf('"')).Substring(result.Result[i].LastIndexOf("=\"")+2);
                    Card.FbDataId=int.Parse(id);
                    fbdataid =  int.Parse(id);
                }
                
            }

            return fbdataid;
        }
        private async Task CheckProfitAsync(Card card, HttpClient client)
        {
            await Task.Delay(1000);
            int[] Prices= await priceService.GetPriceAsync(card.FbDataId, client);
            int CurrentPrice=Prices[0];
            int NextPrice=Prices[1];
           System.Console.WriteLine($"Check  id: {card.FbDataId}, CurrentPrice: {CurrentPrice}, NextPrice: {NextPrice} \n");
            if (NextPrice!=0 && CurrentPrice!=0)
            {   
            int profit = (int)(NextPrice*AFTER_TAX-CurrentPrice);
                if (profit> 0 && profit>=MIN_PROFIT){  
                    if (card.FbDataId == 237679)
                        return;
                    var history = await _history.GetSalesHistoryAsync(card.FbDataId, client);
                    if (history is null)
                    {
                        System.Console.WriteLine($"History is null or incorrect for {card.ShortName}");
                        return;
                    }

                    var lastTenSales = history?.Where(p=>p.status.Contains("closed"))
                             .Take(10);
                             
                    var avgPrice=(lastTenSales!.OrderByDescending(p=>p.Price).Select(p=>p.Price).Sum())/10;
                       if (NextPrice <= avgPrice)  
                       {
                            Console.WriteLine("\t => !!PROFIT!!!");
                            Console.WriteLine($"{card.ShortName } {card.Version} {card.Raiting} {card.Position} Profit: {profit} for {card.ShortName} {card.Version}");
                            Profit newProfit = new() {
                                CardId=card!.CardId,
                                Price=CurrentPrice,
                                SellPrice=NextPrice,
                                ProfitValue=profit,
                                Percentage=(decimal)CurrentPrice/NextPrice  
                        };
                        System.Console.WriteLine($"Profit: {profit} for {card.ShortName} {card.Version}");
                        await tggbot.SendInfo(newProfit,avgPrice,lastTenSales, card);
                     }
                }
                else
                System.Console.WriteLine($"ID: {card.FbDataId}. No profit"); 
            }
        }
     
        
       
       
    }
}