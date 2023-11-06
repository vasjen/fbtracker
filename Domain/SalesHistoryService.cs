using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace fbtracker {
public class SalesHistoryService : ISalesHistoryService{
        private readonly IHttpClientService _service;
     
   public IEnumerable<SalesHistory>? Histories { get; set; }
    public async Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync (int fbDataId, HttpClient client) {
            string URL="https://www.futbin.com/24/getPlayerSales?platform=ps&resourceId=";
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get,URL+fbDataId);
            var httpResponseMessage = await client.SendAsync(httpRequestMessage);
             var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
            string sales = await client.GetStringAsync(URL+fbDataId);
        try {
           Histories = JsonConvert.DeserializeObject<IEnumerable<SalesHistory>>(sales);
           
        }
        catch (JsonException ex)
        {
            System.Console.WriteLine(ex.Message);
            Console.WriteLine("Catch for {0}",fbDataId);
        }
        return Histories;
           
        }
      
        

}
 
 
       
       

}