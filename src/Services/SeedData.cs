using System.Diagnostics;
using fbtracker.Models;
using fbtracker.Services.Interfaces;
using HtmlAgilityPack;

namespace fbtracker.Services {

    public class SeedData
    {
        private readonly ILogger<SeedData> _logger;

        public SeedData(ILogger<SeedData> logger)
        {
            _logger = logger;
        }

        public async IAsyncEnumerable<Card> EnsurePopulatedAsync(IServiceProvider services) {
            
           
            using (IServiceScope scope = services.CreateScope())
            {
                IWebService webService =
                    scope.ServiceProvider
                        .GetRequiredService<IWebService>();
                
                Stopwatch timer = new();
                timer.Start();
                    int numbers = await GetMaxNumberPage("https://www.futbin.com/players?player_rating=80-99&ps_price=10000-15000000");
                    for (int i = 1; i <= numbers; i++)
                    {
                        HttpClient client = webService.Client;
                        IAsyncEnumerable<Card> cards =  GetCards(
                            $"https://www.futbin.com/players?page={i}&player_rating=80-99&ps_price=10000-15000000", client, _logger);
                        await foreach (Card item in cards)
                        {
                            _logger.LogInformation(item.ToString());
                            yield return item;
                        }
                    }
                    _logger.LogInformation("Total time getting all cards: {0}", timer.Elapsed);
            }
        }

        private static async Task<int> GetMaxNumberPage(string Url)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders
                .Add("User-Agent","User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
            string[] result = await Scraping.GetPageAsStrings(client,Url);
            int maxPage = 0;
            for (int i = result.Length - 1; i > 0; i--)
            {
                if (result[i].Contains("page-link \">"))
                {
                    string numberString = result[i];
                    maxPage= int.Parse(numberString.Remove(numberString.IndexOf("</a")).Substring(numberString.IndexOf("\">")+2));
                    break;
                }
            }
            return maxPage;
        }

        private static async IAsyncEnumerable<Card> GetCards(string url, HttpClient client, ILogger logger)
        {
            string page = String.Empty;
            try
            {
                page = await client.GetStringAsync(url);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                yield break;
            }
            
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page);
            int nodesIndex = 0;
            HtmlNodeCollection? link = doc.DocumentNode.SelectNodes("//a[@class='player_name_players_table get-tp']");
           
            for (int i = 1; i <= 61; i += 2)
            {
                if (i == 21 || i == 42)
                    i++;
                if (nodesIndex >= link?.Count)
                    break;
                
                yield return new Card()
                {
                    FbId = Int32.Parse(link[nodesIndex++].GetAttributeValue("data-site-id", "")),
                    ShortName = Scraping.ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[2]/div[2]/div[1]/a").InnerText,
                    Rating = Int32.Parse( Scraping.ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[3]/span").InnerText),
                    Position = Scraping.ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[4]/div[1]").InnerText,
                    Version = Scraping.ParseFromDoc(doc,
                        $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[5]/div[1]").InnerText,
                    ImageUrl = Scraping.ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[2]/div[1]/img").GetAttributeValue("data-original",""),
                };
            }
        }
    }
    
}
