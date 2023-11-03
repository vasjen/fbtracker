using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using HtmlAgilityPack;

namespace fbtracker {

    public  class InitialService : IInitialService {
        
     
        private readonly IHttpClientService _service;
        public InitialService(IHttpClientService service) => _service=service;
         
        public  async Task<IEnumerable<Card>> GetCardsRangeAsync(){
            

            List<Card> cards = new List<Card>();
            string stringId=string.Empty;
            string name=string.Empty;
            string raiting=string.Empty;
            string version=string.Empty;
            string position=string.Empty;
            int counter=0;
            int MaxPage=await GetMaxNumberPage("https://www.futbin.com/players?page=1&player_rating=82-99&ps_price=20000-15000000");
            for (int pageNumber=1; pageNumber<=MaxPage; pageNumber++)
             {  
                try 
                {
                    var httpRequestMessage = new HttpRequestMessage(
                    HttpMethod.Get, $"https://www.futbin.com/players?page={pageNumber}&player_rating=82-99&ps_price=20000-15000000");
                    var _client = _service.GetHttpClient();
                    
                    var httpResponseMessage = await _client.SendAsync(httpRequestMessage);
                    var response = await httpResponseMessage.Content.ReadAsStringAsync();
                  
                    await Task.Delay(1000);
                    string[] result = response.Split(Environment.NewLine);
                    string findingString =string.Empty;
                    
                    for (int i=0;i<result.Length;i++)
                    {   //Getting FbId
                        if (result[i].Contains("data-site-id=") && !(result[i].Contains("<") || result[i].Contains("}") ))


                        {   stringId=result[i].Remove(result[i].LastIndexOf('"')).Substring(result[i].IndexOf('"')+1);
                           
                        }
                       
                        //Getting Name
                        if (result[i].Contains("data-tp-type=\"player\""))
                        {
                            name=result[i+1].Remove(result[i+1].LastIndexOf('<')).Substring(result[i+1].IndexOf('>')+1);
                        }
                        //Getting Raiting
                        if (result[i].Contains("form rating ut23") && !result[i].Contains("lazy"))
                        {
                            raiting = result[i].Substring(result[i].IndexOf("</span")-2,2);
                        }
                        //Getting Position
                        if (result[i].Contains("<div class=\"font-weight-bold\">")) 
                        {
                            position=result[i].Remove(result[i].LastIndexOf("<")).Substring(result[i].IndexOf(">")+1);
                        }
                        //Getting Version
                        if (result[i].Contains("<td class=\"mobile-hide-table-col\">") ) {

                            version=result[i+1].Remove(result[i+1].LastIndexOf("<")).Substring(result[i+1].IndexOf(">")+1);
                        }
                        //Getting Price
                        if (result[i].Contains("<span class=\" font-weight-bold\">"))
                        {   counter++;
                            string priceS=result[i].Remove(result[i].IndexOf("<img")-1).Substring(result[i].IndexOf("\">")+2);
                            cards.Add(new Card {FbId=int.Parse(stringId), Price=priceS, ShortName=name, Version=version,Raiting=int.Parse(raiting),Position=position});
                            System.Console.WriteLine($"[{counter}] Added {name} + {version}");
                        }
                        
                    }
                }    
             catch (Exception ex)
             {
                System.Console.WriteLine(ex.Message);
             }
             System.Console.WriteLine($"Added from {pageNumber} page of total: {MaxPage}");
            }
            System.Console.WriteLine($"Total added a {counter} cards");
            Parallel.ForEach(cards, new ParallelOptions {MaxDegreeOfParallelism = 5}, async p=> { 
                
                    GetDataId(p);
                    IsTradeble(p).Wait();

            });

        return cards;
        
        }
        public async Task<Card> GetNewCardAsync(int FbId)
        {
            var client=_service.GetHttpClient();
            string URL = $"https://www.futbin.com/player/{FbId}";
            var page = await Scraping.GetPageAsStrings(client,URL);
            Card card = new();
            for (int i=0;i<page.Length;i++)
            {
                if (page[i].Contains("data-url"))
                {
                    var found=page[i];
                    string dataUrl=found.Remove(found.LastIndexOf("\""));
                    string Name= dataUrl.Substring(dataUrl.LastIndexOf("/")+1);
                    card.ShortName=Name;
                   
                }
                if (page[i].Contains("pcdisplay-rat"))
                {
                    var found=page[i];
                    string pcdisplayRat=found.Remove(found.LastIndexOf("<"));
                    string Raiting = pcdisplayRat.Substring(pcdisplayRat.LastIndexOf(">")+1);
                    card.Raiting=int.Parse(Raiting);
                    
                }
                if (page[i].Contains("pcdisplay-pos"))
                {
                    var found=page[i];
                    string pcdisplayPos=found.Remove(found.LastIndexOf("<"));
                    string Position = pcdisplayPos.Substring(pcdisplayPos.LastIndexOf(">")+1);
                    card.Position=Position;
                }
                
                if (page[i].Contains("download-prices-player-revision"))
                {
                    var found=page[i];
                    string versionsOnPage=found.Remove(found.LastIndexOf("<"));
                    string Version = versionsOnPage.Substring(versionsOnPage.LastIndexOf(">")+1);
                    card.Version=Version;
                    card.FbId=FbId;
                }
            };
             GetDataId(card);
            IsTradeble(card).Wait();
            
            return card;
        }
        private void GetDataId(Card Card)
        {
            int counter=0;
                var client = _service.GetHttpClient();
                var result =  Scraping.GetPageAsStrings(client,$"http://www.futbin.com/player/{Card.FbId}");
                    if (result!=null){
                     for (int i=0; i<result.Result.Length;i++)
                     {
                        if (result.Result[i].Contains("data-player-resource")){
                            string id = result.Result[i].Remove(result.Result[i].LastIndexOf('"')).Substring(result.Result[i].LastIndexOf("=\"")+2);
                              Card.FbDataId=int.Parse(id);
                              break;
                        }
                
                     }
                     }
                     else {
                        System.Console.WriteLine("Page is null for {0} - {1} : {2}",Card.ShortName, Card.Version, Card.FbId);
                        System.Console.WriteLine("Or FBDataId not found, rly? {0}",Card.FbDataId);
                     }
                     
        }

        public async Task<int> GetMaxNumberPage(string Url)
        {
            var client = _service.GetHttpClient();
            var result = await Scraping.GetPageAsStrings(client,Url);
            string NumberString=string.Empty;
            int MaxPage=0;
            for (int i=result.Length-1;i>0;i--)
            {
                if (result[i].Contains("page-link \">"))
                {
                     NumberString = result[i];
                     MaxPage=int.Parse(NumberString.Remove(NumberString.IndexOf("</a")).Substring(NumberString.IndexOf("\">")+2));
                     break;
                    
                }
            }
            System.Console.WriteLine("Max page is {0}", MaxPage);
            return MaxPage;
        }
        public async IAsyncEnumerable<Card> GetCards(string url)
        {
            HttpClient client = _service.GetHttpClient();
            string page = await client.GetStringAsync(url);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page);
            
            int nodesIndex = 0;
            HtmlNodeCollection? link = doc.DocumentNode.SelectNodes("//a[@class='player_name_players_table get-tp']");
            for (int i = 1; i <= 61; i += 2)
            {
                if (i == 21 || i == 42)
                    i++;
                yield return new Card()
                {
                    FbId = Int32.Parse(link[nodesIndex++].GetAttributeValue("data-site-id", "")),
                    ShortName = ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[2]/div[2]/div[1]/a"),
                    Raiting = Int32.Parse( ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[3]/span")),
                    Position = ParseFromDoc(doc, $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[4]/div[1]"),
                    Version = ParseFromDoc(doc,
                        $"//*[@id=\"repTb\"]/tbody/tr[{i}]/td[5]/div[1]"),
                
                };
            }
        }
        private async  Task IsTradeble(Card card)
         {
          
            string Updated=string.Empty;
            var _client = _service.GetHttpClient();

            string requestUri = $"http://futbin.com/playerPrices?player={card.FbDataId}";
               
                
            Task.Delay(1500).Wait();
            var response =  await _client.GetAsync(requestUri);
             string PriceLowest=string.Empty;
             string PriceNext=string.Empty;
             string jsonResponse = string.Empty;
            
                    try 
                    {
                        if (response.IsSuccessStatusCode)   
                        jsonResponse = await response.Content.ReadAsStringAsync();
                        JsonNode jsonNod = JsonNode.Parse(jsonResponse);
                        Updated = jsonNod[$"{card.FbDataId}"]!["prices"]!["ps"]!["updated"].GetValue<string>();
                        PriceLowest = jsonNod[$"{card.FbDataId}"]!["prices"]["ps"]!["LCPrice"].GetValue<string>();
                        PriceNext = jsonNod[$"{card.FbDataId}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>();
                        card.Tradable = !(Updated.Contains("Never") || (PriceLowest=="0" || PriceNext=="0"));
                        
                System.Console.WriteLine("Card: {0} - {1} with FbId {2} is tradable => {3}",card.ShortName,card.Version,card.FbId,card.Tradable);
                    }
                    catch (Exception ex) { 
                        System.Console.WriteLine(ex.Message);
                        System.Console.WriteLine(response.Content.ReadAsStringAsync());
                        
                        }

                }
            
        private static string ParseFromDoc(HtmlDocument page,string xPath)
            =>  page.DocumentNode
                .SelectSingleNode(xPath)
                .InnerText;

            

        }
          
       }
