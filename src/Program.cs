using Discord;
using Discord.WebSocket;
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
                services.AddScoped<SeedData>();
                services.AddTransient<IProfitService, ProfitService>();
                services.AddTransient<ISalesHistoryService,SalesHistoryService>();
                services.AddTransient<ITelegramService,TelegramService>();
                services.AddTransient<INotificationService,DiscordService>();
                services.AddTransient<IImageService,ImageService>();
                services.AddSingleton<DiscordSocketClient>(p => 
                {
                    DiscordSocketClient client = new DiscordSocketClient();
                    string? token = p.GetRequiredService<IConfiguration>().GetValue<string>("Discord:Token");
                    client.LoginAsync(TokenType.Bot, token);
                    client.StartAsync();
                    return client;
                });
                
                services.AddSingleton<ITelegramBotClient,TelegramBotClient>( p=>
                {
                    string? token = p.GetService<IConfiguration>().GetValue<string>("Telegram:Token");
                    return new TelegramBotClient(token: token);
                });
                services.AddHostedService<PriceCheckerBackground>();
                services.AddLogging(p =>
                {
                    p.AddConsole();
                });
       })
        .Build();
        await host.RunAsync();
    }
    
}
}


