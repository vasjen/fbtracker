using System.Text.RegularExpressions;
using fbtracker.Models;
using fbtracker.Services.Interfaces;
using HtmlAgilityPack;

namespace fbtracker.Services {

    public static class SeedData
    {
        

        public static async IAsyncEnumerable<Card> EnsurePopulatedAsync(IHost host) {
            
           
            using (IServiceScope scope = host.Services.CreateScope())
            {
                IWebService webService =
                    scope.ServiceProvider
                        .GetRequiredService<IWebService>();
                List<HttpClient> clients =
                    webService.CreateHttpClients( webService.CreateHandlers( webService.GetProxyList())).GetAwaiter().GetResult();
                Console.WriteLine("Count of clients: {0} in SeedData", clients.Count);
                int currentIndex = 0;

                HttpClient getNextClient()
                {
                    HttpClient client = clients[currentIndex];
                    client.DefaultRequestHeaders
                        .Add("User-Agent","User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
                    currentIndex = (currentIndex + 1) % clients.Count;
                    return client;
                }
                
                    int numbers = await GetMaxNumberPage("https://www.futbin.com/players?player_rating=80-99&ps_price=10000-15000000");
                    for (int i = 1; i <= numbers; i++)
                    {
                        HttpClient client = getNextClient();
                        IAsyncEnumerable<Card> cards =  GetCards(
                            $"https://www.futbin.com/players?page={i}&player_rating=80-99&ps_price=10000-15000000", client);
                        await foreach (Card item in cards)
                        {
                            Console.WriteLine("Name: {0}, Version: {1}, Position: {2}, Rating {3}, FbId {4}, FbDataId {5}",item.ShortName,item.Version, item.Position, item.Rating, item.FbId, item.FbDataId);
                            // Console.WriteLine("Player page: {0}", URL + $"/player/{item.FbId}");
                            Console.WriteLine("---------------------------------------------------\n");
                            yield return item;
                        }
                    }
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
            Console.WriteLine("Max page is {0}", maxPage);
            return maxPage;
        }

        private static async IAsyncEnumerable<Card> GetCards(string url, HttpClient client)
        {
            string page = String.Empty;
            string ip = await client.GetStringAsync("http://api.ipify.org/");
            try
            {
                page = await client.GetStringAsync(url);
                Console.WriteLine("Success from ip: {0}",ip);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("Error from ip: {0}",ip);
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
                if (nodesIndex >= link.Count)
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
                    FbDataId = Scraping.GetFbdataIdFromUrl(Scraping.ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[2]/div[1]/img").GetAttributeValue("data-original","")),
                    // PromoUrl = await GetBackgroundImage(URL +  $"/24/player/{link[nodesIndex++].GetAttributeValue("data-site-id", "")}", client)
                
                };
            }
        }
        

        
        
        
    }
    
}
