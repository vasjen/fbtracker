using System.Text.Json.Nodes;

namespace fbtracker{

    public class PriceService : IPriceService{
        private readonly IHttpClientService _service;
        private readonly FbDbContext _context;

        public PriceService(IHttpClientService service, FbDbContext context)
        {
            
            _service=service;
            _context=context;
        }
         
       

        public async Task<int[]> GetPriceAsync(int FBDataID)
        {    
            int CurrentPrice=0;
            int NextPrice=0;
            var _client = _service.GetHttpClient();

            string requestUri = $"http://futbin.com/23/playerPrices?player={FBDataID}";
            Task.Delay(1000).Wait();  
            var response = await _client.GetAsync(requestUri);
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
                    catch (Exception ex) { 
                        System.Console.WriteLine(ex.Message);
                        System.Console.WriteLine(response.Content.ReadAsStringAsync());
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