using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace fbtracker {
public class SalesHistoryService : ISalesHistoryService{
        private readonly IHttpClientService _service;
     

        public SalesHistoryService(IHttpClientService service)
        {
         _service=service;
        }

   public IEnumerable<SalesHistory>? Histories { get; set; }
    public async Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync (int fbDataId) {
            string URL="https://www.futbin.com/23/getPlayerSales?platform=ps&resourceId=";
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get,URL+fbDataId);
            var _client = _service.GetHttpClient();
            var httpResponseMessage = await _client.SendAsync(httpRequestMessage);
             var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
        
        try {
           Histories = await JsonSerializer.DeserializeAsync<IEnumerable<SalesHistory>>(contentStream);
           
        }
        catch (JsonException ex)
        {
            System.Console.WriteLine(ex.Message);
            var code = httpResponseMessage.IsSuccessStatusCode;
            System.Console.WriteLine(code);
            System.Console.WriteLine("The item doesn't contains a sales history");
        }
        return Histories;
           
        }
      
        

}
 
 
       
       

}