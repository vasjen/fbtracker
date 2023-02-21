using System.Net;

namespace fbtracker
{
    public class ProxyHandler

    {
        private readonly string _userName;
        private readonly string _userPassword;
        public readonly List<string>? _proxy;

        public ProxyHandler(IConfiguration config)
        {
            _userName = config.GetValue<string>("Proxy:Login");
            _userPassword=config.GetValue<string>("Proxy:Password");
            _proxy=config.GetSection("Proxy:Uri").Get<List<string>>();
                 
        }
        public IEnumerable<HttpClientHandler> GetProxyList(){
            List<HttpClientHandler> Proxies = new();
            for (int i=0;i<_proxy?.Count;i++)
            {   
                var handler =  new HttpClientHandler()
                {
                    Proxy = new WebProxy ( _proxy?.ElementAtOrDefault(i))
                    {
                        Credentials = ( new NetworkCredential 
                        {
                            UserName=_userName,
                            Password=_userPassword

                    })}
                };
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                Proxies.Add(handler);
            }
           return Proxies;
        }

    }
}