using System.Net;

namespace fbtracker.Services.Interfaces;

public interface IWebService
{
    public List<HttpClient> Clients { get; }
    public HttpClient Client { get; }

    // HttpClient getNextClient();
    // IAsyncEnumerable<WebProxy> GetProxyList();
    // IAsyncEnumerable<HttpClientHandler> CreateHandlers(IAsyncEnumerable<WebProxy> proxies);
    // Task<List<HttpClient>> CreateHttpClients(IAsyncEnumerable<HttpClientHandler> handlers);

}