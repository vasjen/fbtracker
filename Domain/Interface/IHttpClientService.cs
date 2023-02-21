namespace fbtracker
{
    public interface IHttpClientService
    {
        public HttpClient GetHttpClient();
        public int HandlerCount();
    }
}