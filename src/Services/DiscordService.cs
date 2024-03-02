using Discord;
using Discord.Rest;
using Discord.WebSocket;
using fbtracker.Models;
using fbtracker.Services.Interfaces;
namespace fbtracker.Services {
    public class DiscordService : INotificationService
    {
        
        private readonly string? _token;
        private const string URL = "https://www.futbin.com/player/";
        private readonly ulong CHANNEL_ID;

        public DiscordService(IConfiguration configuration)
        {
            _token = configuration.GetValue<string>("Discord:Token");
            CHANNEL_ID = UInt64.Parse(configuration.GetValue<string>("Discord:ChatId")); 
        }

       
        public async Task SendMessage(ProfitCard profitCardPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card)
        {
            DiscordSocketClient client = new DiscordSocketClient();
            if (client.ConnectionState != ConnectionState.Connected)
            {
                client.LoginAsync(TokenType.Bot, _token);
                client.StartAsync();
            }
            
            string notification = await CreateNotificationAsync(profitCardPlayer, avgPrice, lastTenSales, card);
            RestTextChannel? channel = await client.GetChannelAsync(CHANNEL_ID) as RestTextChannel;
            await channel.SendMessageAsync(
                notification);
        }
        
         private async Task<string> CreateNotificationAsync(ProfitCard profitCardPlayer, int avgPrice, IEnumerable<SalesHistory> lastSales, Card card)
         { 
             string profitMessage = 
                                 $"<a href=\"{URL}{card.FbId}\">{card.ShortName} {card.Version} "+
                                 $"{card.Rating} {card.Position}</a>" + 
                                 $"\n \n<u>New price:</u> <b>{profitCardPlayer.Price:0,0}</b> &#128176  \n<u>Profit:"+
                                 $"</u><b>{profitCardPlayer.ProfitValue:0,0}</b> &#128200 \n<u>Old price:</u> "+
                                 $"<b>{profitCardPlayer.SellPrice:0,0}</b> &#128176 \n"+
                                 $"\n<u>Change:</u> <b>- {1 - (profitCardPlayer.Percentage):0.00%}</b> &#128315 \n \n ";
            string historyMessage = 
                $" \n<u>Average:</u> <b>{avgPrice:0,0}</b>  &#128176 \n\n"+
                $"&#9201 List of the last ten sales: (UTC±0:00) &#9201\n  \n";
            List<SalesHistory> sales = lastSales.Take(10).ToList();
            
            historyMessage = sales.Aggregate(historyMessage, (current, t) => current + "<i>" + t.Price.ToString("0,0") + " in " + t.updated + " </i>\n");
            return profitMessage + historyMessage;    
        }

         public async Task SendMessageAsync(ProfitCard profitCardPlayer)
         {
             await SendMessage(profitCardPlayer,profitCardPlayer.SellPrice,profitCardPlayer.LastSales, profitCardPlayer.Card);
         }
    }
}