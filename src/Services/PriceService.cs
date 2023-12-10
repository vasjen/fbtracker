using System.Text.Json.Nodes;
using fbtracker.Models;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services
{

    public class PriceService : IPriceService{
       
        public async Task GetPriceAsync(Card card, HttpClient client)
        {
            if (card.FbDataId == 0) 
                Scraping.GetDataId(card,client);
     
            string requestUri = $"https://futbin.com/24/playerPrices?player={card.FbDataId}";
            Task.Delay(1000).Wait();  
            HttpResponseMessage response = await client.GetAsync(requestUri);
            Console.WriteLine(response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                    try
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        card.Prices = Scraping.GetPrices(card.FbDataId,jsonResponse);
                    }
                    catch (Exception ex) 
                    { 
                        Console.WriteLine(ex.Message);
                    }
            }
        }
    }
}