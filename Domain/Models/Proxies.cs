namespace Eafctracker.Models;


public class Proxy
{
    public int id { get; set; }
    public string login { get; set; }
    public string password { get; set; }
    public string ip { get; set; }
    public string http_port { get; set; }
    public string socks_port { get; set; }
    public string expired_at { get; set; }
    public string bought_at { get; set; }
    public string[] tags { get; set; }
    public string type { get; set; }
    public string proxy_type { get; set; }
}

public class ListProxies
{
    public bool error { get; set; }
    public int total { get; set; }
    public int page_size { get; set; }
    
    public List<Proxy> data { get; set; }
}

public class Proxies
{
    public bool success { get; set; }
    public string balance { get; set; }
    public ListProxies list { get; set; }
}
