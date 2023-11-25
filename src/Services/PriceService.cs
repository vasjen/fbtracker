using System.Text.Json.Nodes;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services
{

    public class PriceService : IPriceService{
       
        public async Task<int[]> GetPriceAsync(int fbDataId, HttpClient client)
        {
            if (fbDataId == 0)
                return new []{0,0};
            
            int currentPrice = 0;
            int nextPrice = 0;

            string requestUri = $"https://futbin.com/24/playerPrices?player={fbDataId}";
            Task.Delay(1000).Wait();  
            HttpResponseMessage response = await client.GetAsync(requestUri);
            Console.WriteLine(response.StatusCode);

            if (response.IsSuccessStatusCode)
            {   
                    try
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        JsonNode jsonNod = JsonNode.Parse(jsonResponse);
                        string price = jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice"].GetValue<string>();
                        currentPrice = ConvertPriceToInt(price);
                        price = jsonNod[$"{fbDataId}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>();
                        nextPrice = ConvertPriceToInt(price);
                    }
                    catch (Exception ex) 
                    { 
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(await response.Content.ReadAsStringAsync());
                    }

            }
            int[] prices = new int[2] {currentPrice,nextPrice};
            return prices;
        }
        
       
        private static int ConvertPriceToInt(string price)
        {
            if (price.Contains(','))
            {
                price=price.Remove(price.LastIndexOf(','),1);
                if (price.Contains(','))
                { 
                    price=price.Remove(price.LastIndexOf(','),1); 
                }
            }
            return Int32.Parse(price);
        }
    }
}