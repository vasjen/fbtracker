namespace Eafctracker.Models;


public class Proxy
{
    public string id { get; set; }
    public string order_id { get; set; }
    public string basket_id { get; set; }
    public string ip { get; set; }
    public string ip_only { get; set; }
    public string protocol { get; set; }
    public int port_socks { get; set; }
    public int port_http { get; set; }
    public string login { get; set; }
    public string password { get; set; }
    public string auth_ip { get; set; }
    public string? rotation { get; set; }
    public string link_reboot { get; set; }
    public string country { get; set; }
    public string country_alpha3 { get; set; }
    public string status { get; set; }
    public string status_type { get; set; }
    public bool can_prolong { get; set; }
    public string date_start { get; set; }
    public string date_end { get; set; }
    public string comment { get; set; }
    public string auto_renew { get; set; }
    public string auto_renew_period { get; set; }
}

public class Data
{
    public List<Proxy> items { get; set; }
}

public class Proxies
{
    public string status { get; set; }
    public Data data { get; set; }
    public List<string>? errors { get; set; }
}
