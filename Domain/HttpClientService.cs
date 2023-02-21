using System.Net;

namespace fbtracker{
    public class HttpClientService : IHttpClientService {
         private Random rnd = new(); 
         public IEnumerable<HttpClient> Clients {get; private set;}
         public HttpClientService(ProxyHandler proxyHandler)
         {  
            var handlers = proxyHandler.GetProxyList();
           
            List<HttpClient> AllClients = new();
            for (int i=0;i<handlers.Count();i++) 
            {
                var client = new HttpClient(handlers.ElementAt(i));
                client.DefaultRequestHeaders
                .Add("User-Agent","User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
                AllClients.Add(client);
            }
            this.Clients=AllClients;
         }
         public HttpClient GetHttpClient() =>
             Clients.ElementAt(rnd.Next(Clients.Count()));

        public int HandlerCount()
        {
            return Clients.Count();
        }
    }
    
}