
using fbtracker.Services;
using fbtracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;


namespace fbtracker
{
    public class ProfitService(ISalesHistoryService _history, IPriceService priceService, FbDbContext context,
            ITelegramService tggbot, IHttpClientService client, IInitialService initial, IServiceProvider _services)
        : IProfitService
    {
        public int ProxyCount = 10;
        private  const double _afterTax=0.95;
        private  const double _minProfit=1000;
        private readonly FbDbContext _context = context;
        private readonly IHttpClientService _client = client;

        private List<Profit> ProfitPlayerList {get;set;} = new List<Profit>();


        public async Task FindingProfitAsync (IAsyncEnumerable<Card> Cards)
        {
            // var Cards = await _context.Cards
                          // .AsNoTracking()
                          // .Where(p=>p.Tradable==true)
                          // .ToListAsync();
             using (IServiceScope scope = _services.CreateScope())
             {
                 IWebService webService = 
                     scope.ServiceProvider
                         .GetRequiredService<IWebService>();
                List<HttpClient> clients = await  webService.CreateHttpClients(webService.CreateHandlers(webService.GetProxyList()));
                int currentIndex = 0;
                HttpClient GetNextClient()
                {
                    HttpClient client = clients[currentIndex];
                    currentIndex = (currentIndex + 1) % clients.Count;
                    return client;
                }
                Parallel.ForEach(await Cards.ToListAsync(),
                    new ParallelOptions { MaxDegreeOfParallelism = clients.Count }, async p =>
                    {
                        if (p.FbDataId == 0)
                            p.FbDataId = initial.GetDataId(p, GetNextClient());
                        try
                        {
                            var client = GetNextClient();
                            CheckProfitAsync(p, client).Wait();
                            
                        }
                        catch (Exception ex) { 
                            Console.WriteLine(ex.Message);
                            Console.WriteLine($"Error with {p.FbDataId}");
                        }
                
                    });
                
             }
            List<int> ErrorCards = new();
            // await foreach (var item in Cards)
            // {
                // if (item.FbDataId == 0)
                    // item.FbDataId = _initial.GetDataId(item);

                // Console.WriteLine("Name: {0}, Version: {1}, Position: {2}, Rating {3}",item.ShortName,item.Version, item.Position, item.Raiting);
                // await CheckProfitAsync(item.FbDataId);
            // }
            
        }
        
        private async Task CheckProfitAsync(Card card, HttpClient client)
        {
            await Task.Delay(1000);
            var Prices= await priceService.GetPriceAsync(card.FbDataId, client);
            var CurrentPrice=Prices[0];
            var NextPrice=Prices[1];
           System.Console.WriteLine($"Check  id: {card.FbDataId}, CurrentPrice: {CurrentPrice}, NextPrice: {NextPrice} \n");
            if (NextPrice!=0 && CurrentPrice!=0)
            {   
            int profit = (int)(NextPrice*_afterTax-CurrentPrice);
                if (profit> 0 && profit>=_minProfit){  
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
                      if (NextPrice<=avgPrice)  
                      {
                        System.Console.WriteLine("\t => !!PROFIT!!!");
                        Console.WriteLine($"{card.ShortName } {card.Version} {card.Raiting} {card.Position} Profit: {profit} for {card.ShortName} {card.Version}");
                        foreach (var item in lastTenSales)
                        {
                            Console.WriteLine($"{item.unix_date} {item.Price} {item.status} {item.updated}");
                        }
                        

                        Profit NewProfit = new Profit(){
                            CardId=card!.CardId,
                            Price=CurrentPrice,
                            SellPrice=NextPrice,
                            ProfitValue=profit,
                            Percentage=(decimal)CurrentPrice/NextPrice  
                            

                        };
                        System.Console.WriteLine($"Profit: {profit} for {card.ShortName} {card.Version}");
                        // await _context.AddAsync(NewProfit);
                        // await _context.SaveChangesAsync();
                        await tggbot.SendInfo(NewProfit,avgPrice,lastTenSales, card);
                      }
                     
                }
                else
                System.Console.WriteLine($"ID: {card.FbDataId}. No profit"); 
            }
        
        
            
        }
     
        
       
       
    }
}