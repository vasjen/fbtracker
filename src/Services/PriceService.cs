using System.Text.Json.Nodes;
using fbtracker.Models;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services
{

    public class PriceService : IPriceService{
        private readonly ILogger<PriceService> _logger;

        public PriceService(ILogger<PriceService> logger)
        {
            _logger = logger;
        }
       
        public async Task GetPriceAsync(Card card, HttpClient client)
        {
            if (card.FbDataId == 0) 
                Scraping.GetDataId(card,client);
            
            _logger.LogInformation("Check price for {0}", card.ToString());
            string requestUri = $"https://futbin.com/24/playerPrices?player={card.FbDataId}";
            await Task.Delay(1000);
            try
            {
                HttpResponseMessage response = await client.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    card.Prices = Scraping.GetPrices(card.FbDataId, jsonResponse);
                }
                else
                {
                    _logger.LogInformation("Can't get a price for {0}", card.ToString());
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }
    }
}