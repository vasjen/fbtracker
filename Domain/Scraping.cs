namespace fbtracker{
    public static class Scraping{

         public static async Task<string[]> GetPageAsStrings(HttpClient client,string Url){
            
            var httpRequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,Url);
             var send = await client.SendAsync(httpRequestMessage);
            var response= await send.Content.ReadAsStringAsync();
            var result = response.Split(Environment.NewLine);
            return result;
            
    }
    }
}