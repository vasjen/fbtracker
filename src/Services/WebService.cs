using System.Net;
using System.Text;
using Eafctracker.Models;
using fbtracker.Services.Interfaces;
using Newtonsoft.Json;

namespace fbtracker.Services;

public class WebService : IWebService
{
    private readonly HttpClient _client;

    public WebService(IHttpClientFactory clientFactory)
    {
        _client = clientFactory.CreateClient("proxy");
    }
    public async IAsyncEnumerable<WebProxy> GetProxyList()
    {
        string jsonData = @"{
            ""type"": ""ipv4"",
            ""page"": 1,
            ""page_size"": 10,
            ""sort"": 1
        }";
        StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
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
    
    public async IAsyncEnumerable<HttpClientHandler> CreateHandlers(IAsyncEnumerable<WebProxy> proxies)
    {   
        await foreach (WebProxy proxy in proxies)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                Proxy = proxy
            };
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            yield return handler;
        }
    }
    
    public async Task<List<HttpClient>> CreateHttpClients(IAsyncEnumerable<HttpClientHandler> handlers)
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
        Console.WriteLine("For proxy {0} status code is {1}", proxy.Address, response.StatusCode);
        return response.IsSuccessStatusCode;
    }
    
}