using System.Net;
using Eafctracker.Models;
using fbtracker.Services.Interfaces;
using Newtonsoft.Json;

namespace fbtracker.Services;

public class WebService(IHttpClientFactory clientFactory) : IWebService
{
    private readonly HttpClient _client = clientFactory.CreateClient("proxy");

    public async IAsyncEnumerable<WebProxy> GetProxyList()
    {
        string proxiesInJson = await _client.GetStringAsync(_client.BaseAddress);
        Proxies? proxiesFromJson = JsonConvert.DeserializeObject<Proxies>(proxiesInJson);

        if (proxiesFromJson == null)
        {
            yield break;
        }

        foreach (Proxy item in proxiesFromJson.data.items)
        {
            yield return new WebProxy
            {
                Address = new Uri("http://" + item.ip + ":" + item.port_http),
                Credentials = new NetworkCredential(item.login, item.password)
            };
        }
    }
    
    public async IAsyncEnumerable<HttpClientHandler> CreateHandlers(IAsyncEnumerable<WebProxy> proxies)
    {
        await foreach (WebProxy proxy in proxies)
        {
            var handler = new HttpClientHandler
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

    public async IAsyncEnumerable<string> GetIpAddresses(IAsyncEnumerable<HttpClient> clients)
    {
        await foreach (HttpClient client in clients)
        {
            yield return await client.GetStringAsync("https://api.ipify.org/");
        }
    }
    
}