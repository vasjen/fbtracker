using System.Text.RegularExpressions;
using fbtracker.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace fbtracker.Services{
    public static class Scraping{
        
        public const string URL = "https://www.futbin.com"; 

        public static async Task<string[]> GetPageAsStrings(HttpClient client, string url)
        {
            string res = await client.GetStringAsync(url);
            return res.Split(Environment.NewLine);
        }
        public static HtmlNode ParseFromDoc(HtmlDocument page,string xPath)
            =>  page.DocumentNode
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
            client.DefaultRequestHeaders.Add("User-Agent", "User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
            string page = await client.GetStringAsync(url);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page);
            HtmlNode node = Scraping.ParseFromDoc(doc, "//*[@id=\"download-prices-ele\"]/div[7]/div[1]/img");
            string? link = node.GetAttributeValue("src", "");
           
            return URL + link;
        }
        public static int GetFbdataIdFromUrl(string url)
        {
            string lastPart =  url.Substring(url.LastIndexOf('/') + 1);
            string id = lastPart.Remove(lastPart.IndexOf('.'));
            return Int32.Parse(Regex.IsMatch(id, @"[^\d]") ? id.Remove(0, 1) : id);
        }
        
        public static Ps? GetPsPrices(string priceResponse)
            =>  JsonConvert.DeserializeObject<Ps>(priceResponse);

        public static Pc? GetPcPrices(string priceResponse)
            =>  JsonConvert.DeserializeObject<Pc>(priceResponse);
    }
}