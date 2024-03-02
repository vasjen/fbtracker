using System.Net;
using System.Text;
using fbtracker.Models;
using fbtracker.Services.Interfaces;
using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;

namespace fbtracker.Services;

public class WebService : IWebService
{
    public List<HttpClient> Clients { get; init; }
    public  HttpClient Client { get => getNextClient() ;  } 
    private readonly HttpClient _client;
    private int CURRENT_INDEX = 0;
    private const string JSON_DATA = @"{
                        ""type"": ""ipv4"",
                        ""page"": 1,
                        ""page_size"": 100,
                        ""sort"": 1
                    }";

    public WebService(IHttpClientFactory clientFactory)
    {
        _client = clientFactory.CreateClient("proxy");
        this.Clients = CreateHttpClients(CreateHandlers(GetProxyList())).GetAwaiter().GetResult();
    }
    
    private HttpClient getNextClient()
    {
        HttpClient client = this.Clients[CURRENT_INDEX];
        CURRENT_INDEX = (CURRENT_INDEX + 1) % this.Clients.Count;
        return client;
    }
    
    private async IAsyncEnumerable<WebProxy> GetProxyList()
    {
        
        StringContent content = new StringContent(JSON_DATA, Encoding.UTF8, "application/json");
        HttpResponseMessage proxiesInJson = await _client.PostAsync(_client.BaseAddress, content);
        Proxies? proxiesFromJson = JsonConvert.DeserializeObject<Proxies>(await proxiesInJson.Content.ReadAsStringAsync());
        
        if (proxiesFromJson == null)
        {
            yield break;
        }
    
        foreach (Proxy item in proxiesFromJson.list.data)
        {
            yield return new WebProxy
            {
                Address = new Uri("http://" + item.ip + ":" + item.http_port),
                Credentials = new NetworkCredential(item.login, item.password)
            };
        }
    }
    
    private async IAsyncEnumerable<HttpClientHandler> CreateHandlers(IAsyncEnumerable<WebProxy> proxies)
    {   
        await foreach (WebProxy proxy in proxies)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                Proxy = proxy
            };
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; 
            yield return handler;
        }
    }
    
    private async Task<List<HttpClient>> CreateHttpClients(IAsyncEnumerable<HttpClientHandler> handlers)
    {
        List<HttpClient> clients = new();
        await foreach (HttpClientHandler handler in handlers)
        {
            HttpClient client = new HttpClient(handler: handler, disposeHandler: true);
            client.DefaultRequestHeaders
                .Add("User-Agent","User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");
            clients.Add(client);
        }

        return clients;
    }

   private async Task<bool> IsValidProxy(WebProxy proxy)
    {
        HttpClientHandler handler = new HttpClientHandler
        {
            Proxy = proxy
        };
        HttpClient client = new HttpClient(handler: handler, disposeHandler: true);
        HttpResponseMessage response = await client.GetAsync("https://futbin.com/");
        return response.IsSuccessStatusCode;
    }
   
   
    
}