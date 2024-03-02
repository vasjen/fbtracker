using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using fbtracker.Models;
using HtmlAgilityPack;
using Mapster;
using Newtonsoft.Json;

namespace fbtracker.Services{
    public static class Scraping
    {

        public const string URL = "https://www.futbin.com";

        public static async Task<string[]> GetPageAsStrings(HttpClient client, string url)
        {
            await Task.Delay(1500);
            string res = await client.GetStringAsync(url);
            return res.Split(Environment.NewLine);
        }

        public static HtmlNode ParseFromDoc(HtmlDocument page, string xPath)
            => page.DocumentNode
                .SelectSingleNode(xPath);

        public static async Task<HtmlDocument> LoadPage(HttpClient client, string pageLink)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(await client.GetStringAsync(pageLink));
            return doc;
        }

        public static string GetBackgroundImage(string url)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent",
                "User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
            string link = string.Empty;
            try
            {
                string page = client.GetStringAsync(url).Result;
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(page);
                HtmlNode node = Scraping.ParseFromDoc(doc, "//*[@id=\"download-prices-ele\"]/div[7]/div[1]/img");
                link = node.GetAttributeValue("src", "");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            

            return URL + link;
        }
        public static int GetDataId(Card card, HttpClient client)
        {
            Task.Delay(2000);
            Task<string[]>? result =  Scraping.GetPageAsStrings(client,$"http://www.futbin.com/player/{card.FbId}");
            int fbdataid = 0;
            for (int i = 0; i < result.Result.Length; i++)
            {
                if (result.Result[i].Contains("data-player-resource")){
                    string id = result.Result[i].Remove(result.Result[i].LastIndexOf('"')).Substring(result.Result[i].LastIndexOf("=\"")+2);
                    card.FbDataId = int.Parse(id);
                    fbdataid =  int.Parse(id);
                }
            }
            return fbdataid;
        }

        public static Prices GetPrices(int fbDataId,string jsonPrice)
            =>  new (
                    GetPrice<Ps>(fbDataId, jsonPrice), new Pc() 
                    // GetPrice<Pc>(fbDataId, jsonPrice, "pc")
                );
        
        

        private static TBasePrice GetPrice<TBasePrice>(int fbDataId, string jsonPrice)
        where TBasePrice : BasePrice, new()
        {
            TBasePrice price = new();
            JsonNode jsonNod = JsonNode.Parse(jsonPrice);
            // TBasePrice Psprice = jsonPrice.Adapt<TBasePrice>();
            try
            {
                    price.LCPrice = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["LCPrice"].GetValue<string>());
                    price.LCPrice2 = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["LCPrice2"].GetValue<string>());
                    price.LCPrice3 =
                        ConvertPriceToInt(
                            jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["LCPrice2"]
                                .GetValue<string>());
                    price.LCPrice4 =
                        ConvertPriceToInt(
                            jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["LCPrice2"]
                                .GetValue<string>());
                    price.LCPrice5 =
                        ConvertPriceToInt(
                            jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["LCPrice2"]
                                .GetValue<string>());
                    price.Updated =
                        jsonNod[$"{fbDataId}"]!["prices"]?[$"{typeof(TBasePrice).Name.ToLower()}"]!["updated"]
                            .GetValue<string>();
                    price.MinPrice =
                        ConvertPriceToInt(
                            jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["MinPrice"]
                                .GetValue<string>());
                    price.MaxPrice =
                        ConvertPriceToInt(
                            jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["MaxPrice"]
                                .GetValue<string>());
                    price.PRP = ConvertPriceToInt(
                        jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["PRP"]
                            .GetValue<string>());
                    // price.LCPClosing =
                        // jsonNod[$"{fbDataId}"]!["prices"][$"{typeof(TBasePrice).Name.ToLower()}"]!["LCPClosing"]
                            // .GetValue<int>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            

            return price;
        }
        
        private static int ConvertPriceToInt(string price)
            => Int32.Parse(price.Replace(",", ""));

    }
}