using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using fbtracker.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace fbtracker.Services{
    public static class Scraping
    {

        public const string URL = "https://www.futbin.com";

        public static async Task<string[]> GetPageAsStrings(HttpClient client, string url)
        {
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

        public static async Task<string> GetBackgroundImage(string url)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent",
                "User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
            string page = await client.GetStringAsync(url);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page);
            HtmlNode node = Scraping.ParseFromDoc(doc, "//*[@id=\"download-prices-ele\"]/div[7]/div[1]/img");
            string? link = node.GetAttributeValue("src", "");

            return URL + link;
        }
        public static int GetDataId(Card card, HttpClient client)
        {
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
                    GetPrice(fbDataId, jsonPrice,"ps") as Ps, 
                    GetPrice(fbDataId, jsonPrice, "pc") as Pc
                );
        

        private static BasePrice GetPrice(int fbDataId, string jsonPrice, string typePrice)
        {
            
            if (typePrice != "ps" && typePrice != "pc")
                throw new ArgumentException("Type price must be ps or pc");
            
            JsonNode jsonNod = JsonNode.Parse(jsonPrice);
            BasePrice price = typePrice switch
            {
                "ps" => new Ps(),
                "pc" => new Pc(),
            };
            price.LCPrice =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["LCPrice"].GetValue<string>());
            price.LCPrice2 =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["LCPrice2"].GetValue<string>());
            price.LCPrice3 =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["LCPrice2"].GetValue<string>());
            price.LCPrice4 =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["LCPrice2"].GetValue<string>());
            price.LCPrice5 =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["LCPrice2"].GetValue<string>());
            price.Updated = jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["updated"].GetValue<string>();
            price.MinPrice =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["MinPrice"].GetValue<string>());
            price.MaxPrice =
                ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["MaxPrice"].GetValue<string>());
            price.PRP = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["PRP"].GetValue<string>());
            price.LCPClosing = jsonNod[$"{fbDataId}"]!["prices"][$"{typePrice}"]!["LCPClosing"].GetValue<int>();
            
            return price;
        }
        
        private static int ConvertPriceToInt(string price)
            => Int32.Parse(price.Replace(",", ""));

    }
}