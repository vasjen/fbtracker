using Discord;
using Discord.WebSocket;
using fbtracker.Services;
using fbtracker.Services.Interfaces;
using Serilog;
using Serilog.Sinks.Loki;
using Telegram.Bot;

namespace fbtracker{

internal class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddTransient<IConfiguration>(sp => 
        { 
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder(); 
            configurationBuilder.AddJsonFile("appsettings.json")
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddUserSecrets<Program>();
            return configurationBuilder.Build();
        });
        builder.Services.AddTransient<IWebService, WebService>();
        builder.Services.AddHttpClient("proxy",options =>
        {
            IConfiguration configuration = builder.Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
            string? apikey = configuration.GetValue<string>("Proxy:API");
            options.BaseAddress = new Uri(configuration.GetValue<string>("Proxy:Service") + apikey) ?? throw new ArgumentNullException(nameof(options.BaseAddress));
                    
        });
        builder.Services.AddTransient<IGetingCardData, GetingCardData>();
        builder.Services.AddScoped<SeedData>();
        builder.Services.AddTransient<IProfitService, ProfitService>();
        builder.Services.AddTransient<ISalesHistoryService,SalesHistoryService>();
        builder.Services.AddTransient<INotificationService,TelegramService>();
        // builder.Services.AddTransient<INotificationService,DiscordService>();
        builder.Services.AddTransient<IImageService,ImageService>();
        // builder.Services.AddSingleton<DiscordSocketClient>(p => 
        // {
            // DiscordSocketClient client = new DiscordSocketClient();
            // string? token = p.GetRequiredService<IConfiguration>().GetValue<string>("Discord:Token");
            // client.LoginAsync(TokenType.Bot, token);
            // client.StartAsync();
            // return client;
        // });
                
        builder.Services.AddSingleton<ITelegramBotClient,TelegramBotClient>( p=>
        {
            string? token = p.GetService<IConfiguration>().GetValue<string>("Telegram:Token");
            return new TelegramBotClient(token: token);
        });
        builder.Services.AddHostedService<PriceCheckerBackground>();
        
        builder.Services.AddLogging(p =>
        {
            p.AddSerilog();
        });
        builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(hostingContext.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.LokiHttp(new NoAuthCredentials("http://loki"));
        });
        WebApplication app = builder.Build();
        await app.RunAsync();
    }
    
}
}


