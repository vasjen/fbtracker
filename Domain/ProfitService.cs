
using Microsoft.EntityFrameworkCore;


namespace fbtracker
{
    public class ProfitService :IProfitService
    {
        public int ProxyCount =7;
        private  const double _afterTax=0.95;
        private  const double _minProfit=0.03;
        private readonly ISalesHistoryService _history;
        private readonly IPriceService _priceService;
        private readonly FbDbContext _context;
        private readonly ITelegramService _tgbot;
        private readonly IHttpClientService _client;

        private List<Profit> ProfitPlayerList {get;set;} = new List<Profit>();
        

        public ProfitService(ISalesHistoryService history, IPriceService priceService, FbDbContext context, ITelegramService tggbot, IHttpClientService client)
        {
            _history=history;
            _priceService=priceService;
            _context=context;
            _tgbot=tggbot;
            _client=client;
        }
        
        public async Task FindingProfitAsync ()
        {
            var Cards = await _context.Cards
                          .AsNoTracking()
                          .Where(p=>p.Tradable==true)
                          .ToListAsync();
              System.Console.WriteLine("Load {0} card",Cards.Count);
            List<int> ErrorCards = new();
           
            Parallel.ForEach(Cards, new ParallelOptions { MaxDegreeOfParallelism = ProxyCount }, async p =>
            {
                
                try
                {
                    CheckProfitAsync(p.FbDataId).Wait();
                }
                catch (Exception ex) { Console.WriteLine(ex.Message);
                    ErrorCards.Add(p.FbDataId);
                }
                
            });
         
           
            foreach (var item in ErrorCards) {
                await CheckProfitAsync(item);
            }
            ErrorCards.Clear();
        }
        
        private async Task CheckProfitAsync(int FBDataId)
        {
            await Task.Delay(1000);
            var Prices= await _priceService.GetPriceAsync(FBDataId);
            var CurrentPrice=Prices[0];
            var NextPrice=Prices[1];
           System.Console.WriteLine($"Check  id: {FBDataId}, CurrentPrice: {CurrentPrice}, NextPrice: {NextPrice} \n");
            if (NextPrice!=0 && CurrentPrice!=0)
            {   
            int profit = (int)(NextPrice*_afterTax-CurrentPrice);
                if (profit> 0 && profit>=CurrentPrice*_minProfit){  
                    
                    var history = await _history.GetSalesHistoryAsync(FBDataId);

                    var lastTenSales = history?.Where(p=>p.status.Contains("closed"))
                             .Take(10);
                             
                    var avgPrice=(lastTenSales!.OrderByDescending(p=>p.Price).Select(p=>p.Price).Sum())/10;
                      if (NextPrice<=avgPrice)  
                      {
                        System.Console.WriteLine("\t => !!PROFIT!!!");
                        
                        var card = await _context.Cards.AsNoTracking().Where(p=>p.FbDataId==FBDataId).FirstOrDefaultAsync();

                        Profit NewProfit = new Profit(){
                            CardId=card!.CardId,
                            Price=CurrentPrice,
                            SellPrice=NextPrice,
                            ProfitValue=profit
                            

                        };
                        System.Console.WriteLine($"Profit: {profit} for {card.ShortName} {card.Version}");
                        await _context.AddAsync(NewProfit);
                        await _context.SaveChangesAsync();
                        await _tgbot.SendInfo(NewProfit,avgPrice,lastTenSales);
                      }
                     
                }
                else
                System.Console.WriteLine($"ID: {FBDataId}. No profit"); 
            }
        

            
        }
     

        
    }
}