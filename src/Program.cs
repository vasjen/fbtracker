using fbtracker.Models;
using fbtracker.Services;
using fbtracker.Services.Interfaces;
using Telegram.Bot;

namespace fbtracker{

internal class Program
{
    private static async Task Main(string[] args)
    { 
        IHost host = new HostBuilder()
            .ConfigureServices( services =>
            { 
                services.AddTransient<IConfiguration>(sp => 
                { 
                    IConfigurationBuilder configurationBuilder = new ConfigurationBuilder(); 
                    configurationBuilder.AddJsonFile("appsettings.json")
                                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                    .AddUserSecrets<Program>();
                    return configurationBuilder.Build();
                });
                services.AddTransient<IWebService, WebService>();
                services.AddHttpClient("proxy",options =>
                {
                    IConfiguration configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
                    string? apikey = configuration.GetValue<string>("Proxy:API");
                    options.BaseAddress = new Uri(configuration.GetValue<string>("Proxy:Service") + apikey) ?? throw new ArgumentNullException(nameof(options.BaseAddress));
                    
                });
                services.AddTransient<IPriceService, PriceService>();
                services.AddTransient<IProfitService, ProfitService>();
                services.AddTransient<ISalesHistoryService,SalesHistoryService>();
                services.AddTransient<ITelegramService,TelegramService>();
                
                services.AddSingleton<ITelegramBotClient,TelegramBotClient>( p=>
                {
                    string? token = p.GetService<IConfiguration>().GetValue<string>("Telegram:Token");
                    return new TelegramBotClient(token: token);
                });
            
       })
        .Build();
       
       
       IProfitService profitService = host.Services.GetRequiredService<IProfitService>();
       
          while (true)
          {
              try
              {
                IAsyncEnumerable<Card> cards =  SeedData.EnsurePopulatedAsync(host);
                profitService.FindingProfitAsync(cards).GetAwaiter().GetResult();
              }
              catch (Exception e)
              {
                  Console.WriteLine(e.Message);
                  break;
              }
          }
    }
    
}
}


