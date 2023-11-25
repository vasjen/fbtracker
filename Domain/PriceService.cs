using System.Text.Json.Nodes;

namespace fbtracker{

    public class PriceService : IPriceService{
       
        public async Task<int[]> GetPriceAsync(int FBDataID, HttpClient client)
        {
            if (FBDataID == 0)
                return new []{0,0};
            
            int CurrentPrice = 0;
            int NextPrice=0;

            string requestUri = $"https://futbin.com/24/playerPrices?player={FBDataID}";
            Task.Delay(1000).Wait();  
            var response = await client.GetAsync(requestUri);
            System.Console.WriteLine(response.StatusCode);

            if (response.IsSuccessStatusCode){   
                    try
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        JsonNode jsonNod = JsonNode.Parse(jsonResponse);
                        string Price = jsonNod[$"{FBDataID}"]!["prices"]["ps"]!["LCPrice"].GetValue<string>();
                        CurrentPrice=ConvertPriceToInt(Price);
                        Price = jsonNod[$"{FBDataID}"]!["prices"]["ps"]!["LCPrice2"].GetValue<string>();
                        NextPrice=ConvertPriceToInt(Price);
                    }
                    catch (Exception ex) 
                    { 
                        System.Console.WriteLine(ex.Message);
                        System.Console.WriteLine(await response.Content.ReadAsStringAsync());
                    }

                }
                int[] Prices = new int[2] {CurrentPrice,NextPrice};
                return Prices;
            

        }
        
       
        private static int ConvertPriceToInt(string price){
                 if (price.Contains(',')){
                    price=price.Remove(price.LastIndexOf(','),1);
                    if (price.Contains(','))
                    price=price.Remove(price.LastIndexOf(','),1);
                    }
            return int.Parse(price);
            }
         
        
    }
}