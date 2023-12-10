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
        public static int GetFbdataIdFromUrl(string url)
        {
            string lastPart = url.Substring(url.LastIndexOf('/') + 1);
            string id = lastPart.Remove(lastPart.IndexOf('.'));
            return Int32.Parse(Regex.IsMatch(id, @"[^\d]") ? id.Remove(0, 1) : id);
        }

        public static Prices GetPrices(int fbDataId,string jsonPrice)
        {
            JsonNode jsonNod = JsonNode.Parse(jsonPrice);
            Ps ps = new();
            ps.LCPrice = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice"].GetValue<string>());
            ps.LCPrice2 = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>());
            ps.LCPrice3 = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>());
            ps.LCPrice4 = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>());
            ps.LCPrice5 = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>());
            ps.Updated = jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["updated"].GetValue<string>();
            ps.MinPrice = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["MinPrice"].GetValue<string>());
            ps.MinPrice = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["MaxPrice"].GetValue<string>());
            ps.PRP = ConvertPriceToInt(jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["PRP"].GetValue<string>());
            ps.LCPClosing = jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPClosing"].GetValue<int>();
            
            Pc pc = new(); // Add in some feature updates PC support
            return new Prices( ps, pc );
        }
        private static int ConvertPriceToInt(string price)
            => Int32.Parse(price.Replace(",", ""));

    }
}