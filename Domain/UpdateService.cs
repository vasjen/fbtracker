using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace fbtracker
{
    public class UpdateService : IUpdateService
    {
        private readonly FbDbContext _context;
        private readonly IConfiguration _config;
        private readonly IHttpClientService _service;
        private readonly IInitialService _initial;
        private  const string Url="https://www.futbin.com/latest";

        private int maxFbIdFromSite;
        private int maxFbIdFromDb;

        public UpdateService(FbDbContext context, IHttpClientService service, IInitialService initial)
        {
            _context=context;
            _service=service;
            _initial=initial;
        }

        public async Task ExistNewCardsAsync(){
            maxFbIdFromDb = await _context.Cards
                    .AsNoTracking()
                    .OrderByDescending(p=>p.FbId)
                    .Select(p=>p.FbId)
                    .FirstAsync();
            var client = _service.GetHttpClient();
            var Page = await Scraping.GetPageAsStrings(client,Url);
            maxFbIdFromSite = GetMaxFbId(Page); 
            if (maxFbIdFromSite>maxFbIdFromDb)
              {
                System.Console.WriteLine("Need update, founded a {0} new card", maxFbIdFromSite-maxFbIdFromDb);
                System.Console.WriteLine("Trying to get a new cards");
                  
                  List<int> Fbids = new();
                  for (int i=maxFbIdFromDb+1;i<=maxFbIdFromSite;i++)
                      Fbids.Add(i);
                  var NewCards =  await GetNewCardsRange(Fbids);
                  _context.AddRange(NewCards);
                  _context.SaveChanges(); 
                  foreach (var card in NewCards){
                    System.Console.WriteLine($"Had add card with name: {card.ShortName} => FbId: {card.FbId}, FbDataId: {card.FbDataId}, Tradable? {card.Tradable}");
                  }
              }
              else {
                System.Console.WriteLine("No update, FbId in DM is max: {0}",maxFbIdFromDb);
              }
           
        }


        public int GetMaxFbId(string[] Page)
        {
            string FbId_string=string.Empty;
        
            for (int i=0;i<Page.Length;i++)
            {
                if (Page[i].Contains("data-site-id") && !Page[i].Contains("."))
                {
                    var found=Page[i];
                    FbId_string=found.Remove(found.LastIndexOf('\"')).Substring(found.IndexOf("=\"")+2);
                    break;
                }
            }
           
            System.Console.WriteLine("Max index: {0}",FbId_string);
            
            return int.Parse(FbId_string);
        }

        public async Task<Card> GetNewCardAsync(int FbId)
        {
            return  await _initial.GetNewCardAsync(FbId);
        }

        public async Task<IEnumerable<Card>> GetNewCardsRange(IEnumerable<int> FbIds)
        {
            List<Card> Cards = new();
            Card card = new();
            foreach(var FbId in FbIds)
                {
                    card =await _initial.GetNewCardAsync(FbId);
                    Cards.Add(card);
                }
            return Cards;
        }
    }
}