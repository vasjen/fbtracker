
using System.Net;
using fbtracker.Services;
using fbtracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace fbtracker{

internal class Program
{
    
    private static async Task Main(string[] args)
    {  
         var host = new HostBuilder()
        .ConfigureServices( (Services) =>
       {
        Services.AddTransient<IConfiguration>(sp =>
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json")
                                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                .AddUserSecrets<Program>();
            return configurationBuilder.Build();
        });
        Services.AddTransient<IWebService, WebService>();
        Services.AddHttpClient("proxy",options =>
        {
            var Configuration = Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
            string? apikey = Configuration.GetValue<string>("Proxy:API");
            options.BaseAddress = new Uri($"https://proxy-seller.io/personal/api/v1/{apikey}/proxy/list/ipv4");
        });
        Services.AddTransient<IHttpClientService,HttpClientService>();
        Services.AddTransient<IPriceService, PriceService>();
        Services.AddTransient<IProfitService, ProfitService>();
        Services.AddTransient<IInitialService,InitialService>();
        Services.AddTransient<IUpdateService,UpdateService>();
        Services.AddTransient<ISalesHistoryService,SalesHistoryService>();
        Services.AddDbContext<FbDbContext>(ServiceLifetime.Transient);
        Services.AddTransient<ITelegramService,TelegramService>();
        Services.AddTransient<ProxyHandler>();
        
        Services.AddSingleton<ITelegramBotClient,TelegramBotClient>( (p)=>
        {
            var token = p.GetService<IConfiguration>().GetValue<string>("Telegram:Token");
            return new TelegramBotClient(token: token);
        });
        
       })
        .Build();
       
       
       var cards =  SeedData.EnsurePopulatedAsync(host);
        var update = host.Services.GetRequiredService<IUpdateService>();
        var ProfitService = host.Services.GetRequiredService<IProfitService>();
       
          while (true)
          {
              try
              {
                await ProfitService.FindingProfitAsync(cards);

              }
              catch (Exception e)
              {
                  Console.WriteLine(e.Message);
                  
              }
              // await update.ExistNewCardsAsync();
           
          }
             
 

}


}
}


